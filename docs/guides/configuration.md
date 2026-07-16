# Guide: Production Configuration

Hardening the SDK's configuration for production: credential choices, secrets, retry tuning,
and timeouts. The condensed everything-in-one-file version is
[recipes/production-configuration.md](../recipes/production-configuration.md).

## Credentials, in order of preference

### 1. Managed identity (Azure hosting)

No secrets to store, rotate, or leak:

```csharp
builder.Services.AddDataverse(options =>
{
    options.EnvironmentUrl = new Uri(builder.Configuration["Dataverse:EnvironmentUrl"]!);
    options.Authentication.UseManagedIdentity();
    // user-assigned identity: UseManagedIdentity("<identity-client-id>")
});
```

Setup outside the code: create the managed identity, register it as an **application user** in
the Dataverse environment (Power Platform admin center → environment → app users), assign a
security role.

### 2. Certificate (non-Azure hosting, CI)

Preferred over client secrets — not a shared string, rotatable via your PKI, and loadable from
Key Vault or a machine store:

```csharp
using System.Security.Cryptography.X509Certificates;

using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
store.Open(OpenFlags.ReadOnly);
X509Certificate2 certificate = store.Certificates
    .Find(X509FindType.FindByThumbprint, builder.Configuration["Dataverse:CertThumbprint"]!, validOnly: true)
    .Single();

builder.Services.AddDataverse(options =>
{
    options.EnvironmentUrl = new Uri(builder.Configuration["Dataverse:EnvironmentUrl"]!);
    options.Authentication.UseCertificate(
        builder.Configuration["Dataverse:TenantId"]!,
        builder.Configuration["Dataverse:ClientId"]!,
        certificate);
});
```

### 3. Client secret with Key Vault-backed configuration

When a secret is unavoidable, keep it out of appsettings and pull it through the Key Vault
configuration provider (the SDK just reads `IConfiguration` — it neither knows nor cares that
Key Vault is behind it):

```csharp
builder.Configuration.AddAzureKeyVault(
    new Uri("https://contoso-kv.vault.azure.net/"),
    new Azure.Identity.DefaultAzureCredential());

builder.Services.AddDataverse(options =>
{
    options.EnvironmentUrl = new Uri(builder.Configuration["Dataverse:EnvironmentUrl"]!);
    options.Authentication.UseClientSecret(
        builder.Configuration["Dataverse:TenantId"]!,
        builder.Configuration["Dataverse:ClientId"]!,
        builder.Configuration["Dataverse-ClientSecret"]!); // Key Vault secret name
});
```

(Requires the `Azure.Extensions.AspNetCore.Configuration.Secrets` package.)

Avoid `UseInteractive` in servers (it opens a browser) and be deliberate about `UseDefault` in
production — `DefaultAzureCredential`'s chain probing adds startup latency and can silently
pick a developer credential; prefer the explicit `UseManagedIdentity`.

## Retry tuning

Defaults: 3 retries, 1 s base, 30 s cap, `Retry-After` honored. Tune by workload:

```csharp
// Interactive web APIs: fail fast, let the caller retry.
options.Retry.MaxRetries = 2;
options.Retry.MaxDelay = TimeSpan.FromSeconds(5);

// Background/bulk workloads: absorb service-protection pushback.
options.Retry.MaxRetries = 5;
options.Retry.MaxDelay = TimeSpan.FromMinutes(1);
```

Keep `RespectRetryAfter = true` in production — Dataverse's own hint beats any local backoff
guess, and a *longer* server hint deliberately overrides `MaxDelay`. Remember the retry budget
lives inside `options.Timeout`: retries stop when the operation's total budget runs out,
whichever comes first.

## Timeouts

`options.Timeout` is the per-operation budget **including all retries** (default 100 s).

- **Interactive APIs**: lower it (15–30 s) so a struggling environment degrades your API
  quickly and visibly instead of pinning request threads.
- **Solution operations** (`Solutions.ExportAsync`/`ImportAsync`/`PublishAllAsync`) routinely
  run for minutes. Register a dedicated named client for solution work instead of inflating the
  budget for everything:

```csharp
builder.Services.AddDataverse(options =>          // default: data operations
{
    options.EnvironmentUrl = environmentUrl;
    options.Authentication.UseManagedIdentity();
    options.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddDataverse("solutions", options =>  // ALM operations
{
    options.EnvironmentUrl = environmentUrl;
    options.Authentication.UseManagedIdentity();
    options.Timeout = TimeSpan.FromMinutes(15);
});
```

Both clients hit the same environment; only the budget differs. Resolve the second via
`factory.GetClient("solutions")`.

## Payload trimming

`options.IncludeAnnotations = false` on hot paths shrinks responses: full display annotations
(including lookup logical names and display names) are skipped, so lookup columns come back as
plain `Guid` values instead of `EntityReference` instances, while formatted values remain
available. Leave it `true` (default) unless a profiler tells you otherwise, and prefer a
dedicated named client if only some workloads need trimming.

## Completing the production picture

- [Health checks](health-checks.md) — readiness probe on the Dataverse dependency
- [Telemetry](telemetry.md) — OTel traces/metrics; alert on throttles and retries
- [Logging](logging.md) — categories and recommended production levels
- [Validation](../configuration/validation.md) — everything fails at startup, not at 3 a.m.
