# Package Boundaries

> Elaborates §2 of [`docs/planning/master-plan.md`](../planning/master-plan.md). If this
> document and the master plan disagree, the master plan wins.

This document defines, per package: purpose, contents, allowed dependencies, and — just as
important — what must never appear in it. The boundaries below are enforced by architecture
tests (see [`dependency-rules.md`](dependency-rules.md)) and by PR review
([`../api/public-api-review-checklist.md`](../api/public-api-review-checklist.md)).

## Package summary

| Package | TFMs | Dependencies | MVP |
|---|---|---|---|
| `Koras.Dataverse.Abstractions` | net8.0; net9.0; net10.0 | none | ✔ |
| `Koras.Dataverse.FetchXml` | netstandard2.0; net8.0; net9.0; net10.0 | none | ✔ |
| `Koras.Dataverse` | net8.0; net9.0; net10.0 | Abstractions, FetchXml, Azure.Identity, Microsoft.Extensions.* (see below) | ✔ |
| `Koras.Dataverse.OpenTelemetry` | net8.0; net9.0; net10.0 | Koras.Dataverse (ids only), OpenTelemetry.Api | ✔ |
| `Koras.Dataverse.OrganizationService` | net8.0 | Microsoft.PowerPlatform.Dataverse.Client (heavy) | v1.1 |

## 1. `Koras.Dataverse.Abstractions`

**Purpose.** The contract package: everything a consumer needs to *depend on* the SDK without
depending on its implementation. Application code, domain libraries, and test doubles reference
this package alone.

**Contents.**

- Interfaces: `IDataverseClient`, `IMetadataClient`, `ISolutionClient`,
  `IDataverseTokenProvider`, `IDataverseClientFactory`.
- Data model: `Entity`, `EntityReference`, `ColumnSet`, query result models
  (`DataverseQueryResult`, `WhoAmIResponse`, `UpsertResult`), batch models (`BatchRequest`,
  `BatchOperation`, `BatchResponse`, `BatchItemResult`), metadata models (`TableMetadata`,
  `ColumnMetadata`, `RelationshipMetadata`, `ChoiceOption`), solution models (`SolutionInfo`,
  `SolutionImportOptions`).
- Query model: `ODataQuery`, `ODataFilterBuilder`, `ODataExpand` (builders produce an
  implementation-agnostic query description; execution lives in the core package).
- Error model: `DataverseException`, `DataverseError`, `DataverseErrorCategory`.
- Options: `DataverseClientOptions`, `DataverseAuthenticationOptions`, `DataverseRetryOptions`.

**Allowed dependencies.** None. Only the base class library of each target framework.

**Must never contain.**

- Any NuGet dependency, including Microsoft.Extensions.* and Azure.*.
- HTTP types (`HttpClient`, `HttpRequestMessage`, `HttpStatusCode` on public signatures is
  avoided in favor of `int` status codes — see `error-model.md`), handlers, or anything from
  `System.Net.Http`.
- `Azure.Core.TokenCredential` or any Azure.Identity type. The bridge from `TokenCredential`
  to `IDataverseTokenProvider` lives in the core package (ADR-0004).
- Serialization attributes/types from `System.Text.Json` on the public surface.
- DI registration code (`IServiceCollection` extensions) — DI lives in the main package
  (ADR-0003). Note: the `IDataverseClientFactory` *interface* lives here; its implementation
  and registration live in `Koras.Dataverse`.
- Implementation logic: no networking, no retry policy, no token caching.

## 2. `Koras.Dataverse.FetchXml`

**Purpose.** A standalone, dependency-free fluent FetchXML builder (KDV-004) that is also
usable outside the SDK — including, later, inside Dataverse plugin assemblies (KDV-022), which
is why it targets `netstandard2.0` (ADR-0002).

**Contents.** `FetchXml` (static entry point), `FetchXmlQuery`, `FetchFilterBuilder`,
`FetchLinkEntityBuilder`, `FetchConditionOperator`, and supporting types for ordering, paging
cookies, and strict XML value encoding.

**Allowed dependencies.** None. No project reference to `Abstractions` either — the package
must stand completely alone so it can be consumed from a plugin project without dragging in
anything else.

**Must never contain.**

- Any NuGet or project dependency.
- Any API or language/runtime feature unavailable on `netstandard2.0` in the shipped
  `netstandard2.0` compilation (multi-targeting may light up conveniences on modern TFMs, but
  the `netstandard2.0` build must remain complete and functional, not a stub).
- HTTP, authentication, DI, logging, or telemetry code. The builder produces FetchXML; it never
  executes it.
- References to `Entity`/`EntityReference` or any Abstractions type.

## 3. `Koras.Dataverse` (core)

**Purpose.** The Web API client implementation and the package most consumers install. Pulls in
`Abstractions` and `FetchXml` transitively.

**Contents.** CRUD + upsert + alternate keys, typed POCO mapping (KDV-002); OData query
execution + `IAsyncEnumerable` auto-paging (KDV-003); FetchXML execution with paging cookies
(KDV-004); `$batch` with atomic change sets and continue-on-error (KDV-005); metadata client
(KDV-006); solution client (KDV-007); authentication via Azure.Identity plus the default
`IDataverseTokenProvider` (KDV-001); retry/throttling handler (KDV-008); error payload
normalization (KDV-009); DI registration `AddDataverse` + named clients + factory
implementation + startup validation (KDV-010); logging + `ActivitySource`/`Meter` (KDV-011);
health check (KDV-012).

**Allowed dependencies.**

- `Koras.Dataverse.Abstractions`, `Koras.Dataverse.FetchXml` (project/package references).
- `Azure.Identity`.
- `Microsoft.Extensions.Http`, `Microsoft.Extensions.Options`,
  `Microsoft.Extensions.Options.DataAnnotations`, `Microsoft.Extensions.Logging.Abstractions`,
  `Microsoft.Extensions.DependencyInjection.Abstractions`,
  `Microsoft.Extensions.Diagnostics.HealthChecks.Abstractions` — pinned per-TFM (8.0.x for
  net8.0, 9.0.x for net9.0, 10.0.x for net10.0; ADR-0002, ADR-0009).

**Must never contain.**

- OpenTelemetry packages of any kind — telemetry uses only `System.Diagnostics`
  `ActivitySource`/`Meter` from the BCL (ADR-0008).
- `Microsoft.PowerPlatform.Dataverse.Client`, `Microsoft.Xrm.Sdk`, or any WCF/Organization
  Service dependency (ADR-0001).
- Polly or `Microsoft.Extensions.Http.Resilience` (ADR-0007) — resilience is a focused
  built-in handler.
- `Microsoft.Extensions.Logging` (the full implementation package) — only
  `Logging.Abstractions` is allowed.
- Public API types that expose Azure.Identity or Microsoft.Extensions types beyond what the
  documented surface requires (e.g., `UseTokenCredential(TokenCredential)` on the
  authentication options is deliberate and documented; new leakage requires review).

## 4. `Koras.Dataverse.OpenTelemetry`

**Purpose.** Convenience wiring for OpenTelemetry users: `TracerProviderBuilder` /
`MeterProviderBuilder` extensions that subscribe to the SDK's `ActivitySource` and `Meter` by
name (ADR-0008).

**Contents.** Extension methods (e.g., `AddDataverseInstrumentation()` on both builder types —
exact names subject to implementation review) and the shared source/meter name constants they
reference.

**Allowed dependencies.** `Koras.Dataverse` (referenced for the instrumentation ids/constants
only), `OpenTelemetry.Api`.

**Must never contain.**

- The full `OpenTelemetry` SDK package or any exporter package — `OpenTelemetry.Api` only.
- Instrumentation logic of its own: it must not create activities, record metrics, or add
  processors that inspect payloads. It only registers the source/meter names.
- Any public type other than the builder extensions and what they minimally require.

## 5. `Koras.Dataverse.OrganizationService` (v1.1, not in MVP)

**Purpose.** Optional transport adapter over `Microsoft.PowerPlatform.Dataverse.Client` for
organizations that require `IOrganizationService` semantics (KDV-015, ADR-0001). Planned for
v1.1; boundaries recorded now so the core design keeps room for it.

**Contents (planned).** An adapter implementing the `Abstractions` interfaces over the official
ServiceClient transport. Details are subject to implementation review at v1.1.

**Allowed dependencies (planned).** `Koras.Dataverse.Abstractions`,
`Microsoft.PowerPlatform.Dataverse.Client` and its transitive graph.

**Must never contain.**

- Anything the core package would need to reference — the dependency arrow points only from
  this package toward `Abstractions`, never the reverse. The heavy Microsoft dependency graph
  must never reach `Koras.Dataverse` or `Abstractions`.

## 6. Boundary rules that apply to every package

1. Nothing depends on implementation packages: no package references `Koras.Dataverse` except
   `Koras.Dataverse.OpenTelemetry` (ids only).
2. `Abstractions` and `FetchXml` have zero third-party dependencies, permanently. Adding one is
   a breaking architectural change requiring a superseding ADR.
3. `internal` types are not a boundary escape hatch across packages: no `InternalsVisibleTo`
   from a shipped package to another shipped package (test assemblies excepted).
4. Every public type lives in the namespace assigned to it by master plan §4. The mapping is
   not one-to-one with package names — e.g., `IDataverseClient` and `Entity` sit in the
   `Koras.Dataverse` namespace while shipping in the `Abstractions` package — but it is fixed
   and enforced (see [`../api/naming-guidelines.md`](../api/naming-guidelines.md)).
