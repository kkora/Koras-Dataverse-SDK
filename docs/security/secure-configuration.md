# Secure Configuration Guide

> Guidance for consumers of the Koras Dataverse SDK on configuring authentication and
> connection settings securely. Consistent with
> [master plan §4/§7](../planning/master-plan.md) and the
> [threat model](threat-model.md). API shown is the planned MVP surface.

## 1. Principles

1. **Secrets never live in source control.** Not in `appsettings.json`, not in code, not
   in test fixtures, not in samples.
2. **Prefer credentials that are not secrets at all:** managed identity first, then
   certificates, then client secrets as the fallback of last resort.
3. **Configuration and secret material are separate concerns:** the *shape* (URL, tenant,
   client id) can live in `appsettings.json`; the *secret* comes from a secret store.
4. **Fail fast:** the SDK validates options at startup (DataAnnotations + custom rules),
   so a misconfigured deployment dies at boot, not at 3 a.m. under traffic.
5. **Least privilege in Dataverse:** the application user gets a custom role with exactly
   the tables/privileges it needs.

## 2. Choosing an authentication mode

| Mode (via `DataverseAuthenticationOptions`) | Use when | Secret to protect |
|---|---|---|
| `UseManagedIdentity()` | Running in Azure (App Service, Functions, AKS, VMs) | None — this is why it is first choice |
| `UseCertificate(...)` | Non-Azure hosting, CI, on-premises services | Certificate private key (store in Key Vault or machine store, never as a file in the repo) |
| `UseClientSecret(tenantId, clientId, secret)` | Last resort; quick starts; environments without cert infrastructure | The client secret — rotate regularly, short expiry |
| `UseDefault()` (`DefaultAzureCredential`) | Local dev + Azure prod with one code path | Depends on the resolved credential |
| `UseInteractive()` | Developer tools / local experiments only | None persistent; never in services |
| `UseTokenCredential(cred)` | Sovereign clouds, custom MSAL setups, brokered scenarios | Whatever the custom credential holds |

Certificates are preferred over secrets because the private key never transits to Entra
ID (proof-of-possession via signed assertion), they integrate with managed rotation, and
leaked *public* configuration reveals nothing usable.

## 3. Environment-specific setup

### 3.1 Local development — user-secrets

Keep the non-secret shape in `appsettings.json` (§5) and put the secret in the .NET
user-secrets store, which lives outside the repository:

```bash
dotnet user-secrets init
dotnet user-secrets set "Dataverse:Authentication:ClientSecret" "<dev-secret>"
```

```csharp
builder.Services.AddDataverse(o =>
{
    var section = builder.Configuration.GetSection("Dataverse");
    o.EnvironmentUrl = new Uri(section["EnvironmentUrl"]!);
    o.Authentication.UseClientSecret(
        section["Authentication:TenantId"]!,
        section["Authentication:ClientId"]!,
        section["Authentication:ClientSecret"]!); // resolved from user-secrets in dev
});
```

Even better for local dev: `UseDefault()` with `az login` / Visual Studio credentials
against a personal developer environment — zero stored secrets.

For the SDK's own live integration tests, credentials come from
`KORAS_DATAVERSE_URL` / `KORAS_DATAVERSE_TENANT_ID` / `KORAS_DATAVERSE_CLIENT_ID` /
`KORAS_DATAVERSE_CLIENT_SECRET` environment variables — same rule: shell/CI secret
store, never files in the repo
([integration-testing.md](../testing/integration-testing.md)).

### 3.2 Production — managed identity (preferred)

```csharp
builder.Services.AddDataverse(o =>
{
    o.EnvironmentUrl = new Uri(builder.Configuration["Dataverse:EnvironmentUrl"]!);
    o.Authentication.UseManagedIdentity(); // no secret exists anywhere
});
```

Requirements: create an application user in the Dataverse environment for the managed
identity's service principal and assign its least-privilege role (§7). No Key Vault
round-trip is needed because there is no secret.

### 3.3 Production — Key Vault when managed identity cannot reach Dataverse directly

If you must use a client secret or certificate, store it in Azure Key Vault and load it
through the Key Vault configuration provider (authenticated by managed identity), so the
secret reaches the app as configuration without ever being deployed with it:

```csharp
builder.Configuration.AddAzureKeyVault(
    new Uri("https://contoso-vault.vault.azure.net/"),
    new DefaultAzureCredential());
// Key Vault secret "Dataverse--Authentication--ClientSecret" surfaces as
// configuration key "Dataverse:Authentication:ClientSecret".
```

For certificates, prefer `UseCertificate(...)` with the certificate retrieved from Key
Vault or the machine certificate store at startup — do not ship PFX files in deployment
artifacts.

## 4. What never to do

- Never commit secrets to `appsettings.json`, `appsettings.*.json`, `launchSettings.json`,
  docker-compose files, or test code. Treat any committed secret as leaked: revoke and
  rotate immediately — history rewrites do not un-leak it.
- Never use an `http://` environment URL. The SDK rejects it at validation
  ([threat model §2.1](threat-model.md)); do not proxy around this.
- Never point `EnvironmentUrl` at unvalidated user input — it decides where bearer tokens
  are sent.
- Never log configuration objects wholesale; see [data-protection.md](data-protection.md).
- Never share one app registration/secret across dev, test, and prod, and never reuse the
  SDK integration-test credential for anything else.
- Never grant the application user System Administrator "to make it work" (§7).

## 5. Sample `appsettings.json` shape (placeholders only)

Safe to commit — it contains no secret values. The `ClientSecret` key is shown only to
document the binding path; in real projects it is supplied by user-secrets, Key Vault, or
environment variables, and omitted from the committed file:

```jsonc
{
  "Dataverse": {
    "EnvironmentUrl": "https://<your-org>.crm.dynamics.com",
    "Authentication": {
      "TenantId": "<tenant-guid>",
      "ClientId": "<app-registration-client-id>"
      // "ClientSecret": supplied via user-secrets / Key Vault / env var
      //                 (Dataverse__Authentication__ClientSecret) — never committed
    },
    "Retry": {
      "MaxRetries": 3
    }
  }
}
```

Notes:

- Environment-variable form of a key uses `__`: `Dataverse__Authentication__ClientSecret`.
- The exact bindable option properties are defined by `DataverseClientOptions` /
  `DataverseAuthenticationOptions` / `DataverseRetryOptions`
  (see `docs/api/public-api-design.md` once written); this sample shows the shape, not an
  exhaustive schema.

## 6. HTTPS enforcement

- `EnvironmentUrl` must be `https://`; options validation rejects anything else at
  startup.
- The SDK never disables TLS certificate validation and exposes no option to do so. If a
  corporate proxy re-signs TLS, install its CA into the machine trust store — do not ask
  the SDK to ignore validation errors (it cannot).
- Redirects to a different host are not followed with credentials
  ([threat model §2.1](threat-model.md)).

## 7. Least-privilege application users in Dataverse

The identity the SDK authenticates as should be a Dataverse **application user** with a
**custom security role**, not a licensed human user and not an out-of-box admin role:

1. Create the app registration (or managed identity) in Entra ID.
2. In the Power Platform admin center, add it as an application user in the target
   environment.
3. Create a custom security role granting only what the integration needs:
   - Only the tables the app touches (e.g., `account`, `contact`), only the privileges it
     uses (Read/Create/Write — not Delete unless it deletes), at the narrowest workable
     scope (Business Unit rather than Organization where the data model allows).
   - `prvActOnBehalfOfAnotherUser` only when impersonation (KDV-013, v1.1) is actually
     adopted.
   - Solution export/import privileges only for ALM-focused identities — keep data
     integrations and ALM operations on **separate** application users so a leaked data
     credential cannot alter customizations.
4. Review role assignments whenever the integration's scope changes, and on a fixed
   cadence (at least annually).

The SDK's health check (`AddDataverseHealthCheck`, WhoAmI probe) needs no privilege
beyond a valid authenticated user, so least-privilege roles do not break it.

## 8. Secret rotation

- Client secrets: set short expiries (≤ 180 days), rotate via overlapping validity
  (create new, deploy, delete old). Because the SDK reads the secret through options at
  startup, rotation is a config change + restart/redeploy; no code change.
- Certificates: use Key Vault auto-rotation where possible.
- The SDK caches tokens, not credentials-derived state beyond that; a restarted process
  picks up rotated material with no residue.
