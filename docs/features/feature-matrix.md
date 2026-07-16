# Feature Matrix — Koras Dataverse SDK

> Feature-to-package mapping per §2 and §3 of
> [`docs/planning/master-plan.md`](../planning/master-plan.md). This is a planning matrix:
> "Required" in the Tests / Documentation / Example columns records the delivery bar each
> feature must meet before its release train ships (master plan §3: every MVP feature ships
> with unit tests, error-path tests, cancellation tests, a docs page, runnable sample usage,
> and XML IntelliSense docs). Nothing here claims current implementation status.

**Package legend.** Core column: where the feature's contracts and implementation live —
`Abstractions` = `Koras.Dataverse.Abstractions` (interfaces/models), `FetchXml` =
`Koras.Dataverse.FetchXml`, `Core` = `Koras.Dataverse` (implementation). Integration column:
companion/optional package involvement — `OpenTelemetry` = `Koras.Dataverse.OpenTelemetry`,
`OrgService` = `Koras.Dataverse.OrganizationService` (v1.1).

| Feature | Core Package | Integration Package | MVP | Tests | Documentation | Example |
|---|---|---|---|---|---|---|
| KDV-001 Authentication | Abstractions (contracts) + Core | — | ✔ | Required (unit + error-path + cancellation; integration per credential type) | Required (per credential mode) | Required (managed identity worker) |
| KDV-002 CRUD / upsert / alternate keys | Abstractions (models) + Core | OrgService (v1.1 adapter) | ✔ | Required (payload, mapping, error, cancellation; CRUD round-trip) | Required (quick start, POCO mapping) | Required (console CRUD) |
| KDV-003 OData queries + auto-paging | Abstractions (models) + Core | OrgService (v1.1 adapter) | ✔ | Required (builder/encoding, paging, cancellation; multi-page integration) | Required (query cookbook) | Required (streaming query) |
| KDV-004 FetchXML builder + execution | FetchXml (builder) + Core (execution) | OrgService (v1.1 adapter) | ✔ | Required (XML snapshots, escaping; paged integration) | Required (builder reference) | Required (Advanced Find port) |
| KDV-005 Batch operations | Abstractions (models) + Core | — | ✔ | Required (payload gen/parse, guard, cancellation; batch integration) | Required (batch recipe) | Required (bulk load) |
| KDV-006 Metadata read | Abstractions (contracts/models) + Core | — | ✔ | Required (mapping fixtures, errors; metadata integration) | Required (metadata guide) | Required (schema report) |
| KDV-007 Solutions | Abstractions (contracts/models) + Core | — | ✔ | Required (polling state machine, errors; export/import integration) | Required (deployment recipe) | Required (pipeline deploy) |
| KDV-008 Resilience | Core | — | ✔ | Required (Retry-After, backoff/jitter, timeout, cancellation) | Required (throttling guide) | Required (shown in all samples) |
| KDV-009 Error model | Abstractions (model) + Core (mapping) | — | ✔ | Required (payload fixture matrix, classification table) | Required (taxonomy reference) | Required (error handling recipe) |
| KDV-010 DI + options | Core (`AddDataverse`) | — | ✔ | Required (registration, validation matrix, named clients) | Required (quick start, config reference) | Required (console / minimal API / worker) |
| KDV-011 Observability | Core (ActivitySource/Meter/ILogger) | OpenTelemetry (exporter wiring) | ✔ | Required (listener-based activity/metric tests) | Required (telemetry contract) | Required (OTel minimal API) |
| KDV-012 Health checks | Core (`AddDataverseHealthCheck`) | — | ✔ | Required (result mapping, timeout; live probe integration) | Required (health wiring) | Required (minimal API sample) |
| KDV-013 Impersonation | Core | OrgService (adapter parity) | — (v1.1) | Required at v1.1 bar | Required (security caveats) | Required |
| KDV-014 File & image columns | Core | — | — (v1.1) | Required at v1.1 bar | Required (streaming recipe) | Required |
| KDV-015 Organization Service transport | Abstractions (contracts) | OrgService (implementation) | — (v1.1) | Required (conformance suite vs core transport) | Required (transport choice guide) | Required |
| KDV-016 Source-generated models | Separate generator package (planned) | — | — (v1.2) | Required (generator snapshot + compile tests) | Required (end-to-end guide) | Required |
| KDV-017 Typed fluent queries | Core (translation) + KDV-016 output | — | — (v1.2) | Required (translation matrix) | Required (operator reference) | Required |
| KDV-018 Metadata snapshot + comparison | Core (on `IMetadataClient`) | — | — (v1.2) | Required (format round-trip, diff matrix) | Required (drift pipeline recipe) | Required |
| KDV-019 Solution dependency analysis | Core (`Koras.Dataverse.Solutions`) | — | — (v2.0) | Defined with design | Defined with design | Defined with design |
| KDV-020 Power Pages Web API helpers | Subject to design (2.0 window) | — | — (v2.0) | Defined with design | Defined with design | Defined with design |
| KDV-021 ALM pipeline helpers | Core (`Koras.Dataverse.Solutions`) + KDV-019 | — | — (v2.0) | Defined with design | Defined with design | Defined with design |
| KDV-022 Plugin development helpers | FetchXml (ns2.0 base) + future helper package | — | — (v2.0) | Required (ns2.0/net462 consumer compile tests) | Defined with design | Defined with design |
| KDV-023 Elastic / long-running helpers | Subject to design (Experimental) | — | — (Experimental) | Gated integration tests | Experimental-status docs | Defined with design |

Notes:

- DI registration (`AddDataverse`, `AddDataverseHealthCheck`) lives in the main
  `Koras.Dataverse` package by design — there is no separate `.DependencyInjection` package
  (ADR-0003, master plan §2).
- `Koras.Dataverse.OpenTelemetry` contains only `TracerProviderBuilder`/`MeterProviderBuilder`
  extensions; the core emits via `ActivitySource`/`Meter` with no OpenTelemetry dependency
  (master plan §2, §3).
- The `OrgService` column entries for KDV-002/003/004/013 reflect the v1.1 adapter implementing
  the same abstractions over `Microsoft.PowerPlatform.Dataverse.Client`; the features
  themselves are core features.
