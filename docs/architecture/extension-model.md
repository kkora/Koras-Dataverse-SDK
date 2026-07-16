# Extension Model

> Elaborates §4 and §5 of [`docs/planning/master-plan.md`](../planning/master-plan.md). If this
> document and the master plan disagree, the master plan wins.

The SDK is deliberately closed where correctness matters (error mapping, encoding, protocol
details) and open at a small set of documented seams. Anything not listed here is an
implementation detail and may change in any release.

## 1. Custom authentication: `IDataverseTokenProvider`

**Package:** `Koras.Dataverse.Abstractions` (namespace `Koras.Dataverse.Authentication`).

The authentication seam is a single dependency-free interface (ADR-0004): given the resource
scope and a cancellation token, return a bearer token and its expiry. The default
implementation (in the core package) adapts `Azure.Core.TokenCredential` — client secret,
certificate, managed identity, interactive, `DefaultAzureCredential`, or a caller-supplied
credential — with scope `{environmentUrl}/.default` and cached, single-flight refresh until
5 minutes before expiry.

Use a custom provider when Azure.Identity does not fit: pre-issued tokens in tests, custom STS
integrations, token brokering across processes, or sovereign-cloud flows not covered by
`EnvironmentUrl`-derived scopes.

```csharp
services.AddDataverse(o =>
{
    o.EnvironmentUrl = new Uri("https://contoso.crm.dynamics.com");
    o.Authentication.UseTokenProvider<MyStsTokenProvider>();
    // or: o.Authentication.UseTokenCredential(myCredential);
});
```

Contract expectations for implementers:

- Must be thread-safe; the SDK registers it for singleton-scoped use and may call it
  concurrently.
- Must honor the `CancellationToken`.
- Should cache internally; the SDK does not add a second cache in front of a custom provider.
- Must never log or otherwise persist the token material.

The exact member signatures of `IDataverseTokenProvider` and the `UseTokenProvider`
registration overloads are subject to implementation review; the seam itself
(interface in Abstractions, default `TokenCredential` adapter in core) is fixed by ADR-0004.

## 2. Custom `DelegatingHandler`s via the `AddDataverse` builder

**Package:** `Koras.Dataverse` (DI layer).

The `AddDataverse` registration exposes a builder hook for appending user
`DelegatingHandler`s to the named client's pipeline. User handlers run **after** the SDK's
`AuthenticationHandler` and `RetryHandler` and before the primary handler:

```text
AuthenticationHandler → RetryHandler → user handler(s) → primary handler → network
```

Consequences of that position:

- User handlers see the authenticated request (headers already attached).
- User handlers run once **per attempt** — a retried request passes through them again. Do not
  implement retry logic in a user handler; tune `DataverseRetryOptions` instead (ADR-0007).
- Handlers must be safe for reuse across requests, per standard `IHttpClientFactory` rules.

Typical uses: corporate proxy/header injection, request/response auditing, chaos testing.
The exact builder method shape (e.g., a callback exposing the underlying
`IHttpClientBuilder`) is subject to implementation review.

## 3. Multi-environment: named clients and `IDataverseClientFactory`

**Interface package:** `Koras.Dataverse.Abstractions`; implementation and registration in
`Koras.Dataverse` (KDV-010).

`AddDataverse` supports named registrations, each with its own options (environment URL,
credentials, retry settings) and its own named `HttpClient` (`"Koras.Dataverse:{name}"`).
`IDataverseClientFactory` resolves a client by name at runtime:

```csharp
services.AddDataverse("crm-prod", o => { /* prod options */ });
services.AddDataverse("crm-sandbox", o => { /* sandbox options */ });

public sealed class SyncJob(IDataverseClientFactory factory)
{
    public async Task RunAsync(CancellationToken ct)
    {
        IDataverseClient prod = factory.CreateClient("crm-prod");
        IDataverseClient sandbox = factory.CreateClient("crm-sandbox");
        // ...
    }
}
```

The unnamed `AddDataverse(...)` overload registers the default client, which is also what a
bare `IDataverseClient` constructor dependency receives. Clients returned by the factory are
singletons per name; disposing them is not the caller's responsibility.

## 4. Future transport abstraction: OrganizationService package

**Planned for v1.1** (KDV-015, ADR-0001). The `Abstractions` interfaces are the transport
abstraction: they contain no HTTP types, so an alternative transport can implement
`IDataverseClient` (and the sub-clients) over `Microsoft.PowerPlatform.Dataverse.Client` for
organizations that mandate `IOrganizationService` semantics.

Design guardrails kept in place today so this remains possible:

- No `HttpRequestMessage`/`HttpResponseMessage` or handler types on any `Abstractions`
  signature.
- Error model expressed in transport-neutral terms (`DataverseErrorCategory`, integer HTTP
  status as *data*, not as a type dependency).
- Options types split so transport-specific settings (retry pipeline shape) do not leak into
  the shared contract in ways an alternative transport could not honor.

The adapter's own registration story (e.g., a parallel `AddDataverseOrganizationService`
entry point) is subject to implementation review at v1.1 and will get its own ADR if it
deviates from the model described here.

## 5. Options-based configuration of retry and timeout

**Package:** `Koras.Dataverse.Abstractions` (types), bound and validated in `Koras.Dataverse`.

Resilience is configured, not replaced (ADR-0007). `DataverseRetryOptions` (nested under
`DataverseClientOptions`) exposes the tuning surface — maximum retry attempts, base delay /
backoff shape, maximum per-attempt and overall delay, and whether to honor `Retry-After`
beyond the backoff cap — plus the per-request timeout on the client options. Defaults follow
Dataverse service-protection guidance (retry 429/503/504, always honor `Retry-After`,
jittered exponential backoff). Exact property names and default values are subject to
implementation review and are documented as public contract once shipped
(see [`../api/backward-compatibility.md`](../api/backward-compatibility.md)).

```csharp
services.AddDataverse(o =>
{
    o.EnvironmentUrl = new Uri("https://contoso.crm.dynamics.com");
    o.Authentication.UseManagedIdentity();
    o.Retry.MaxRetries = 5;          // names illustrative — subject to implementation review
    o.Timeout = TimeSpan.FromSeconds(100);
});
```

Options are validated with DataAnnotations at startup (KDV-010); invalid combinations fail
fast rather than at first request.

## 6. What is deliberately not extensible

- **Error mapping.** The HTTP-status/Dataverse-code → `DataverseErrorCategory` mapping is
  fixed (see [`error-model.md`](error-model.md)); consistent taxonomy is a core product
  promise (KDV-009).
- **Retry policy engine.** Tunable via options, not pluggable via interfaces (ADR-0007). Users
  who need a fully custom policy can disable the built-in retries via options and wrap calls
  in their own resilience pipeline at the call site.
- **Serialization.** The Web API payload handling is internal; the plain CLR value model
  (ADR-0005) is the contract, not the wire format.
- **Telemetry names.** `ActivitySource`/`Meter` names and instrument names are fixed contract
  (see [`observability.md`](observability.md)); enrichment happens in the consumer's
  OpenTelemetry pipeline, not through SDK hooks.
