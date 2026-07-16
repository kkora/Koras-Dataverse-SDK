# MVP Backlog

> Epics → stories → tasks for the MVP (milestones 0–9), consistent with
> [master plan](master-plan.md) §2 (package layout), §3 (features), §4 (public API).
> Task ids are `KDV-xxx-Tn` (feature id + task number); `KDV-000` is a backlog-local id
> for cross-cutting infrastructure that has no feature id. Public API listed per task
> names only types/members already defined by master plan §4 — no new surface is
> invented here; exact signatures are fixed in `docs/api/public-api-design.md`.
> Test-case detail per feature: [test matrix](../testing/test-matrix.md). Task-level
> "Done when" is always *in addition to* the
> [Definition of Done](definition-of-done.md).

Conventions for file paths: `src/Koras.Dataverse.Abstractions/`,
`src/Koras.Dataverse.FetchXml/`, `src/Koras.Dataverse/`,
`src/Koras.Dataverse.OpenTelemetry/`, `tests/Koras.Dataverse.UnitTests/`,
`tests/Koras.Dataverse.FetchXml.UnitTests/`, `tests/Koras.Dataverse.ArchitectureTests/`,
`tests/Koras.Dataverse.IntegrationTests/`, `tests/Koras.Dataverse.PackageTests/`,
`benchmarks/Koras.Dataverse.Benchmarks/`.

---

## Epic 0 — Foundation (Milestone 0)

### Story: contributor-ready repository with quality rails

**KDV-000-T1 — Solution, project shells, build props**
- **Description:** Create solution; empty projects for the four shipped packages + six test projects; `Directory.Build.props` (TFMs, nullable, warnings-as-errors, deterministic build, SourceLink, XML docs, `EnablePackageValidation`); `Directory.Packages.props` (CPM, per-TFM Microsoft.Extensions pins 8.0.x/9.0.x/10.0.x, Azure.Identity).
- **Files:** `Koras.Dataverse.sln`, `Directory.Build.props`, `Directory.Packages.props`, `src/*/​*.csproj`, `tests/*/​*.csproj`, `.editorconfig`.
- **Depends on:** —. **Public API:** none.
- **Tests:** solution builds/tests/packs on all TFMs (CI proof).
- **Docs:** CONTRIBUTING build instructions.
- **Risks:** TFM/pin mistakes surface late → mitigated by KDV-000-T3 package tests.
- **Done when:** `dotnet build/test/pack` green locally and in CI matrix; FetchXml shell targets netstandard2.0+net8/9/10, others net8/9/10.

**KDV-000-T2 — Analyzers, PublicAPI tracking, CI workflows**
- **Description:** NET analyzers + PublicApiAnalyzers (empty `PublicAPI.Shipped|Unshipped.txt` per shipped project); PR workflow (Linux+Windows × net8/9/10 build/test, coverage via coverlet + ratchet, pack+validation); CodeQL; dependency review; `dotnet list package --vulnerable --include-transitive` step; Dependabot config; secret scanning + push protection; branch protection.
- **Files:** `.github/workflows/ci.yml`, `.github/workflows/codeql.yml`, `.github/dependabot.yml`, `src/*/PublicAPI.*.txt`, props updates.
- **Depends on:** KDV-000-T1. **Public API:** none.
- **Tests:** probe commits prove each gate fails when violated (then reverted).
- **Docs:** CONTRIBUTING (CI expectations), [dependency-security.md](../security/dependency-security.md) alignment.
- **Risks:** gate fatigue if too strict too early → severities finalized at M8.
- **Done when:** all gates active and demonstrated; coverage report published per PR.

**KDV-000-T3 — Community files + package metadata scaffolding + package tests shell**
- **Description:** README (positioning), CONTRIBUTING, CODE_OF_CONDUCT, SECURITY.md (private reporting + supported versions), issue/PR templates, CLAUDE.md; package icon, per-package READMEs, tags, MIT license expression; `tests/Koras.Dataverse.PackageTests` harness (local feed, consumer projects per the [compatibility matrix](../testing/compatibility-testing.md)).
- **Files:** repo root community files, `assets/icon.png`, `src/*/README.md`, `tests/Koras.Dataverse.PackageTests/**`.
- **Depends on:** KDV-000-T1. **Public API:** none.
- **Tests:** package-consumption smoke green on shell packages; metadata assertions pass.
- **Docs:** the files themselves.
- **Risks:** metadata drift → assertions in package tests keep it pinned.
- **Done when:** packed shells install from local feed into all consumer scenarios.

---

## Epic A — Abstractions & models (Milestone 1)

### Story: complete contract surface (interfaces, models, errors, options)

**KDV-002-T1 — Entity model**
- **Description:** `Entity` (logical name, id, attribute dictionary, plain CLR values, indexer), `EntityReference`, `ColumnSet`, `DataverseQueryResult`, `WhoAmIResponse`, `UpsertResult`; records for immutable models; sealed by default.
- **Files:** `src/Koras.Dataverse.Abstractions/Entity.cs`, `EntityReference.cs`, `ColumnSet.cs`, `DataverseQueryResult.cs`, `WhoAmIResponse.cs`, `UpsertResult.cs`; `tests/Koras.Dataverse.UnitTests/Model/*`.
- **Depends on:** KDV-000-T1. **Public API:** the listed types (ns `Koras.Dataverse`).
- **Tests:** attribute get/set semantics, null handling, invalid-input guards (empty logical name), equality for records.
- **Docs:** XML docs; entity-model concept page (M7).
- **Risks:** value-conversion ambiguity (decimal vs double for money) → decide in API doc, pin with tests.
- **Done when:** matrix rows (model half of KDV-002) green; zero deps proven by arch tests.

**KDV-009-T1 — Error model**
- **Description:** `DataverseError` (category, Dataverse code, HTTP status, request id, transient flag, message), `DataverseErrorCategory` enum, `DataverseException`.
- **Files:** `src/Koras.Dataverse.Abstractions/Errors/DataverseError.cs`, `DataverseErrorCategory.cs`, `DataverseException.cs`; `tests/Koras.Dataverse.UnitTests/Errors/*`.
- **Depends on:** KDV-000-T1. **Public API:** ns `Koras.Dataverse.Errors` types above.
- **Tests:** construction, message composition (category+status+request id), serialization-safety of exception.
- **Docs:** XML docs; error-handling concept page (M7) with the category taxonomy table.
- **Risks:** taxonomy churn after real-payload exposure → categories reviewed again at M8 against live responses.
- **Done when:** model tests green; taxonomy documented.

**KDV-010-T1 — Options types + validation attributes**
- **Description:** `DataverseClientOptions` (`EnvironmentUrl`, api-version, timeout), `DataverseAuthenticationOptions` (+ `UseClientSecret/UseCertificate/UseManagedIdentity/UseDefault/UseInteractive/UseTokenCredential` helpers), `DataverseRetryOptions`; DataAnnotations + HTTPS-only rule; no secret leakage via `ToString()`.
- **Files:** `src/Koras.Dataverse.Abstractions/Options/*.cs`; `tests/Koras.Dataverse.UnitTests/Options/*`.
- **Depends on:** KDV-000-T1. **Public API:** the three options types + credential helpers (ns `Koras.Dataverse` / `Koras.Dataverse.Authentication`).
- **Tests:** validation matrix (missing URL, http URL, conflicting credential kinds, out-of-range retry values), binding round trip, ToString redaction.
- **Docs:** XML docs; [secure-configuration.md](../security/secure-configuration.md) appsettings shape stays in sync.
- **Risks:** DataAnnotations expressiveness limits → custom `IValidateOptions` in core (KDV-010-T2).
- **Done when:** configuration-validation matrix rows green.

**KDV-000-T4 — Interfaces + batch/metadata/solution models + architecture tests**
- **Description:** `IDataverseClient`, `IMetadataClient`, `ISolutionClient`, `IDataverseTokenProvider`, `IDataverseClientFactory`; `BatchRequest`/`BatchOperation`/`BatchResponse`/`BatchItemResult`; `TableMetadata`/`ColumnMetadata`/`RelationshipMetadata`/`ChoiceOption`; `SolutionInfo`/`SolutionImportOptions`; `ODataQuery`/`ODataFilterBuilder`/`ODataExpand` shells per API doc placement. Stand up `tests/Koras.Dataverse.ArchitectureTests` (zero third-party in Abstractions/FetchXml, dependency direction, sealed/abstract, Async+CancellationToken, namespaces).
- **Files:** `src/Koras.Dataverse.Abstractions/**` (interfaces + `Batches/`, `Metadata/`, `Solutions/` models), `tests/Koras.Dataverse.ArchitectureTests/**`.
- **Depends on:** KDV-002-T1, KDV-009-T1, KDV-010-T1. **Public API:** as listed in master plan §4 namespaces.
- **Tests:** architecture suite green; interface members compile against planned usage samples (doc snippets as compiled tests where practical).
- **Docs:** XML docs on all members; `PublicAPI.Unshipped.txt` review.
- **Risks:** interface churn during M2 implementation → expected pre-1.0; PublicAPI diffs keep it visible.
- **Done when:** Abstractions API review sign-off recorded; arch tests enforced in CI.

---

## Epic B — Core client (Milestone 2)

### Story: authentication (KDV-001)

**KDV-001-T1 — Token provider (TokenCredential adapter, cache, single-flight)**
- **Description:** Default `IDataverseTokenProvider` over `Azure.Core.TokenCredential`; scope `{environmentUrl}/.default`; cache until 5 min before expiry; thread-safe single-flight refresh; `TimeProvider`-driven.
- **Files:** `src/Koras.Dataverse/Authentication/TokenCredentialDataverseTokenProvider.cs` (+ internals); `tests/Koras.Dataverse.UnitTests/Authentication/*`, shared `FakeTimeProvider`.
- **Depends on:** KDV-000-T4, KDV-010-T1. **Public API:** none beyond `IDataverseTokenProvider` (implementation internal or minimal per API doc).
- **Tests:** KDV-001 matrix rows: cache boundary at 5 min, single-flight under parallelism, cancellation, credential-failure mapping.
- **Docs:** authentication concept page (M7); XML docs.
- **Risks:** sovereign-cloud scope subtleties → `UseTokenCredential` escape hatch documented (plan §8 risk).
- **Done when:** KDV-001 unit rows green with zero real time waits.

**KDV-001-T2 — Authentication handler + credential factory from options**
- **Description:** `DelegatingHandler` attaching bearer tokens from the provider; factory translating `DataverseAuthenticationOptions` into the right `TokenCredential` (secret/cert/managed identity/default/interactive/custom).
- **Files:** `src/Koras.Dataverse/Http/AuthenticationHandler.cs`, `src/Koras.Dataverse/Authentication/CredentialFactory.cs`; `tests/Koras.Dataverse.UnitTests/Http/AuthenticationHandlerTests.cs`.
- **Depends on:** KDV-001-T1. **Public API:** none (pipeline internals).
- **Tests:** header attachment, provider-failure short-circuit (no request sent), never-logged token assertions.
- **Docs:** architecture page (pipeline diagram reference).
- **Risks:** credential kinds behave differently across clouds → integration coverage limited to client secret (documented in [test-strategy §9](../testing/test-strategy.md)).
- **Done when:** component tests through the real pipeline green; redaction tests green.

### Story: resilience (KDV-008) and error model mapping (KDV-009)

**KDV-008-T1 — Retry handler**
- **Description:** `DelegatingHandler` implementing bounded retries for 429/503/504 + transient network faults; `Retry-After` (delta + http-date) honored exactly; jittered exponential backoff; per-attempt timeout via linked CTS; `TimeProvider`-driven delays; configured by `DataverseRetryOptions`.
- **Files:** `src/Koras.Dataverse/Http/RetryHandler.cs`; `tests/Koras.Dataverse.UnitTests/Http/RetryHandlerTests.cs`.
- **Depends on:** KDV-000-T4, KDV-010-T1; fake handler/time infra (KDV-008-T2).
- **Public API:** behavior of `DataverseRetryOptions` only.
- **Tests:** full KDV-008 matrix rows (delay exactness, non-retry statuses, budget exhaustion, cancellation during backoff, concurrency isolation).
- **Docs:** resilience concept page (M7): what is retried, how to tune.
- **Risks:** throttling semantics drift (plan §8 risk) → central policy + options; live suite passively validates.
- **Done when:** matrix rows green; zero real delays in tests.

**KDV-008-T2 — Test infrastructure: FakeHttpMessageHandler + FakeTimeProvider**
- **Description:** Reusable scripted handler (matchers, request capture with buffered bodies, sequenced failures, gates) and fake time (advance, timers, cancelable delays) per [test-strategy §6–7](../testing/test-strategy.md).
- **Files:** `tests/Koras.Dataverse.UnitTests/Infrastructure/FakeHttpMessageHandler.cs`, `FakeTimeProvider.cs`.
- **Depends on:** KDV-000-T1. **Public API:** none (test-internal).
- **Tests:** self-tests for the fakes (capture fidelity, timer firing, cancellation).
- **Docs:** CONTRIBUTING testing section.
- **Risks:** fake-time semantics diverge from BCL → mirror `Microsoft.Extensions.TimeProvider.Testing` semantics; revisit via ADR if costly.
- **Done when:** all Epic B suites consume these fakes; no test uses sockets or `Task.Delay` real time.

**KDV-009-T2 — Error payload parser + mapping**
- **Description:** Non-success response → parse OData error payload (size-capped, tolerant of malformed/non-JSON) → `DataverseError` → throw `DataverseException`; transient classification; request-id extraction; mapping table per category.
- **Files:** `src/Koras.Dataverse/Errors/ErrorParser.cs` (+ mapping internals); `tests/Koras.Dataverse.UnitTests/Errors/ErrorParserTests.cs` + payload fixtures.
- **Depends on:** KDV-009-T1. **Public API:** none beyond the Abstractions error types.
- **Tests:** table-driven KDV-009 matrix rows incl. hostile/oversized/HTML bodies.
- **Docs:** error-handling page taxonomy table (M7).
- **Risks:** real payload variance → fixtures extended at M8 from live observations.
- **Done when:** matrix rows green; parser fuzz-ish corpus in place.

### Story: CRUD + serialization (KDV-002)

**KDV-002-T2 — JSON serialization + POCO mapping**
- **Description:** System.Text.Json serialization of `Entity` (invariant culture, `@odata.bind` for `EntityReference` values, JSON null for nulls, no polymorphism); tolerant response deserialization (annotations ignored); attribute-based POCO ↔ `Entity` mapping.
- **Files:** `src/Koras.Dataverse/Serialization/*`; `tests/Koras.Dataverse.UnitTests/Serialization/*`.
- **Depends on:** KDV-002-T1. **Public API:** POCO mapping attributes as fixed in the API doc (attribute-based mapping per master plan §3).
- **Tests:** round trips per CLR type, culture matrix (en-US/de-DE/tr-TR), annotation tolerance, `@odata.bind` cases.
- **Docs:** typed-mapping page (M7).
- **Risks:** type-mapping edge cases (money/choice values as plain CLR) → decisions recorded in API doc + pinned tests.
- **Done when:** serialization matrix rows green on all TFMs.

**KDV-002-T3 — CRUD + upsert + alternate keys client operations**
- **Description:** `IDataverseClient` implementation for Create/Retrieve/Update/Delete/Upsert (+ alternate-key addressing, `UpsertResult`, id-from-header on create, `WhoAmIAsync`); per-request timeout; activities/metrics hooks stubbed for M6.
- **Files:** `src/Koras.Dataverse/DataverseClient.cs` (+ `Http/` internals); `tests/Koras.Dataverse.UnitTests/Client/CrudTests.cs`.
- **Depends on:** KDV-001-T2, KDV-008-T1, KDV-009-T2, KDV-002-T2. **Public API:** `IDataverseClient` CRUD members per master plan §4.
- **Tests:** KDV-002 matrix rows end-to-end through the pipeline (request-shape contract tests + failure mapping + cancellation).
- **Docs:** CRUD quick start + recipes (M7).
- **Risks:** alternate-key encoding subtleties → boundary tests with quotes/Unicode keys.
- **Done when:** KDV-002 rows green; contract tests pin wire shapes.

### Story: OData queries + paging (KDV-003)

**KDV-003-T1 — ODataQuery builder family with strict encoding**
- **Description:** `ODataQuery.For(entitySet)` + `Select/Where/OrderBy/Top/Expand` etc.; `ODataFilterBuilder` (`Eq`, comparison/string functions per API doc), `ODataExpand`; strict literal encoding for all value types; identifier validation; injection-safe by construction.
- **Files:** `src/Koras.Dataverse/Queries/ODataQuery.cs`, `ODataFilterBuilder.cs`, `ODataExpand.cs`, encoder internals; `tests/Koras.Dataverse.UnitTests/Queries/*`.
- **Depends on:** KDV-000-T4. **Public API:** ns `Koras.Dataverse.Queries` types (master plan §4).
- **Tests:** query-string exactness, literal encoding table, hostile-input corpus, invalid identifiers.
- **Docs:** query concept page; injection note pointing raw fragments to caller responsibility ([threat model §2.2](../security/threat-model.md)).
- **Risks:** operator surface creep → MVP operator set fixed in API doc; extensions are minors.
- **Done when:** KDV-003 builder rows + security corpus green.

**KDV-003-T2 — Query execution + IAsyncEnumerable auto-paging**
- **Description:** `QueryAsync` (single page → `DataverseQueryResult`), `QueryAllAsync` (`IAsyncEnumerable<Entity>`, `@odata.nextLink` following on the configured host only, `Prefer: odata.maxpagesize` support, streaming deserialization, lazy fetch, disposal stops fetching).
- **Files:** `src/Koras.Dataverse/DataverseClient.Query.cs` (+ paging internals); `tests/Koras.Dataverse.UnitTests/Client/QueryPagingTests.cs`.
- **Depends on:** KDV-003-T1, KDV-002-T3. **Public API:** `IDataverseClient.QueryAsync`/`QueryAllAsync` per §4.
- **Tests:** multi-page enumeration, cancellation between pages, early disposal, mid-enumeration 429 retry, empty pages.
- **Docs:** paging recipe; [memory-management](../performance/memory-management.md) alignment.
- **Risks:** nextLink host confusion (SSRF class) → cross-host nextLink rejected; test pinned.
- **Done when:** KDV-003 execution rows green; streaming behavior verified (single-page memory in tests via allocation-conscious assertions where practical, benchmarks at M8).

### Story: batch (KDV-005)

**KDV-005-T1 — Batch payload assembly + response parsing**
- **Description:** `BatchRequest`/`BatchOperation` multipart generation (change sets, Content-ID references, 1000-op guard, streaming content writer), `BatchResponse`/`BatchItemResult` parsing (ordered, per-item error mapping, continue-on-error).
- **Files:** `src/Koras.Dataverse/Batches/BatchPayloadWriter.cs`, `BatchResponseParser.cs`; `tests/Koras.Dataverse.UnitTests/Batches/*` + multipart fixtures.
- **Depends on:** KDV-002-T2, KDV-009-T2. **Public API:** ns `Koras.Dataverse.Batches` types.
- **Tests:** payload exactness, guard boundaries (1000/1001), atomic vs continue-on-error parsing, malformed multipart tolerance.
- **Docs:** batch recipe (M7); size guidance links to performance docs.
- **Risks:** LOH from giant payloads → streaming writer; benchmark at M8.
- **Done when:** KDV-005 assembly/parsing rows green.

**KDV-005-T2 — ExecuteBatchAsync client operation**
- **Description:** Execute `$batch` through the pipeline; whole-batch retry semantics; per-item results surfaced without throwing in continue-on-error mode; change-set atomic failure semantics.
- **Files:** `src/Koras.Dataverse/DataverseClient.Batch.cs`; `tests/Koras.Dataverse.UnitTests/Client/BatchExecutionTests.cs`.
- **Depends on:** KDV-005-T1, KDV-002-T3. **Public API:** `IDataverseClient` batch member per §4.
- **Tests:** KDV-005 execution rows incl. whole-batch 429 retry and cancellation.
- **Docs:** batch recipe.
- **Risks:** retrying non-idempotent batches → semantics documented explicitly (retry only transport-level/429 failures of the whole request).
- **Done when:** KDV-005 rows green end-to-end.

---

## Epic C — DI & configuration (Milestone 3, KDV-010)

**KDV-010-T2 — AddDataverse + named clients + factory + startup validation**
- **Description:** `AddDataverse(Action<DataverseClientOptions>)` (+ named overload), named `HttpClient` `"Koras.Dataverse:{name}"` with handler chain, singleton client registrations (`IDataverseClient`, `IMetadataClient`, `ISolutionClient`), `IDataverseClientFactory` with strict-miss throw, options binding from `IConfiguration`, `ValidateOnStart` (DataAnnotations + custom `IValidateOptions` for HTTPS/credential rules), full per-name isolation.
- **Files:** `src/Koras.Dataverse/DependencyInjection/DataverseServiceCollectionExtensions.cs` (ns `Microsoft.Extensions.DependencyInjection`), `DataverseClientFactory.cs`, options validation internals; `tests/Koras.Dataverse.UnitTests/DependencyInjection/*`, `Configuration/*`.
- **Depends on:** Epic B stories. **Public API:** `AddDataverse`, `IDataverseClientFactory` per §4.
- **Tests:** KDV-010 matrix rows (lifetimes, named isolation incl. token cache, unknown-name throw, startup-failure cases, binding round trip).
- **Docs:** registration/configuration pages; [secure-configuration.md](../security/secure-configuration.md) stays authoritative for the appsettings shape.
- **Risks:** double-registration semantics ambiguous → behavior chosen in API doc and pinned by test.
- **Done when:** KDV-010 rows green; smoke host boots from documented appsettings + user-secrets.

---

## Epic D — Feature packages (Milestone 4)

### Story: FetchXML (KDV-004)

**KDV-004-T1 — FetchXml builder package**
- **Description:** `FetchXml.For(entity)` entry; `FetchXmlQuery`; `Attributes`, `Where` (`FetchFilterBuilder`: `Eq/Like/And/Or`… per `FetchConditionOperator`), `Link` (`FetchLinkEntityBuilder` with `Alias`/`Attributes`), `OrderBy`, `Top`, paging-cookie support; XML-writer generation (escaping by construction); netstandard2.0-compatible code.
- **Files:** `src/Koras.Dataverse.FetchXml/FetchXml.cs`, `FetchXmlQuery.cs`, `FetchFilterBuilder.cs`, `FetchLinkEntityBuilder.cs`, `FetchConditionOperator.cs`; `tests/Koras.Dataverse.FetchXml.UnitTests/**`.
- **Depends on:** KDV-000-T1. **Public API:** ns `Koras.Dataverse.FetchXml` per §4.
- **Tests:** KDV-004 builder rows; escaping corpus + re-parse well-formedness; ns2.0 consumption via package tests.
- **Docs:** FetchXML builder page; standalone-usage (plugin) note.
- **Risks:** operator coverage gaps vs real FetchXML → operator set enumerated in API doc; additive growth in minors.
- **Done when:** builder rows green; zero-dep + ns2.0 proven in CI.

**KDV-004-T2 — FetchXML execution + paging-cookie paging**
- **Description:** `FetchAsync(fetchXml, ct)` in core (encoded transmission), fetch-all path using paging cookies; raw-string escape hatch documented as caller responsibility.
- **Files:** `src/Koras.Dataverse/DataverseClient.Fetch.cs`; `tests/Koras.Dataverse.UnitTests/Client/FetchExecutionTests.cs`.
- **Depends on:** KDV-004-T1, KDV-002-T3. **Public API:** `IDataverseClient.FetchAsync` (+ fetch-all member per API doc).
- **Tests:** KDV-004 execution rows (cookie round-trip encoding, cancellation between pages, server-rejection mapping).
- **Docs:** FetchXML execution page with injection warning on raw strings.
- **Risks:** cookie encoding quirks → boundary fixtures from documented formats, extended at M8 from live runs.
- **Done when:** KDV-004 rows green.

### Story: metadata (KDV-006)

**KDV-006-T1 — Metadata client**
- **Description:** `IMetadataClient` implementation: tables, columns, local + global choices, relationships → `TableMetadata`/`ColumnMetadata`/`RelationshipMetadata`/`ChoiceOption` (read-only, typed lightweight models); no caching (per [data-protection §5](../security/data-protection.md)).
- **Files:** `src/Koras.Dataverse/Metadata/MetadataClient.cs` (+ internals); `tests/Koras.Dataverse.UnitTests/Metadata/*` + fixtures.
- **Depends on:** KDV-002-T3 pipeline pieces. **Public API:** ns `Koras.Dataverse.Metadata` per §4.
- **Tests:** KDV-006 matrix rows (type mapping, label fallback, not-found mapping, cancellation).
- **Docs:** metadata page (M7).
- **Risks:** EntityDefinitions payload breadth → lightweight models keep scope contained; unknown members tolerated.
- **Done when:** KDV-006 rows green.

### Story: solutions (KDV-007)

**KDV-007-T1 — Solution client**
- **Description:** `ISolutionClient` implementation: export (bytes), import with async-job polling (bounded, cancelable, `TimeProvider`-driven interval), publish-all, installed-solution query → `SolutionInfo`; `SolutionImportOptions`.
- **Files:** `src/Koras.Dataverse/Solutions/SolutionClient.cs`; `tests/Koras.Dataverse.UnitTests/Solutions/*`.
- **Depends on:** KDV-002-T3 pipeline pieces. **Public API:** ns `Koras.Dataverse.Solutions` per §4.
- **Tests:** KDV-007 matrix rows (polling completion/fault/cancellation, empty-input guards, retry pass-through).
- **Docs:** solutions page; ALM-separation guidance links to [secure-configuration §7](../security/secure-configuration.md).
- **Risks:** import job status variance → tolerant status parsing, fixtures extended at M8.
- **Done when:** KDV-007 rows green with zero real-time polling waits in tests.

---

## Epic E — ASP.NET Core & observability (Milestones 5–6)

**KDV-012-T1 — Health check**
- **Description:** `AddDataverseHealthCheck()` registering a WhoAmI probe; healthy/degraded/unhealthy mapping (documented); named-client targeting; tags/name options per health-check conventions; secret-free failure descriptions.
- **Files:** `src/Koras.Dataverse/HealthChecks/DataverseHealthCheck.cs`, registration extension (ns `Microsoft.Extensions.DependencyInjection`); `tests/Koras.Dataverse.UnitTests/HealthChecks/*`.
- **Depends on:** KDV-010-T2, KDV-002-T3 (WhoAmI). **Public API:** `AddDataverseHealthCheck` per §4.
- **Tests:** KDV-012 matrix rows.
- **Docs:** health-check page + minimal API sample wiring.
- **Risks:** probe cost under aggressive health polling → WhoAmI is cheap; documented guidance on polling intervals.
- **Done when:** KDV-012 rows green; sample `/health` behaves per mapping.

**KDV-011-T1 — Logging, ActivitySource, Meter in core**
- **Description:** Structured logging with stable `Koras.Dataverse.*` categories and property names; `ActivitySource "Koras.Dataverse"` activities at the client layer wrapping retries; `Meter` counters/histograms (requests, duration, retries); allowed-tag policy enforced ([data-protection §3](../security/data-protection.md)); zero-cost no-listener path.
- **Files:** `src/Koras.Dataverse/Diagnostics/*` + instrumentation call sites in client operations; `tests/Koras.Dataverse.UnitTests/Diagnostics/*`.
- **Depends on:** Epics B–D operations in place. **Public API:** source/meter/category *names* as documented contract; no new types beyond §4.
- **Tests:** KDV-011 matrix rows (activity capture, tag policy, redaction, no-listener safety, retry-wrapping).
- **Docs:** observability page: names, tags, metrics reference.
- **Risks:** tag cardinality creep → policy tests enumerate the allowed set.
- **Done when:** KDV-011 core rows green; arch test confirms no OTel dependency in core.

**KDV-011-T2 — Koras.Dataverse.OpenTelemetry package**
- **Description:** `TracerProviderBuilder`/`MeterProviderBuilder` extension methods subscribing the SDK's source/meter ids; depends on core (ids only) + OpenTelemetry.Api.
- **Files:** `src/Koras.Dataverse.OpenTelemetry/*`; `tests/Koras.Dataverse.UnitTests/OpenTelemetry/*` (or dedicated small test project if isolation demands); package-consumption scenario.
- **Depends on:** KDV-011-T1. **Public API:** the two builder extensions per master plan §2.
- **Tests:** subscription works (spans/metrics observed via in-memory exporters), package consumption green.
- **Docs:** OTel setup page.
- **Risks:** OpenTelemetry.Api version breadth → widest workable floor, pinned in CPM.
- **Done when:** package builds, tests green, consumption scenario green.

---

## Epic F — Adoption & hardening (Milestones 7–8)

**KDV-000-T5 — Samples (Console, MinimalApi, Worker)**
- **Description:** Three runnable samples per master plan §1 adoption strategy, restoring against local pack output in CI; each demonstrates registration, CRUD, query streaming, batch, health check (web), telemetry wiring (worker).
- **Files:** `samples/Console/**`, `samples/MinimalApi/**`, `samples/Worker/**`.
- **Depends on:** Milestones 2–6. **Public API:** none.
- **Tests:** CI builds + runs samples against fake/local configuration.
- **Docs:** each sample's README.
- **Risks:** samples rot → CI-built forever.
- **Done when:** all three run green in CI.

**KDV-000-T6 — Docs tree completion**
- **Description:** Quick start, per-feature pages (KDV-001..012), recipes (throttling/paging/batch/metadata), ServiceClient comparison, API docs consistency pass; verify every "ships with docs page" bullet of master plan §3.
- **Files:** `docs/**` (features/, guides/ per docs plan).
- **Depends on:** KDV-000-T5. **Public API:** none.
- **Tests:** doc code snippets compiled where practical; link check in CI.
- **Docs:** is the deliverable.
- **Risks:** drift vs implementation → snippets compiled; release checklist re-verifies.
- **Done when:** docs review sign-off; quick start ≤ 5 minutes cold.

**KDV-000-T7 — Live integration suite + nightly workflow**
- **Description:** `tests/Koras.Dataverse.IntegrationTests` per [integration-testing.md](../testing/integration-testing.md): `LiveFact` gating on `KORAS_DATAVERSE_*` env vars, collection fixture, run-prefix isolation + sweeper, throttling etiquette, CRUD/paging/batch/metadata/WhoAmI coverage; nightly + release-gate workflows with protected-environment secrets.
- **Files:** `tests/Koras.Dataverse.IntegrationTests/**`, `.github/workflows/nightly-live.yml`.
- **Depends on:** Milestones 2–6. **Public API:** none.
- **Tests:** is the deliverable; skip-clean behavior verified without env vars.
- **Docs:** integration-testing doc stays authoritative.
- **Risks:** flake → quarantine policy ([integration-testing §7](../testing/integration-testing.md)).
- **Done when:** nightly green against the real test environment.

**KDV-000-T8 — Benchmarks + coverage gate + security dry run**
- **Description:** Implement suites per [benchmarks.md](../performance/benchmarks.md); capture baseline artifacts; turn on coverage gate (≥ 80/70); run the [security checklist](../security/security-checklist.md) dry run + threat-model review vs code; finalize analyzer severities; expand parser corpora.
- **Files:** `benchmarks/Koras.Dataverse.Benchmarks/**`, CI workflow updates, analyzer config.
- **Depends on:** feature-complete MVP (M7). **Public API:** none.
- **Tests:** benchmarks runnable per methodology; gates active in CI.
- **Docs:** baseline artifacts archived; findings tracked.
- **Risks:** late-found perf/security issues → that is the point of M8; buffer before M9.
- **Done when:** M8 exit criteria in [implementation-plan.md](implementation-plan.md) met.

## Epic G — Release (Milestone 9)

**KDV-000-T9 — Release workflow + NuGet setup + 0.1.0-preview.1**
- **Description:** Tag-triggered release workflow per [release-process.md](../release/release-process.md) (verify → test → live gate → benchmarks → pack → sign-when-configured → `nuget-release`-gated publish → GitHub Release with notes/symbols/SBOMs); NuGet.org org + push-only `Koras.*` API key as environment secret; local-feed dry run; CHANGELOG; execute release + security checklists; publish; post-release verification; file `Koras.*` prefix reservation.
- **Files:** `.github/workflows/release.yml`, `CHANGELOG.md`, version property bump.
- **Depends on:** KDV-000-T8. **Public API:** none (promotes `PublicAPI.Unshipped` at stable releases only; preview skips promotion).
- **Tests:** dry run end-to-end against local feed; package tests against release-versioned output.
- **Docs:** release docs stay authoritative; release issue archives checklists.
- **Risks:** first-publish friction (indexing lag, reservation delay) → handled in [nuget-publishing.md §5](../release/nuget-publishing.md).
- **Done when:** `0.1.0-preview.1` installable from NuGet.org with verified listing, symbols, and archived checklists.

---

## Dependency overview (critical path)

```
KDV-000-T1 → KDV-000-T2/T3
KDV-000-T1 → KDV-002-T1, KDV-009-T1, KDV-010-T1 → KDV-000-T4
KDV-000-T4 → KDV-001-T1 → KDV-001-T2 ─┐
KDV-000-T4 → KDV-008-T2 → KDV-008-T1 ─┼→ KDV-002-T3 → KDV-003-T2, KDV-005-T2, KDV-004-T2,
KDV-009-T1 → KDV-009-T2 ──────────────┘   KDV-006-T1, KDV-007-T1
KDV-002-T1 → KDV-002-T2;  KDV-000-T4 → KDV-003-T1;  KDV-002-T2/KDV-009-T2 → KDV-005-T1
Epic B..D → KDV-010-T2 → KDV-012-T1;  operations → KDV-011-T1 → KDV-011-T2
M2–M6 → KDV-000-T5..T8 → KDV-000-T9
```
