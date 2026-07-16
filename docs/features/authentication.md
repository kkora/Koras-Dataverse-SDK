# Feature Planning — KDV-001 Authentication

> Planning-level design document (pre-implementation). Catalog entry:
> [`feature-catalog.md`](feature-catalog.md#kdv-001--authentication). Source of truth:
> [`docs/planning/master-plan.md`](../planning/master-plan.md) §3 (KDV-001), §4, §5, §7.
> Release classification: **MVP**.

## Overview

Every Dataverse Web API request requires an Entra ID bearer token for the scope
`{environmentUrl}/.default`. KDV-001 provides credential configuration through
`DataverseAuthenticationOptions`, a token-provider abstraction
(`IDataverseTokenProvider`), a default implementation adapting `Azure.Core.TokenCredential`,
and a thread-safe token cache with proactive refresh. Tokens are attached by an
`AuthenticationHandler` at the top of the HTTP pipeline (master plan §5).

## Requirements

**Functional**

1. Credential modes selectable via `DataverseAuthenticationOptions` helper methods:
   `UseClientSecret(tenantId, clientId, secret)`, `UseCertificate(...)`,
   `UseManagedIdentity()`, `UseInteractive()`, `UseDefault()` (`DefaultAzureCredential`), and
   `UseTokenCredential(cred)` for any custom `TokenCredential` (master plan §4).
2. Full escape hatch: consumers may replace token acquisition entirely by registering their own
   `IDataverseTokenProvider`.
3. Scope derivation: `{environmentUrl}/.default` from `DataverseClientOptions.EnvironmentUrl`.
4. Token caching: a token is reused until 5 minutes before expiry, then refreshed proactively;
   refresh is single-flight (concurrent callers await one acquisition) and thread-safe (master
   plan §5).
5. Named clients (KDV-010) may each carry independent credential configuration.
6. Cancellation: token acquisition honors the caller's `CancellationToken`.

**Nonfunctional.** Thread-safe under high concurrency; no token or secret material ever
logged or embedded in exception messages; expiry timing driven by injected `TimeProvider` for
deterministic tests (master plan §5).

## Proposed public API

Fixed by master plan §4:

```csharp
services.AddDataverse(o =>
{
    o.EnvironmentUrl = new Uri("https://contoso.crm.dynamics.com");
    o.Authentication.UseClientSecret(tenantId, clientId, secret);
    // or: UseManagedIdentity(), UseDefault(), UseCertificate(), UseInteractive(),
    //     UseTokenCredential(cred)
});
```

Namespace `Koras.Dataverse.Authentication`: `IDataverseTokenProvider` plus credential option
helpers. `DataverseAuthenticationOptions` lives with the client options in `Koras.Dataverse`.

Conservative proposal, subject to implementation:

```csharp
public interface IDataverseTokenProvider
{
    ValueTask<DataverseAccessToken> GetTokenAsync(CancellationToken cancellationToken = default);
}
```

where `DataverseAccessToken` is a small readonly record carrying the token value and expiry.
The exact shape (including whether `Azure.Core.AccessToken` is adapted internally instead of a
Koras record) is subject to implementation; the constraint that `Abstractions` contains no
third-party types (master plan §4) means `IDataverseTokenProvider` must not expose
`Azure.Core` types.

## Configuration

- `DataverseClientOptions.EnvironmentUrl` (required, HTTPS only) — also the scope source.
- `DataverseClientOptions.Authentication` — exactly one credential mode must be configured;
  configuring zero or more than one fails startup validation (KDV-010).
- Certificate mode accepts a certificate reference (thumbprint/store or `X509Certificate2`
  instance — exact members subject to implementation); raw key material is never accepted as a
  string.
- Refresh margin fixed at 5 minutes per master plan §5 (not configurable in MVP; keeping the
  knob count low is deliberate).

## Error conditions

| Condition | Behavior |
|---|---|
| No credential configured / multiple configured | `OptionsValidationException` at startup (KDV-010) with actionable message |
| Token acquisition fails (bad secret, revoked cert, MI unavailable) | `DataverseException` with authentication `DataverseErrorCategory`; inner exception preserved; message contains no secret material |
| Acquisition canceled | `OperationCanceledException` propagated unwrapped (master plan §5) |
| 401 from Dataverse despite token | Surfaced via KDV-009 mapping; one proactive re-acquisition attempt is a design option, subject to implementation |

## Security

- Secrets only via options bound from user-secret stores/environment configuration; never
  connection strings in code (master plan §7).
- Documentation ranks modes: managed identity > certificate > client secret; interactive is
  dev-only.
- Tokens never logged, never included in exceptions, never placed in activity tags.
- Non-HTTPS `EnvironmentUrl` rejected at validation, so tokens cannot be sent over plaintext.

## Performance

- Hot path is a cache read (no lock contention design goal: volatile read / lock-free fast
  path, subject to implementation).
- Single-flight refresh prevents acquisition stampedes at startup and expiry boundaries.
- Proactive refresh (5-minute margin) keeps acquisition latency off request paths in steady
  state.

## Observability

- Debug-level log events: token acquired (mode, expiry, correlation — no token content),
  refresh started/completed/failed.
- Failed acquisition logged once at warning with category; retry noise suppressed.
- No dedicated metrics in MVP (operation-level metrics in KDV-011 subsume this); an
  acquisition-failure counter is a candidate, subject to implementation.

## Test plan

**Unit** (fake `TimeProvider`, fake token source):
- Cache: token reused before refresh window; refreshed at window; expiry edge cases.
- Single-flight: N concurrent first calls → exactly one acquisition.
- Mode selection: each `Use*` helper resolves the correct credential path; conflict detection.
- Custom `IDataverseTokenProvider` replaces the default entirely.
- Cancellation during acquisition propagates `OperationCanceledException` unwrapped.
- Log assertions: no token substring appears in any log output.

**Integration** (env-var gated, master plan §6): WhoAmI succeeds using each credential type
available in CI (client secret at minimum); invalid secret produces classified auth failure.

## Acceptance criteria

1. All six credential modes are selectable and mutually exclusive, with startup validation
   failure on misconfiguration.
2. Steady-state operation performs zero token acquisitions between refresh windows.
3. Concurrent cold start performs exactly one acquisition.
4. A custom `IDataverseTokenProvider` receives every token request when registered.
5. No test or sample can produce token/secret material in logs (verified by log-capture
   assertions).
6. Integration WhoAmI passes with a real service principal.
