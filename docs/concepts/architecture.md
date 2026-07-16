# Architecture

A condensed view of the SDK's architecture. The full design document — module
responsibilities, threading/async/cancellation models, and diagrams — is
[docs/architecture/overview.md](../architecture/overview.md).

## Layers

```text
Consumer application
        │ programs against interfaces
        ▼
Koras.Dataverse.Abstractions        IDataverseClient, Entity, error model, query/batch models
        ▲ implemented by
        │
Koras.Dataverse (core client)       CRUD, query execution, $batch, paging, metadata,
        │                           solutions, error mapping, telemetry, DI, health check
        ▼
HTTP pipeline (IHttpClientFactory)  RetryHandler → AuthenticationHandler → (user handlers)
        ▼
Dataverse Web API v9.2              {environmentUrl}/api/data/v9.2/
```

Key placement decisions:

- **Telemetry lives in the client layer, not a handler** — one span/metric per operation, with
  retries inside it.
- **Retry wraps authentication** — each attempt gets a valid token; the token cache lives in the
  token provider, outside handler rotation, so `IHttpClientFactory` recycling never discards
  cached tokens.
- **The operation timeout is enforced by the client** via a linked `CancellationTokenSource`,
  covering all retries; `HttpClient.Timeout` is set to infinite.

## Package dependency direction

```text
Koras.Dataverse.OpenTelemetry ──► Koras.Dataverse ──► Koras.Dataverse.Abstractions ──► Koras.Dataverse.FetchXml
```

- `Koras.Dataverse.FetchXml` has **zero** dependencies (and targets netstandard2.0).
- `Koras.Dataverse.Abstractions` depends only on `FetchXml` (so
  `IDataverseClient.FetchAsync(FetchXmlQuery)` is strongly typed) — no third-party packages.
- `Koras.Dataverse` carries the implementation dependencies: Azure.Identity and the
  `Microsoft.Extensions.*` family. DI registration ships here (no separate `.DependencyInjection`
  package).
- Nothing depends on the implementation package. Libraries and test projects reference
  `Abstractions` only and stay lightweight and mockable.

## Further reading

- [Architecture overview (full)](../architecture/overview.md)
- [Package boundaries](../architecture/package-boundaries.md) and
  [dependency rules](../architecture/dependency-rules.md)
- [Error model design](../architecture/error-model.md)
- [Observability design](../architecture/observability.md)
- [Decision records](../architecture/decision-records/)
