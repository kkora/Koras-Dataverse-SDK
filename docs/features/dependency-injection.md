# Feature Planning — KDV-010 Dependency Injection and Options

> Planning-level design document (pre-implementation). Catalog entry:
> [`feature-catalog.md`](feature-catalog.md#kdv-010--dependency-injection--options).
> Source of truth: [`docs/planning/master-plan.md`](../planning/master-plan.md) §2, §3
> (KDV-010), §4, §5, §7. Release classification: **MVP**.

## Overview

KDV-010 is the SDK's front door: a single `services.AddDataverse(...)` call registers the
client stack over `IHttpClientFactory`, binds `DataverseClientOptions` via the options
pattern, validates configuration at startup (DataAnnotations plus custom rules), and supports
named clients for multi-environment scenarios through `IDataverseClientFactory`. DI
registration lives in the main `Koras.Dataverse` package — no separate `.DependencyInjection`
package (ADR-0003, master plan §2).

## Requirements

**Functional**

1. `AddDataverse(Action<DataverseClientOptions>)` extension in the
   `Microsoft.Extensions.DependencyInjection` namespace (master plan §4), registering:
   `IDataverseClient`, `IMetadataClient`, `ISolutionClient`, `IDataverseTokenProvider`
   (default implementation), and the HTTP pipeline.
2. Options pattern: `DataverseClientOptions` with `EnvironmentUrl`, `Authentication`
   (`DataverseAuthenticationOptions`), and `Retry` (`DataverseRetryOptions`); bindable from
   code and from `IConfiguration` (binding specifics subject to implementation).
3. Startup validation: DataAnnotations plus custom validators (master plan §3) —
   `ValidateOnStart` semantics so misconfiguration fails at boot, not first call.
4. Named clients: multiple registrations targeting different environments; HTTP resources per
   name via `IHttpClientFactory` named client `"Koras.Dataverse:{name}"` (master plan §5).
5. `IDataverseClientFactory` resolves clients by name; the default (unnamed) registration
   also injects `IDataverseClient` directly (proposed convention, subject to implementation).
6. Client lifetimes: all public client types thread-safe and registered as singletons
   (master plan §5).

**Nonfunctional.** Registration is idempotent/additive-safe (calling twice with different
names composes; duplicate same-name registration behavior defined and tested); no service
locator patterns; trimming/AOT friendliness evaluated during implementation.

## Proposed public API

Fixed by master plan §4:

```csharp
services.AddDataverse(o =>
{
    o.EnvironmentUrl = new Uri("https://contoso.crm.dynamics.com");
    o.Authentication.UseClientSecret(tenantId, clientId, secret);
});
```

Types: `AddDataverse`, `AddDataverseHealthCheck`, `IDataverseClientFactory` in
`Microsoft.Extensions.DependencyInjection`; `DataverseClientOptions`,
`DataverseAuthenticationOptions`, `DataverseRetryOptions` in `Koras.Dataverse`.

Conservative proposal for named clients, subject to implementation:

```csharp
services.AddDataverse("prod", o => { /* ... */ });
services.AddDataverse("sandbox", o => { /* ... */ });

public sealed class SyncJob(IDataverseClientFactory factory)
{
    public Task RunAsync(CancellationToken ct)
    {
        IDataverseClient prod = factory.CreateClient("prod");
        // ...
        return Task.CompletedTask;
    }
}
```

## Configuration

`DataverseClientOptions` (validated members; list conservative, subject to implementation):

| Option | Rule |
|---|---|
| `EnvironmentUrl` | Required; absolute URI; **HTTPS only** (non-HTTPS rejected, master plan §7) |
| `Authentication` | Exactly one credential mode configured (KDV-001) |
| `Retry` | Bounds validated per KDV-008 |

Validation failures throw `OptionsValidationException` at startup listing **all** violations
with actionable messages, not just the first.

## Error conditions

| Condition | Behavior |
|---|---|
| Missing/relative/non-HTTPS `EnvironmentUrl` | Startup validation failure |
| No or ambiguous credential configuration | Startup validation failure (KDV-001) |
| `IDataverseClientFactory.CreateClient("unknown")` | Immediate exception naming the missing registration and listing known names (proposed) |
| Duplicate same-name registration | Defined behavior (last-wins or throw) decided in implementation, documented, and tested |
| Options mutated after startup | Not supported in MVP (singleton clients); documented |

## Security

- Non-HTTPS environment URLs rejected — tokens can never be configured onto a plaintext
  channel (master plan §7).
- Options objects can carry secrets: they are never logged wholesale; `ToString()` on options
  types must not reveal secret members.
- Configuration binding docs steer secrets to user-secret stores/environment/key-vault
  providers, never appsettings in source control.

## Performance

- Singleton clients over `IHttpClientFactory`-managed handlers: connection pooling, handler
  rotation, no per-request client construction.
- Startup validation cost is boot-time only.
- Factory lookups are dictionary reads; no per-call service-provider walks on hot paths
  (design goal, subject to implementation).

## Observability

- The client name (`"Koras.Dataverse:{name}"` convention) is attached as a tag/log scope to
  all telemetry so multi-environment traffic is separable (KDV-011).
- Startup logs registration summary (names, environment hosts — no credentials) at debug.

## Test plan

**Unit** (master plan §6 lists DI registration and options validation explicitly):
- Registration: all expected services resolvable; lifetimes are singleton; pipeline handler
  order (`AuthenticationHandler` → `RetryHandler`) asserted.
- Validation matrix: each rule violation fails startup with the documented message; valid
  configuration boots.
- Named clients: two names resolve distinct clients hitting distinct base addresses (fake
  handler asserted); factory unknown-name behavior; duplicate-registration behavior.
- Configuration binding: options bound from an in-memory `IConfiguration`.
- Options isolation: named options do not bleed into each other.

**Integration** (env-var gated): a generic host boots with configuration-bound options and
executes WhoAmI; a deliberately broken configuration fails at startup, not first request.

## Acceptance criteria

1. The master plan §4 registration sample compiles and runs unmodified.
2. Single-call registration works in the console, minimal API, and worker samples.
3. Every documented misconfiguration is caught at startup with an actionable message.
4. Two named clients operate against two environments independently in the same host.
5. Handler pipeline order and singleton lifetimes verified by tests.
6. No separate DI package exists; the extension lives in
   `Microsoft.Extensions.DependencyInjection` namespace within `Koras.Dataverse` (ADR-0003).
