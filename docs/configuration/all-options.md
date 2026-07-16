# Configuration Reference: All Options

Complete reference for `DataverseClientOptions` and its nested option types. Set these in the
`AddDataverse` lambda or on the instance passed to `DataverseClient.Create`. Validation rules
are detailed in [validation](validation.md).

## DataverseClientOptions (`Koras.Dataverse`)

| Option | Type | Default | Effect |
|---|---|---|---|
| `EnvironmentUrl` | `Uri?` | `null` (**required**) | Base URL of the environment, e.g. `https://contoso.crm.dynamics.com`. Must be absolute HTTPS; no `/api/data` suffix. Also determines the token scope (`{authority}/.default`). |
| `ApiVersion` | `string` | `"v9.2"` | Web API version segment; requests go to `{EnvironmentUrl}/api/data/{ApiVersion}/`. Must start with `v`. |
| `Timeout` | `TimeSpan` | 100 seconds | Per-operation time budget covering the initial attempt **and all retries**. Expiry throws `DataverseException` with category `Timeout` (`IsTransient = true`). Raise for solution import/export. |
| `IncludeAnnotations` | `bool` | `true` | Whether responses request full display annotations (`odata.include-annotations="*"`): formatted values, lookup display names, and lookup logical names (needed to materialize lookups as `EntityReference`). When `false`, only formatted values are requested — smaller payloads; lookup columns read back as plain `Guid`s. |
| `Authentication` | `DataverseAuthenticationOptions` | `UseDefault()` behavior | Authentication configuration; get-only — call one of the `Use…` methods below. |
| `Retry` | `DataverseRetryOptions` | see below | Retry/throttling behavior; get-only — set its properties. |
| `EntitySetNameOverrides` | `IDictionary<string, string>` | empty | Entity set names for tables whose Web API set name doesn't follow standard pluralization, keyed by table logical name (e.g. `["new_metadata"] = "new_metadataset"`). Get-only dictionary — add entries. |

## DataverseRetryOptions (`options.Retry`)

| Option | Type | Default | Effect |
|---|---|---|---|
| `MaxRetries` | `int` | `3` | Retry attempts after the initial try, for HTTP 429/502/503/504 and network errors. `0` disables retries. Valid range 0–10. |
| `BaseDelay` | `TimeSpan` | 1 second | Base of the exponential backoff (`BaseDelay × 2^attempt`, plus up to 25 % jitter). Must be positive. |
| `MaxDelay` | `TimeSpan` | 30 seconds | Cap for a single computed backoff delay. Must be ≥ `BaseDelay`. A **longer server-provided `Retry-After` still wins** over this cap. |
| `RespectRetryAfter` | `bool` | `true` | Honor the server's `Retry-After` header (delta or date form) instead of computed backoff. Keep `true` in production. |

All retrying happens within `Timeout`; whichever budget runs out first ends the operation.

## DataverseAuthenticationOptions (`options.Authentication`)

Selection methods — call exactly one; **the last call wins** and clears previous settings:

| Method | Resulting `Kind` | Requires | Use for |
|---|---|---|---|
| `UseDefault()` | `Default` | — | `DefaultAzureCredential` chain: environment variables, workload identity, managed identity, Azure CLI, and more. The default when no method is called. |
| `UseClientSecret(tenantId, clientId, clientSecret)` | `ClientSecret` | all three non-blank | Entra ID app registration with a secret. |
| `UseCertificate(tenantId, clientId, certificate)` | `Certificate` | ids non-blank + `X509Certificate2` | Entra ID app registration with a certificate — preferred over secrets. |
| `UseManagedIdentity(clientId = null)` | `ManagedIdentity` | — | Azure managed identity; pass a client id for a user-assigned identity, `null` for system-assigned. |
| `UseInteractive(tenantId = null, clientId = null)` | `Interactive` | — | Browser sign-in. Development and admin tooling only. |
| `UseTokenCredential(credential)` | `TokenCredential` | non-null `Azure.Core.TokenCredential` | Any custom credential; SDK still provides token caching and single-flight refresh. |
| `UseTokenProvider(provider)` | `TokenProvider` | non-null `IDataverseTokenProvider` | Fully custom token source; no Azure.Identity involvement. Your provider owns caching. |

Read-only state (populated by the `Use…` methods): `Kind`, `TenantId`, `ClientId`,
`ClientSecret`, `Certificate`, `ManagedIdentityClientId`, `Credential`, `TokenProvider`.

Token behavior for every kind except `TokenProvider`: scope `{EnvironmentUrl
authority}/.default`, cached, refreshed single-flight five minutes before expiry.

## Non-configurable behavior worth knowing

- Retryable statuses (429, 502, 503, 504) and the retry/authentication handler order are fixed.
- The OData protocol headers (`OData-Version: 4.0`) and strict-update semantics
  (`If-Match: *` on `UpdateAsync`) are fixed.
- The SDK reads **no environment variables and no IConfiguration** itself — see
  [environment variables](environment-variables.md).

## Related

- [Getting started: configuration](../getting-started/configuration.md) — walkthrough with binding
- [appsettings patterns](appsettings.md) · [validation](validation.md)
