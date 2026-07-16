# Architecture Overview

> Elaborates §5 of [`docs/planning/master-plan.md`](../planning/master-plan.md). If this
> document and the master plan disagree, the master plan wins.

This document describes the layered architecture of the Koras Dataverse SDK, the
responsibilities of each module, and the cross-cutting models for threading, asynchrony,
cancellation, and time. It is a pre-implementation design document: it describes the intended
shape of the SDK, not code that already exists.

## 1. Layered view

The SDK sits between a consumer application and the Dataverse Web API v9.2. Each layer depends
only on the layer directly beneath it.

```text
┌──────────────────────────────────────────────────────────────┐
│ Consumer application (console, minimal API, worker, tests)  │
├──────────────────────────────────────────────────────────────┤
│ DI / options layer                                           │
│   AddDataverse, AddDataverseHealthCheck, named clients,      │
│   DataverseClientOptions (+ DataAnnotations validation)      │
├──────────────────────────────────────────────────────────────┤
│ Abstractions (Koras.Dataverse.Abstractions)                  │
│   IDataverseClient, IMetadataClient, ISolutionClient,        │
│   IDataverseTokenProvider, IDataverseClientFactory,          │
│   Entity, EntityReference, query/batch/metadata models,      │
│   error model, options                                       │
├──────────────────────────────────────────────────────────────┤
│ Core client (Koras.Dataverse)                                │
│   CRUD, OData query execution, FetchXML execution, $batch,   │
│   paging, metadata client, solution client, error mapping,   │
│   telemetry (ActivitySource/Meter), logging, health check    │
├──────────────────────────────────────────────────────────────┤
│ HTTP pipeline (per named client, via IHttpClientFactory)     │
│   AuthenticationHandler → RetryHandler → (user handlers)     │
│   → HttpClient primary handler → network                     │
├──────────────────────────────────────────────────────────────┤
│ Microsoft Dataverse Web API v9.2                             │
│   {environmentUrl}/api/data/v9.2/                            │
└──────────────────────────────────────────────────────────────┘
```

Consumers program against the interfaces in `Koras.Dataverse.Abstractions` only. The core
package is the sole implementation in the MVP; a later `Koras.Dataverse.OrganizationService`
package (v1.1, ADR-0001) may provide an alternative transport behind the same abstractions.

The standalone `Koras.Dataverse.FetchXml` builder package sits beside this stack: it produces
FetchXML strings/queries and has no knowledge of HTTP, authentication, or the client. The core
client consumes its output when executing FetchXML queries.

## 2. Module responsibilities

### 2.1 DI / options layer

- **Entry points:** `AddDataverse` and `AddDataverseHealthCheck` extension methods in the
  `Microsoft.Extensions.DependencyInjection` namespace (shipped in the main `Koras.Dataverse`
  package — see ADR-0003).
- **Responsibilities:** bind and validate `DataverseClientOptions` (options pattern with
  DataAnnotations validation and validate-on-start), register the named `HttpClient`
  (`"Koras.Dataverse:{name}"`) with the handler pipeline, register `IDataverseClient` and
  sub-clients as singletons, register the default token provider, register
  `IDataverseClientFactory` for multi-environment scenarios (KDV-010), and register the
  `WhoAmI`-based health check (KDV-012).
- **Not responsible for:** any Dataverse protocol knowledge. All Web API behavior lives in the
  core client and handlers.

### 2.2 Abstractions (`Koras.Dataverse.Abstractions`)

- **Responsibilities:** define the complete injectable surface (`IDataverseClient`,
  `IMetadataClient`, `ISolutionClient`, `IDataverseTokenProvider`, `IDataverseClientFactory`),
  the data model (`Entity`, `EntityReference`, query/batch/metadata models), the error model
  (`DataverseException`, `DataverseError`, `DataverseErrorCategory`), and the options types.
- **Constraints:** zero dependencies, no HTTP types, no Azure types, no serialization types in
  the public surface. This is what makes consumer code and tests mockable without pulling in
  the implementation (see `package-boundaries.md`).

### 2.3 Core client (`Koras.Dataverse`)

- **Responsibilities:**
  - Compose Web API requests (URLs, OData headers, `@odata.bind` translation per ADR-0005,
    alternate-key addressing, `$batch` multipart payloads) and parse responses into the plain
    CLR value model.
  - Execute OData and FetchXML queries, including `IAsyncEnumerable` auto-paging (`@odata.nextLink`
    for OData, paging cookies for FetchXML).
  - Map non-success responses to `DataverseError`/`DataverseException` (see `error-model.md`).
  - Emit telemetry: `ActivitySource`/`Meter` named `"Koras.Dataverse"` and structured logging
    under the `Koras.Dataverse` / `Koras.Dataverse.Http` categories (see `observability.md`).
    Activities are started in the client layer — not in a handler — so a single
    `dataverse.execute` span wraps all retry attempts of one logical operation.
  - Host the sub-clients: the metadata client (KDV-006) and solution client (KDV-007) are thin
    facades that reuse the same HTTP pipeline, error mapping, and telemetry as the main client.
    They are not separate HTTP stacks; they share the named `HttpClient`, token provider, and
    options of the client they belong to.
- **Not responsible for:** authentication token acquisition (delegated to
  `IDataverseTokenProvider`) or transport-level retries (delegated to the retry handler).

### 2.4 HTTP pipeline

Built per named client through `IHttpClientFactory`, in this order (outermost first):

1. **AuthenticationHandler** — obtains a token from `IDataverseTokenProvider` and attaches the
   `Authorization: Bearer` header. The default provider adapts an `Azure.Core.TokenCredential`
   with scope `{environmentUrl}/.default`, caching tokens until 5 minutes before expiry with
   thread-safe, single-flight refresh (ADR-0004). Because the handler runs above the retry
   handler, a retried attempt reuses the cached token or picks up a refreshed one.
2. **RetryHandler** — service-protection-limit-aware retry (KDV-008, ADR-0007): retries 429,
   503, and 504, always honoring `Retry-After` when present, otherwise using jittered
   exponential backoff; bounded attempt count and total delay via `DataverseRetryOptions`.
3. **User handlers** — additional `DelegatingHandler`s registered through the `AddDataverse`
   builder run after the SDK handlers (see `extension-model.md`).
4. **Primary handler / network** — the `HttpClient` primary handler managed by
   `IHttpClientFactory` (connection pooling, handler lifetime rotation).

Each named client (default plus any named registrations) gets its own logical pipeline and its
own options snapshot; the factory pattern of `IHttpClientFactory` keeps socket usage pooled
across instances.

### 2.5 Dataverse Web API v9.2

The SDK pins the `v9.2` route segment (master plan §8, risk table). Protocol concerns —
OData 4.0 conventions, `Prefer` headers, service-protection limit responses, error payload
shapes — are encapsulated in the core client and never leak into `Abstractions`.

## 3. Thread-safety model

| Kind | Examples | Model |
|---|---|---|
| Client services | `IDataverseClient`, `IMetadataClient`, `ISolutionClient`, `IDataverseClientFactory`, token providers | Thread-safe; registered as singletons; safe for concurrent use from any thread |
| Results / models | `Entity` results, `DataverseQueryResult`, `WhoAmIResponse`, `BatchResponse`, metadata models, `DataverseError` | Immutable after the SDK returns them (records or effectively read-only); safe to share across threads. A consumer-constructed `Entity` being prepared for a write is an ordinary mutable object owned by its creator until passed to the client. |
| Builders | `ODataQuery`, `FetchXml` builder chain, `BatchRequest` composition | Mutable-until-`Build`, **not** thread-safe; build on one thread, use anywhere. Documented on every builder type. |

Rationale: singleton services keep DI simple and match `IHttpClientFactory` guidance; immutable
results eliminate defensive copies; builders trade thread safety for a fluent mutable API, which
is safe because a builder instance is naturally confined to the code path that creates it.

## 4. Async model

- **Async-first:** every I/O operation is exposed only as an async method with the `Async`
  suffix returning `Task`/`Task<T>`/`ValueTask<T>`/`IAsyncEnumerable<T>`. There are no
  synchronous I/O counterparts and the SDK never performs sync-over-async internally
  (no `.Result`, `.Wait()`, or `GetAwaiter().GetResult()` on I/O paths).
- **Streaming:** unbounded reads use `IAsyncEnumerable<Entity>` (`QueryAllAsync`,
  FetchXML paging) so consumers can stop enumerating at any point and paging stops with them.
- **Context:** the library uses `ConfigureAwait(false)` internally; it never depends on a
  synchronization context.

## 5. Cancellation model

- Every I/O method takes a `CancellationToken` parameter (defaulted to `default` so simple call
  sites stay simple, but always present — master plan §4).
- Per-request timeout: the caller's token is combined with the configured per-request timeout
  through a linked `CancellationTokenSource`. Timeout expiry surfaces as the SDK's timeout
  error category (see `error-model.md` for the distinction from caller cancellation), while
  genuine caller cancellation surfaces as `OperationCanceledException` — never swallowed,
  never wrapped in `DataverseException`.
- `IAsyncEnumerable` paging honors both the token passed to the query method and any token
  supplied via `WithCancellation` at enumeration time.
- Retry delays observe cancellation: a canceled token aborts a pending backoff wait immediately.

## 6. Time model

All time-dependent behavior — retry backoff delays, `Retry-After` waits, and token expiry
checks in the default token provider — flows through an injected `TimeProvider` (master plan
§5). Production uses `TimeProvider.System`; tests inject a fake to make retry and token-cache
behavior fully deterministic without real delays.

## 7. Related documents

- [`package-boundaries.md`](package-boundaries.md) — what each package may and may not contain.
- [`dependency-rules.md`](dependency-rules.md) — dependency direction and third-party policy.
- [`extension-model.md`](extension-model.md) — supported extension points.
- [`error-model.md`](error-model.md) — error taxonomy and mapping.
- [`observability.md`](observability.md) — logging, tracing, metrics.
- [`diagrams.md`](diagrams.md) — Mermaid diagrams for all of the above.
- [`decision-records/`](decision-records/README.md) — ADRs behind these choices.
