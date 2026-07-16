# Configuration

All client behavior is controlled by `DataverseClientOptions`, configured in the `AddDataverse`
lambda (or passed to `DataverseClient.Create`). The complete reference table is in
[All options](../configuration/all-options.md); this page walks through the options and shows
the recommended `appsettings.json` + user-secrets pattern.

## DataverseClientOptions walkthrough

```csharp
builder.Services.AddDataverse(options =>
{
    // Required. Absolute HTTPS URL of the environment, no /api/data suffix.
    options.EnvironmentUrl = new Uri("https://contoso.crm.dynamics.com");

    // Web API version segment. Default "v9.2" — leave it unless you have a reason not to.
    options.ApiVersion = "v9.2";

    // Per-operation time budget covering *all* retry attempts. Default 100 seconds.
    // Raise it for solution import/export; expiry throws DataverseException (Timeout).
    options.Timeout = TimeSpan.FromSeconds(100);

    // Whether responses include display annotations (formatted values, lookup names).
    // Default true; disable to shrink payloads on hot paths.
    options.IncludeAnnotations = true;

    // Retry and throttling behavior.
    options.Retry.MaxRetries = 3;                          // 0 disables retries (0–10)
    options.Retry.BaseDelay = TimeSpan.FromSeconds(1);     // exponential backoff base
    options.Retry.MaxDelay = TimeSpan.FromSeconds(30);     // cap per delay; server Retry-After still wins
    options.Retry.RespectRetryAfter = true;                // honor the server's Retry-After hint

    // Entity set name overrides for tables whose Web API set name does not follow
    // Dataverse's standard pluralization.
    options.EntitySetNameOverrides["new_metadata"] = "new_metadataset";

    // Authentication: call exactly one Use… method (the last call wins).
    options.Authentication.UseDefault();
});
```

Authentication choices (`options.Authentication`):

| Method | Use when |
|---|---|
| `UseDefault()` | Local development and Azure hosting via `DefaultAzureCredential` (default when nothing is called) |
| `UseClientSecret(tenantId, clientId, secret)` | App registration with a secret |
| `UseCertificate(tenantId, clientId, certificate)` | App registration with a certificate (preferred over secrets) |
| `UseManagedIdentity(clientId?)` | Azure hosting; pass a client id for a user-assigned identity |
| `UseInteractive(tenantId?, clientId?)` | Developer/admin tooling with browser sign-in |
| `UseTokenCredential(credential)` | Any custom `Azure.Core.TokenCredential` |
| `UseTokenProvider(provider)` | Your own `IDataverseTokenProvider` — no Azure.Identity involvement |

## Binding from appsettings.json + user-secrets

The SDK does not read configuration or environment variables itself. You pull values from
`IConfiguration` inside the `AddDataverse` lambda, which keeps binding explicit and gives you a
single place where configuration meets the SDK:

`appsettings.json` (non-secret values only):

```json
{
  "Dataverse": {
    "EnvironmentUrl": "https://contoso.crm.dynamics.com",
    "TenantId": "11111111-1111-1111-1111-111111111111",
    "ClientId": "22222222-2222-2222-2222-222222222222",
    "Timeout": "00:01:40",
    "Retry": {
      "MaxRetries": 4
    }
  }
}
```

Secret via user-secrets in development:

```bash
dotnet user-secrets set "Dataverse:ClientSecret" "<secret>"
```

`Program.cs`:

```csharp
IConfigurationSection dataverse = builder.Configuration.GetSection("Dataverse");

builder.Services.AddDataverse(options =>
{
    options.EnvironmentUrl = new Uri(dataverse["EnvironmentUrl"]!);
    options.Timeout = dataverse.GetValue("Timeout", TimeSpan.FromSeconds(100));
    options.Retry.MaxRetries = dataverse.GetValue("Retry:MaxRetries", 3);

    options.Authentication.UseClientSecret(
        dataverse["TenantId"]!,
        dataverse["ClientId"]!,
        dataverse["ClientSecret"]!); // user-secrets in dev, Key Vault/env in prod
});
```

In production, supply `Dataverse:ClientSecret` through your host's secret mechanism (Key Vault
configuration provider, container secrets, environment variables surfaced as configuration) —
or avoid the secret entirely with `UseManagedIdentity()`. See
[appsettings guidance](../configuration/appsettings.md) and the
[production configuration guide](../guides/configuration.md).

## Validation at startup

`AddDataverse` registers the options with `ValidateOnStart()`. When the host starts, every
registered client's options are validated; failures throw an `OptionsValidationException`
before the application serves traffic, with messages like:

```text
Dataverse client 'Default' configuration is invalid: DataverseClientOptions.EnvironmentUrl is
required (for example https://contoso.crm.dynamics.com).
```

What is enforced (details in [validation reference](../configuration/validation.md)):

- `EnvironmentUrl` is set, absolute, and HTTPS,
- `ApiVersion` looks like `v9.2`,
- `Timeout` is positive,
- `Retry.MaxRetries` is 0–10; delays are positive and `MaxDelay >= BaseDelay`,
- the selected authentication kind has all its required values (for example, client-secret
  auth requires tenant id, client id, and secret).

`DataverseClient.Create` runs the same validation immediately, so misconfiguration in non-DI
usage fails at construction, not on the first request.
