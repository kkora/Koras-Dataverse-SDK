# Configuration Reference: Environment Variables

## The SDK reads no environment variables

To be explicit: **`Koras.Dataverse` itself never reads environment variables** (and never
reads `IConfiguration`). Every value reaches the SDK through `DataverseClientOptions`, set by
*your* code. If an environment variable influences the SDK, it is because your code (or a
configuration provider you added, or Azure.Identity) read it and passed the value on.

Two indirect paths are worth knowing:

- The .NET host's default configuration maps environment variables into `IConfiguration`
  (e.g. `Dataverse__TenantId` → `Dataverse:TenantId`) — your binding code in the `AddDataverse`
  lambda then reads them.
- `UseDefault()` builds a `DefaultAzureCredential`, and **Azure.Identity** consults its own
  standard variables (`AZURE_TENANT_ID`, `AZURE_CLIENT_ID`, `AZURE_CLIENT_SECRET`, and
  related). That is Azure.Identity's documented behavior, not the SDK's.

## Recommended variable names for your applications

When you deliver secrets by environment variable, any names work; for consistency across
Koras-SDK-based services we suggest:

| Variable | Feeds | Notes |
|---|---|---|
| `DATAVERSE_ENVIRONMENT_URL` | `options.EnvironmentUrl` | Not a secret, but convenient to inject per environment |
| `DATAVERSE_TENANT_ID` | `UseClientSecret` / `UseCertificate` | |
| `DATAVERSE_CLIENT_ID` | `UseClientSecret` / `UseCertificate` | |
| `DATAVERSE_CLIENT_SECRET` | `UseClientSecret` | Secret — inject via your platform's secret mechanism, never bake into images |

```csharp
builder.Services.AddDataverse(options =>
{
    options.EnvironmentUrl = new Uri(Environment.GetEnvironmentVariable("DATAVERSE_ENVIRONMENT_URL")
        ?? throw new InvalidOperationException("DATAVERSE_ENVIRONMENT_URL is not set."));

    options.Authentication.UseClientSecret(
        Environment.GetEnvironmentVariable("DATAVERSE_TENANT_ID")!,
        Environment.GetEnvironmentVariable("DATAVERSE_CLIENT_ID")!,
        Environment.GetEnvironmentVariable("DATAVERSE_CLIENT_SECRET")!);
});
```

(With the double-underscore convention — `Dataverse__ClientSecret` — you can skip
`Environment.GetEnvironmentVariable` entirely and bind via `IConfiguration`; see
[appsettings](appsettings.md).)

Prefer `UseManagedIdentity()` where available: no secret variables at all.

## The SDK repository's integration-test variables

The SDK's own integration-test suite (`tests/Koras.Dataverse.IntegrationTests`) runs against a
live environment **only** when these variables are present, and skips otherwise (keeping CI
green without secrets):

| Variable | Meaning |
|---|---|
| `KORAS_DATAVERSE_URL` | Environment URL of the test environment |
| `KORAS_DATAVERSE_TENANT_ID` | Entra ID tenant id |
| `KORAS_DATAVERSE_CLIENT_ID` | App registration client id |
| `KORAS_DATAVERSE_CLIENT_SECRET` | App registration client secret |

These are read by the *test code*, not by the SDK. They are a convention worth copying for
your own gated live tests — see [testing recipes](../recipes/testing-recipes.md) and
[integration testing](../testing/integration-testing.md).
