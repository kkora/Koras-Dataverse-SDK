# Feature Catalog — Koras Dataverse SDK

> Canonical per-feature planning entries for KDV-001 … KDV-023, consistent with §3 and §4 of
> [`docs/planning/master-plan.md`](../planning/master-plan.md). IDs are stable and used across
> docs, backlog, tests, and changelog. Release classifications: MVP / v1.1 / v1.2 / v2.0 /
> Experimental / Out of scope. Detailed planning docs for MVP features are linked per entry.
>
> These are planning entries written before implementation; nothing here is a claim that a
> capability exists yet. API details not fixed in master plan §4 are conservative proposals,
> subject to implementation.

Cross-cutting bar for every MVP feature (master plan §3): unit tests, error-path tests,
cancellation tests, a docs page, runnable sample usage, and XML IntelliSense docs.

---

## KDV-001 — Authentication

- **User problem.** Acquiring, caching, and refreshing Entra ID tokens for Dataverse is
  repetitive and easy to get wrong (expiry races, refresh stampedes, leaked secrets).
- **User story.** As a backend developer, I want to configure authentication once at startup —
  client secret, certificate, managed identity, interactive/dev, `DefaultAzureCredential`, or a
  custom `TokenCredential`/`IDataverseTokenProvider` — so that every request is transparently
  authenticated.
- **Business value.** Removes the highest-risk plumbing; enables secretless (managed identity)
  deployments.
- **Functional requirements.** Credential selection via `DataverseAuthenticationOptions`
  helpers (`UseClientSecret`, `UseCertificate`, `UseManagedIdentity`, `UseInteractive`,
  `UseDefault`, `UseTokenCredential`); custom `IDataverseTokenProvider` override; scope
  `{environmentUrl}/.default`; token cache with proactive refresh (5 minutes before expiry),
  thread-safe with single-flight refresh (master plan §5).
- **Nonfunctional requirements.** Thread-safe; no token material in logs or exceptions;
  testable via injected `TimeProvider`.
- **Public API pointer.** `Koras.Dataverse.Authentication` — `IDataverseTokenProvider`,
  credential option helpers; `DataverseAuthenticationOptions` (master plan §4).
- **Configuration.** Sub-options of `DataverseClientOptions.Authentication`; exactly one
  credential mode must be configured (validated at startup, KDV-010).
- **Dependencies.** Azure.Identity (main package only); KDV-010 for registration/validation.
- **Error conditions.** Missing/ambiguous credential configuration (startup validation
  failure); token acquisition failure surfaced as `DataverseException` with
  `DataverseErrorCategory` authentication classification; cancellation honored during
  acquisition.
- **Security considerations.** Secrets only via options/user-secret stores; certificate and
  managed identity preferred over client secrets; tokens never logged (master plan §7).
- **Performance considerations.** Cached token on the hot path (no per-request acquisition);
  single-flight refresh prevents thundering herd.
- **Observability.** Debug-level log events for acquisition/refresh (no token content); refresh
  failures logged with category and correlation.
- **Testing.** Unit: cache expiry/refresh timing with fake time, single-flight behavior,
  provider selection, cancellation. Integration: WhoAmI succeeds per credential type available
  in CI.
- **Docs & examples.** Docs page per credential mode; sample showing managed identity in a
  worker service; XML docs on all public members.
- **Acceptance criteria.** All six credential modes selectable; token reused until refresh
  window; concurrent first calls trigger exactly one acquisition; misconfiguration fails at
  startup with actionable message.
- **Release classification.** MVP. Detail: [`authentication.md`](authentication.md).

## KDV-002 — CRUD, upsert, alternate keys, entity model

- **User problem.** Basic record operations against the Web API require verbose JSON handling,
  `@odata.bind` syntax knowledge, and wrapper types in the official client.
- **User story.** As a developer, I want create/retrieve/update/delete/upsert with a late-bound
  `Entity` using plain CLR values, plus attribute-based POCO mapping, so that record operations
  are one-liners.
- **Business value.** The most-used surface of the SDK; defines the developer experience.
- **Functional requirements.** `CreateAsync`, `RetrieveAsync`, `UpdateAsync`, `DeleteAsync`,
  `UpsertAsync` (returning `UpsertResult`) on `IDataverseClient`; alternate-key addressing;
  late-bound `Entity`/`EntityReference`/`ColumnSet`; plain CLR values with automatic
  `@odata.bind` handling; attribute-based typed POCO mapping (master plan §3).
- **Nonfunctional requirements.** Async-first with `CancellationToken` on every call;
  thread-safe client; nullable-annotated API.
- **Public API pointer.** `Koras.Dataverse` — `IDataverseClient`, `Entity`, `EntityReference`,
  `ColumnSet`, `UpsertResult`, `WhoAmIResponse` (master plan §4).
- **Configuration.** None beyond client options; serialization behavior fixed by design.
- **Dependencies.** KDV-001 (auth), KDV-008 (resilience), KDV-009 (errors).
- **Error conditions.** Not-found on retrieve/update/delete; duplicate alternate key;
  validation/permission failures — all normalized to `DataverseException` (KDV-009).
- **Security considerations.** Attribute values serialized safely; no dynamic type resolution
  during deserialization (master plan §7).
- **Performance considerations.** Minimal allocation on the serialization path; retrieve honors
  `ColumnSet` to avoid over-fetching.
- **Observability.** One activity per operation with entity logical name and operation tags;
  operation counters/duration histograms (KDV-011).
- **Testing.** Unit: payload generation (including `@odata.bind`), value conversion round-trip,
  POCO mapping, error paths, cancellation. Integration: full CRUD + upsert round-trip.
- **Docs & examples.** CRUD quick start; alternate-key upsert recipe; POCO mapping guide.
- **Acceptance criteria.** CRUD + upsert round-trip against a real environment; alternate keys
  usable on retrieve/update/upsert/delete; plain CLR values only — no wrapper types in the
  public surface.
- **Release classification.** MVP. Detail: [`crud-operations.md`](crud-operations.md).

## KDV-003 — OData query builder + execution + auto-paging

- **User problem.** Hand-built OData query strings are injection-prone, hard to read, and
  paging via `@odata.nextLink` is repetitive.
- **User story.** As a developer, I want a fluent, injection-safe `ODataQuery` builder and an
  `IAsyncEnumerable` auto-paging executor so I can stream any result set safely.
- **Business value.** Primary read path; the injection-safe builder is a headline
  differentiator.
- **Functional requirements.** `ODataQuery.For(...)` with `Select`, `Where(f => ...)` via
  `ODataFilterBuilder` (e.g., `f.Eq(...)`), `OrderBy`, `ODataExpand`; single-page execution
  returning `DataverseQueryResult`; `QueryAllAsync` streaming all pages as
  `IAsyncEnumerable<Entity>` (master plan §4).
- **Nonfunctional requirements.** Strict OData literal encoding of all values; builder is
  mutable-until-`Build` and documented as not thread-safe (master plan §5).
- **Public API pointer.** `Koras.Dataverse.Queries` — `ODataQuery`, `ODataFilterBuilder`,
  `ODataExpand`; `Koras.Dataverse` — `DataverseQueryResult`, `IDataverseClient.QueryAllAsync`.
- **Configuration.** Page size defaults with per-query override (proposed; subject to
  implementation).
- **Dependencies.** KDV-002 (entity model), KDV-008, KDV-009.
- **Error conditions.** Invalid query rejected by the builder before I/O where detectable;
  server query errors normalized (KDV-009); cancellation stops enumeration promptly.
- **Security considerations.** All filter values encoded — string concatenation of user input
  cannot produce injection (master plan §7).
- **Performance considerations.** Streaming enumeration with bounded memory; no eager
  materialization of all pages.
- **Observability.** Activities per page request; page-count/row-count tags where cheap.
- **Testing.** Unit: builder output for every operator, encoding edge cases (quotes, dates,
  GUIDs), paging continuation logic, cancellation mid-stream. Integration: multi-page query
  against a real environment.
- **Docs & examples.** Query cookbook; paging recipe mirroring master plan §4 sample.
- **Acceptance criteria.** Builder output byte-stable for given input; hostile input cannot
  alter query semantics; `QueryAllAsync` yields all rows across pages without memory growth.
- **Release classification.** MVP. Detail: [`odata-queries.md`](odata-queries.md).

## KDV-004 — FetchXML builder + execution + paging-cookie paging

- **User problem.** FetchXML is powerful (aggregates, link-entities, Advanced Find parity) but
  building it by string concatenation is fragile and injection-prone; paging cookies are
  awkward.
- **User story.** As a developer, I want a fluent FetchXML builder (usable standalone) and an
  executor with paging-cookie paging, so I can run existing and new FetchXML queries safely.
- **Business value.** Serves the large installed base of FetchXML knowledge; the standalone
  netstandard2.0 package seeds future plugin scenarios (KDV-022).
- **Functional requirements.** `FetchXml.For(...)` with `Attributes`, `Where(f => ...)`
  (`FetchFilterBuilder` with `Eq`/`Like`/`And` composition), `Link(...)` with
  `FetchLinkEntityBuilder` (`Alias`, `Attributes`), `OrderBy`, `Top`, `Build()` producing
  `FetchXmlQuery`; execution via `IDataverseClient.FetchAsync`; paging-cookie continuation
  (master plan §4).
- **Nonfunctional requirements.** Builder package has zero dependencies; targets
  netstandard2.0 + net8.0/9.0/10.0 (master plan §2); XML output strictly escaped.
- **Public API pointer.** `Koras.Dataverse.FetchXml` — `FetchXml`, `FetchXmlQuery`,
  `FetchFilterBuilder`, `FetchLinkEntityBuilder`, `FetchConditionOperator`.
- **Configuration.** None in the builder; execution page size proposed as an option (subject to
  implementation).
- **Dependencies.** Builder: none. Execution: KDV-002, KDV-008, KDV-009.
- **Error conditions.** Invalid builder states rejected at `Build()`; server FetchXML errors
  normalized (KDV-009).
- **Security considerations.** XML escaping of all attribute names/values blocks injection
  (master plan §7).
- **Performance considerations.** Builder allocates proportionally to query size; paging
  cookies passed through without re-parsing beyond necessity.
- **Observability.** Activities per fetch page; entity name tag.
- **Testing.** Unit: XML output snapshots for operators/links/ordering/paging, escaping edge
  cases, condition-operator coverage. Integration: paged fetch against a real environment.
- **Docs & examples.** Builder reference; "port your Advanced Find query" recipe; standalone
  package usage note.
- **Acceptance criteria.** Generated XML valid against FetchXML schema for all builder paths;
  hostile input inert; multi-page fetch returns complete results.
- **Release classification.** MVP. Detail: [`fetchxml.md`](fetchxml.md).

## KDV-005 — Batch operations (`$batch`)

- **User problem.** The `$batch` endpoint (multipart MIME, change sets, content-ID references,
  1000-op limit, per-item statuses) is the hardest Web API mechanic to hand-roll.
- **User story.** As a developer, I want to compose batches of operations with atomic change
  sets or continue-on-error semantics and get per-item results, so bulk work is reliable.
- **Business value.** Essential for data migration jobs and high-throughput integrations.
- **Functional requirements.** `BatchRequest` composed of `BatchOperation` items; atomic change
  sets; continue-on-error mode; `BatchResponse` with per-item `BatchItemResult`; 1000-operation
  guard (master plan §3, §4).
- **Nonfunctional requirements.** Deterministic payload generation; bounded memory for large
  batches.
- **Public API pointer.** `Koras.Dataverse.Batches` — `BatchRequest`, `BatchOperation`,
  `BatchResponse`, `BatchItemResult`.
- **Configuration.** Continue-on-error flag per request; defaults documented (subject to
  implementation).
- **Dependencies.** KDV-002 (operation payloads), KDV-008, KDV-009.
- **Error conditions.** Over-limit batch rejected client-side before I/O; change-set failure
  reported atomically; per-item errors carried in `BatchItemResult` using the KDV-009 model;
  whole-batch transport failures retried per KDV-008.
- **Security considerations.** Item payloads inherit KDV-002 serialization safety; MIME
  boundaries generated, never user-supplied.
- **Performance considerations.** Streaming multipart serialization where practical; response
  parsed incrementally.
- **Observability.** One activity per batch with item-count tag; per-item failure counter.
- **Testing.** Unit: payload generation/parsing snapshots, change-set atomicity semantics,
  continue-on-error result mapping, 1000-op guard, cancellation. Integration: mixed-operation
  batch round-trip.
- **Docs & examples.** Batch recipe including migration-shaped continue-on-error example.
- **Acceptance criteria.** Per-item results align 1:1 with operations; atomic change sets roll
  back on failure; guard triggers exactly at the documented limit.
- **Release classification.** MVP. Detail: [`batch-operations.md`](batch-operations.md).

## KDV-006 — Metadata read

- **User problem.** Reading table/column/choice/relationship metadata from the Web API involves
  a verbose, deeply nested contract nobody wants to model per project.
- **User story.** As a developer, I want typed, lightweight, read-only metadata models for
  tables, columns, choices (local + global), and relationships, so schema-aware tooling is
  simple to build.
- **Business value.** Enables metadata automation (UC-2) and underpins future KDV-016/KDV-018.
- **Functional requirements.** `IMetadataClient` read operations for tables, columns, local and
  global choices, and relationships, returning `TableMetadata`, `ColumnMetadata`,
  `RelationshipMetadata`, `ChoiceOption` (master plan §3, §4). Read-only helpers; no metadata
  write in this feature.
- **Nonfunctional requirements.** Lightweight models (records), tolerant of server-side
  additions to the metadata contract.
- **Public API pointer.** `Koras.Dataverse.Metadata` — `IMetadataClient`, `TableMetadata`,
  `ColumnMetadata`, `RelationshipMetadata`, `ChoiceOption`.
- **Configuration.** None specific (proposed; caching deliberately left to consumers in MVP,
  subject to implementation).
- **Dependencies.** KDV-001, KDV-008, KDV-009.
- **Error conditions.** Unknown table/column → not-found classification; permission failures
  normalized (KDV-009).
- **Security considerations.** Logical names encoded into request URLs safely.
- **Performance considerations.** Selective property retrieval to keep payloads small; callers
  advised to cache (documented).
- **Observability.** Activities per metadata call with logical-name tags.
- **Testing.** Unit: response mapping from representative payload fixtures, error paths,
  cancellation. Integration: read metadata of a known table incl. choices and relationships.
- **Docs & examples.** Metadata reading guide; convention-validation sample sketch.
- **Acceptance criteria.** All four model families retrievable and correctly mapped; unknown
  names produce classified not-found errors, not raw HTTP failures.
- **Release classification.** MVP. Detail: [`metadata.md`](metadata.md).

## KDV-007 — Solutions

- **User problem.** Scripting solution export/import requires undocumented-feeling Web API
  actions and correct polling of asynchronous import jobs.
- **User story.** As a DevOps engineer, I want export, import (with async job polling),
  publish-all, and installed-solution queries from .NET code, so deployments run inside my
  pipeline without external tools.
- **Business value.** Unlocks solution deployment automation (UC-3); a capability most
  community wrappers lack.
- **Functional requirements.** `ISolutionClient`: export (managed/unmanaged), import with
  `SolutionImportOptions` and async job polling to completion, publish-all, query installed
  solutions returning `SolutionInfo` (master plan §3, §4).
- **Nonfunctional requirements.** Long-running operations fully cancellable; polling interval
  driven by injected `TimeProvider` for testability.
- **Public API pointer.** `Koras.Dataverse.Solutions` — `ISolutionClient`, `SolutionInfo`,
  `SolutionImportOptions`.
- **Configuration.** `SolutionImportOptions` (e.g., overwrite/publish behavior — exact members
  subject to implementation); polling cadence defaults documented.
- **Dependencies.** KDV-001, KDV-008, KDV-009.
- **Error conditions.** Import job failure surfaced with job diagnostic detail via KDV-009;
  export of unknown solution → not-found; timeout of polling bounded and configurable.
- **Security considerations.** Solution archives treated as opaque bytes — never extracted or
  deserialized by the SDK (master plan §7: no polymorphic deserialization).
- **Performance considerations.** Export/import payloads streamed, not buffered wholesale,
  where the API allows.
- **Observability.** Activities spanning the full import including polling; job-status log
  events.
- **Testing.** Unit: request shapes, polling state machine with fake time, failure mapping,
  cancellation mid-poll. Integration: export → import → publish round-trip of a small solution.
- **Docs & examples.** Pipeline deployment recipe (UC-3 flow).
- **Acceptance criteria.** Import returns only after terminal job state; failures carry job
  diagnostics; publish-all and installed-solution query verified against a real environment.
- **Release classification.** MVP. Detail: [`solutions.md`](solutions.md).

## KDV-008 — Resilience

- **User problem.** Dataverse service protection limits (429/503/504 + `Retry-After`) break
  naive clients under load; every team reimplements retry logic differently.
- **User story.** As a developer, I want correct retry, throttling awareness, timeouts, and
  jittered backoff by default, tunable via options, so my service survives load without custom
  code.
- **Business value.** Headline differentiator: service-protection-limit-aware resilience by
  default (master plan §1).
- **Functional requirements.** `RetryHandler` in the HTTP pipeline (`AuthenticationHandler` →
  `RetryHandler` → user handlers → network, master plan §5); honors `Retry-After` always;
  jittered exponential backoff; bounded retry count; per-request timeout combined with caller
  `CancellationToken` via linked CTS; transient-only retries driven by the KDV-009
  classification.
- **Nonfunctional requirements.** Deterministic under fake time (`TimeProvider` injected);
  `OperationCanceledException` never swallowed or wrapped (master plan §5).
- **Public API pointer.** `Koras.Dataverse` — `DataverseRetryOptions` (within
  `DataverseClientOptions`).
- **Configuration.** Retry count, base delay, max delay, timeout — exact option members subject
  to implementation; defaults tuned to service protection limits.
- **Dependencies.** KDV-009 (transient classification), KDV-010 (options).
- **Error conditions.** Retries exhausted → the final failure surfaces via KDV-009 mapping;
  non-transient failures never retried; timeout produces a distinguishable cancellation cause.
- **Security considerations.** Bounded retries prevent amplification (master plan §7).
- **Performance considerations.** Backoff delays never block threads (async waits); jitter
  prevents synchronized retry storms across instances.
- **Observability.** Retry attempts logged with delay and reason; retry counter metric;
  activities wrap the full retry span because telemetry is emitted above the handler (master
  plan §5).
- **Testing.** Unit: Retry-After honored (numeric and date forms), backoff/jitter bounds with
  fake time, retry ceiling, no-retry on non-transient, linked-CTS timeout behavior,
  cancellation propagation. Integration: sustained-load test tolerates injected 429s.
- **Docs & examples.** Throttling guide — the single most searched Dataverse integration topic.
- **Acceptance criteria.** 429 with Retry-After delays exactly as instructed; retries bounded;
  cancellation always wins immediately; defaults require zero configuration.
- **Release classification.** MVP. Detail: [`resilience.md`](resilience.md).

## KDV-009 — Strong error model

- **User problem.** Web API failures surface as raw HTTP + OData payloads; consumers key logic
  off status codes or message strings.
- **User story.** As a developer, I want every failure normalized to `DataverseException`
  carrying a `DataverseError` (category, Dataverse code, HTTP status, request ID, transient
  flag), so error handling is programmatic and support cases are traceable.
- **Business value.** Programmatic error handling plus request-ID traceability for Microsoft
  support escalations.
- **Functional requirements.** Parse OData error payloads on non-success HTTP; produce
  `DataverseError` with `DataverseErrorCategory`, Dataverse error code, HTTP status, request
  id, transient flag; throw `DataverseException`; mapping sits below telemetry and above
  transport, after retries (master plan §5).
- **Nonfunctional requirements.** Error-tolerant parsing (malformed payloads still yield a
  classified error, master plan §8 risk table); no sensitive data in exception messages.
- **Public API pointer.** `Koras.Dataverse.Errors` — `DataverseException`, `DataverseError`,
  `DataverseErrorCategory`.
- **Configuration.** None; taxonomy is fixed contract.
- **Dependencies.** None (Abstractions-level model); consumed by KDV-002…KDV-008, KDV-012.
- **Error conditions.** This feature *is* the error path; edge cases: empty body, non-JSON
  body, unknown Dataverse codes (mapped to a general category, code preserved).
- **Security considerations.** Tokens/secrets never included; payload echo bounded.
- **Performance considerations.** Parsing cost only on failure path; zero overhead on success.
- **Observability.** Errors logged once at the client layer with category, code, and request
  id; error counter tagged by category (KDV-011).
- **Testing.** Unit: payload fixture matrix (throttling, validation, permission, not-found,
  malformed), transient classification table, request-id extraction. Integration: provoked
  not-found and validation failures classified correctly.
- **Docs & examples.** Error-handling guide with category-by-category guidance; taxonomy table.
- **Acceptance criteria.** Every non-success response yields `DataverseException` with populated
  `DataverseError`; transient flag consistent with KDV-008 retry policy; request id present
  whenever the server supplies one.
- **Release classification.** MVP. Detail: [`error-model.md`](error-model.md).

## KDV-010 — Dependency injection + options

- **User problem.** Wiring an HTTP-based client correctly (`IHttpClientFactory`, options,
  validation, named instances) is boilerplate teams get subtly wrong.
- **User story.** As a developer, I want `services.AddDataverse(...)` with options-pattern
  configuration, DataAnnotations startup validation, named clients, and an
  `IDataverseClientFactory`, so registration is one call and misconfiguration fails fast.
- **Business value.** The front door of the SDK; defines the five-minute quick start.
- **Functional requirements.** `AddDataverse` extension in
  `Microsoft.Extensions.DependencyInjection`; `DataverseClientOptions` (with
  `EnvironmentUrl`, `Authentication`, retry options); DataAnnotations + custom validation at
  startup; named clients over `IHttpClientFactory` named client `"Koras.Dataverse:{name}"`;
  `IDataverseClientFactory` for resolving named clients; singleton, thread-safe client
  registration (master plan §2, §4, §5).
- **Nonfunctional requirements.** No separate DI package (ADR-0003); DI registration idempotent
  and additive-safe.
- **Public API pointer.** `Microsoft.Extensions.DependencyInjection` — `AddDataverse`,
  `IDataverseClientFactory`; `Koras.Dataverse` — `DataverseClientOptions`,
  `DataverseAuthenticationOptions`, `DataverseRetryOptions`.
- **Configuration.** This feature *is* the configuration surface; binds from code and
  `IConfiguration` (binding specifics subject to implementation).
- **Dependencies.** Microsoft.Extensions.{Http, Options, Options.DataAnnotations,
  DependencyInjection.Abstractions} (master plan §2); KDV-001.
- **Error conditions.** Invalid options (missing URL, non-HTTPS URL, no credential) fail at
  startup with precise messages; unknown named client requested from factory → clear exception.
- **Security considerations.** Non-HTTPS `EnvironmentUrl` rejected (master plan §7); options
  never logged wholesale (may contain secrets).
- **Performance considerations.** Singleton clients; handler reuse via `IHttpClientFactory`.
- **Observability.** Client name attached as tag/log scope to all telemetry (KDV-011).
- **Testing.** Unit: registration shapes, validation matrix, named-client isolation, factory
  resolution, double-registration behavior. Integration: hosted app boots with configuration
  binding and executes WhoAmI.
- **Docs & examples.** Quick start; named multi-environment recipe; configuration reference.
- **Acceptance criteria.** Single-call registration works in console, minimal API, and worker
  samples; startup validation catches each documented misconfiguration; two named clients hit
  two environments independently.
- **Release classification.** MVP. Detail:
  [`dependency-injection.md`](dependency-injection.md).

## KDV-011 — Observability

- **User problem.** Dataverse calls are a telemetry blind spot; teams bolt on inconsistent
  logging after incidents.
- **User story.** As an operator, I want structured logs, `ActivitySource` traces, and `Meter`
  metrics from every Dataverse operation, exportable via OpenTelemetry, so Dataverse traffic is
  observable like the rest of my system.
- **Business value.** Headline differentiator; prerequisite for enterprise adoption.
- **Functional requirements.** `ILogger` categories per area; `ActivitySource` named
  `"Koras.Dataverse"`; `Meter` counters/histograms (operation counts, durations, retries,
  errors by category); activities emitted at the client layer so they wrap retries (master plan
  §3, §5); `Koras.Dataverse.OpenTelemetry` package with `TracerProviderBuilder`/
  `MeterProviderBuilder` extensions; **no OTel dependency in core** (master plan §2, §3).
- **Nonfunctional requirements.** Near-zero cost when no listener is attached; stable
  instrument/tag names treated as public contract.
- **Public API pointer.** Core: instrumentation is emitted, not exposed as types;
  `Koras.Dataverse.OpenTelemetry` — builder extensions (names subject to implementation).
- **Configuration.** Enrichment/verbosity via standard logging configuration; no bespoke knobs
  planned.
- **Dependencies.** Microsoft.Extensions.Logging.Abstractions (core); OpenTelemetry.Api (OTel
  package only).
- **Error conditions.** Telemetry failures must never fail an operation.
- **Security considerations.** No tokens, secrets, or full record payloads in logs/tags
  (master plan §7).
- **Performance considerations.** Tag allocation guarded by `Activity.IsAllDataRequested`/
  listener checks; histograms bounded in cardinality.
- **Observability.** Self-describing: instrument names and tags documented as a contract page.
- **Testing.** Unit: activity creation/tags via `ActivityListener`, metric emission via
  `MeterListener`, log category assertions, no-listener fast path. Integration: OTel package
  exports spans/metrics in the sample app.
- **Docs & examples.** Telemetry contract reference; OTel wiring sample for minimal API.
- **Acceptance criteria.** Every client operation produces an activity wrapping its retries;
  documented metrics observable via `MeterListener`; core has zero OpenTelemetry package
  references.
- **Release classification.** MVP. Detail: [`observability.md`](observability.md).

## KDV-012 — Health checks

- **User problem.** Services need a standard liveness signal for their Dataverse dependency;
  teams hand-roll WhoAmI pings.
- **User story.** As a developer, I want `AddDataverseHealthCheck()` registering a `WhoAmI`
  probe into ASP.NET Core health checks, so orchestrators and dashboards see Dataverse
  connectivity status.
- **Business value.** Cheap to build, disproportionately valuable for operations; completes the
  "operable service" story.
- **Functional requirements.** `AddDataverseHealthCheck()` extension; executes `WhoAmI`
  (`WhoAmIResponse`) against the configured (optionally named) client; maps
  success/degradation/failure to health-check results (master plan §3, §4).
- **Nonfunctional requirements.** Probe respects health-check timeout/cancellation; never
  throws out of the check.
- **Public API pointer.** `Microsoft.Extensions.DependencyInjection` —
  `AddDataverseHealthCheck`; `Koras.Dataverse` — `WhoAmIResponse`.
- **Configuration.** Standard health-check registration options (name, tags, failure status);
  named-client selection (subject to implementation).
- **Dependencies.** Microsoft.Extensions.Diagnostics.HealthChecks.Abstractions (master plan
  §2); KDV-001, KDV-009, KDV-010.
- **Error conditions.** Auth failure, throttling, and network failure map to unhealthy (or
  configured status) with the KDV-009 category in the check description; no raw exception
  leakage.
- **Security considerations.** Check output contains no tokens or environment secrets.
- **Performance considerations.** WhoAmI is minimal-cost; probe frequency is host-controlled;
  no caching layer needed in MVP.
- **Observability.** Probe executions visible via standard health-check logging; SDK activities
  apply as usual.
- **Testing.** Unit: result mapping per failure class, timeout handling, named-client
  selection. Integration: healthy and credential-broken probes against a real environment.
- **Docs & examples.** Health-check wiring in the minimal API sample.
- **Acceptance criteria.** Registration compiles into standard health pipeline; healthy on
  reachable environment; classified unhealthy result on each simulated failure class.
- **Release classification.** MVP. Detail: [`health-checks.md`](health-checks.md).

---

## KDV-013 — Impersonation (`CallerObjectId`)

- **User problem.** Integration services often must perform operations *as* a specific user
  (auditing, ownership, security-role evaluation), which the Web API supports via the
  `CallerObjectId` header.
- **User story.** As a developer, I want per-client and per-request impersonation so service
  operations are attributed to the correct user.
- **Business value.** Required for many enterprise integrations; small surface, high leverage.
- **Functional requirements.** Per-client default caller and per-request override (exact API
  shape subject to design); header applied consistently across CRUD/query/batch.
- **Nonfunctional requirements.** Zero overhead when unused.
- **Public API pointer.** Extension of `IDataverseClient`/options; to be designed against the
  frozen MVP surface.
- **Configuration.** Optional default caller in client options (proposed).
- **Dependencies.** KDV-002…KDV-005 surfaces; KDV-010 options.
- **Error conditions.** Insufficient impersonation privilege → classified permission error
  (KDV-009).
- **Security considerations.** Impersonation is privilege-sensitive: identifiers logged only as
  IDs, feature documented with least-privilege guidance.
- **Performance considerations.** Header-only change; none material.
- **Observability.** Caller-object-id tag on activities (as an ID; no PII beyond the GUID).
- **Testing.** Unit: header propagation matrix across operation types. Integration:
  impersonated create shows correct ownership.
- **Docs & examples.** Impersonation guide with security caveats.
- **Acceptance criteria.** Per-request override beats per-client default; header present on
  every operation type when configured.
- **Release classification.** v1.1 (introduced in the 0.5.0 train per master plan §8).

## KDV-014 — File & image column upload/download

- **User problem.** File/image columns require a chunked upload/download protocol that plain
  CRUD cannot express.
- **User story.** As a developer, I want streaming upload/download for file and image columns
  so large binary content moves without buffering entire files in memory.
- **Business value.** Common requirement (documents, images) unserved by most wrappers.
- **Functional requirements.** Chunked streaming upload and download (master plan §3) with
  progress and cancellation; API shape subject to design (stream-based, proposed).
- **Nonfunctional requirements.** Bounded memory regardless of file size; resumability
  evaluated during design.
- **Public API pointer.** Extension of `IDataverseClient` (proposed; subject to design).
- **Configuration.** Chunk size option with a safe default (proposed).
- **Dependencies.** KDV-002, KDV-008 (per-chunk retry semantics), KDV-009.
- **Error conditions.** Interrupted transfer, oversize content, column-type mismatch — all
  classified via KDV-009.
- **Security considerations.** Content treated as opaque bytes; no content sniffing; size
  limits respected.
- **Performance considerations.** Chunk pipeline avoids large-object-heap allocations.
- **Observability.** Transfer activities with byte-count tags; transfer-duration histogram.
- **Testing.** Unit: chunking math, retry-per-chunk, cancellation mid-transfer. Integration:
  round-trip a multi-chunk file.
- **Docs & examples.** File column recipe with streaming source/sink.
- **Acceptance criteria.** Round-trip fidelity byte-for-byte; memory bounded during large
  transfers; mid-transfer cancellation prompt.
- **Release classification.** v1.1 (introduced in the 0.5.0 train per master plan §8).

## KDV-015 — Organization Service transport package

- **User problem.** Some organizations mandate `IOrganizationService` semantics or depend on
  behaviors only the official client provides.
- **User story.** As an enterprise developer, I want an optional adapter package over
  `Microsoft.PowerPlatform.Dataverse.Client` so I can adopt the Koras programming model where
  policy requires the official transport.
- **Business value.** Adoption bridge for conservative enterprises and mixed estates.
- **Functional requirements.** `Koras.Dataverse.OrganizationService` package (net8.0) adapting
  the Koras abstractions over the official client (master plan §2); heavy Microsoft
  dependencies isolated entirely in this package.
- **Nonfunctional requirements.** Zero impact on core package dependency graph; documented
  behavioral differences vs the Web API transport.
- **Public API pointer.** Implements `Koras.Dataverse.Abstractions` interfaces
  (`IDataverseClient` et al.); adapter-specific registration subject to design.
- **Configuration.** Own options for the underlying ServiceClient (subject to design).
- **Dependencies.** `Microsoft.PowerPlatform.Dataverse.Client` (heavy MS deps, master plan §2).
- **Error conditions.** Official-client faults mapped into the KDV-009 taxonomy on a
  best-effort, documented basis.
- **Security considerations.** Inherits official client's auth paths; Koras guidance still
  applies (no connection strings with secrets in code).
- **Performance considerations.** Documented as carrying the official client's
  characteristics; not the performance-recommended path.
- **Observability.** Same ActivitySource/Meter contract emitted by the adapter layer.
- **Testing.** Unit: adapter mapping. Integration: shared conformance suite run against both
  transports.
- **Docs & examples.** "When to use which transport" guide.
- **Acceptance criteria.** MVP conformance suite passes on the adapter (documented exceptions
  allowed); core packages unchanged.
- **Release classification.** v1.1 (introduced in the 0.5.0 train per master plan §8).

## KDV-016 — Source-generated early-bound models

- **User problem.** Late-bound code loses compile-time safety; classic code generation
  (CrmSvcUtil-style) is clunky and drifts from the schema.
- **User story.** As a developer, I want a Roslyn source generator producing typed models from
  a checked-in metadata snapshot, so I get compile-time safety without runtime reflection or
  external codegen steps.
- **Business value.** Major DX step; foundation for KDV-017.
- **Functional requirements.** Roslyn source generator consuming a metadata snapshot file
  (master plan §3); generated types interoperate with the late-bound `Entity` model; snapshot
  format shared with KDV-018.
- **Nonfunctional requirements.** Deterministic generation; incremental-generator performance
  discipline; generated code nullable-annotated.
- **Public API pointer.** Generator package + generated-type conventions; subject to design.
- **Configuration.** Snapshot path + generation options (naming, filtering) — subject to
  design.
- **Dependencies.** KDV-006 (metadata models), KDV-018 (snapshot format, co-designed).
- **Error conditions.** Malformed/missing snapshot → clear build diagnostics with IDs.
- **Security considerations.** Generator reads only the declared snapshot; no network at build
  time.
- **Performance considerations.** Incremental generator; no measurable build impact on
  unchanged snapshots.
- **Observability.** Build diagnostics only (design-time feature).
- **Testing.** Generator snapshot tests; compilation tests of generated output on all TFMs.
- **Docs & examples.** End-to-end guide: export snapshot → generate → use typed models.
- **Acceptance criteria.** Snapshot in, compiling typed models out, round-tripping through
  `IDataverseClient` operations.
- **Release classification.** v1.2.

## KDV-017 — Fluent strongly typed (LINQ-style) queries

- **User problem.** String-based attribute names in queries defeat refactoring and reviews.
- **User story.** As a developer, I want to express queries against generated types
  (LINQ-style/fluent) so column references are compile-checked.
- **Business value.** Completes the typed story begun by KDV-016.
- **Functional requirements.** Typed query surface translating to the same OData/FetchXML
  execution paths as KDV-003/KDV-004; explicit, documented supported-operator set — no
  pretense of full LINQ translation (see
  [`../product/problem-statement.md`](../product/problem-statement.md)).
- **Nonfunctional requirements.** Translation failures are compile-time or immediate, explicit
  runtime errors — never silently wrong queries.
- **Public API pointer.** Builds on `ODataQuery`/`FetchXml` foundations and KDV-016 output;
  subject to design.
- **Configuration.** None planned.
- **Dependencies.** KDV-016 (hard dependency, master plan §3), KDV-003, KDV-004.
- **Error conditions.** Unsupported expression → explicit not-supported error naming the
  construct.
- **Security considerations.** Inherits builder encoding guarantees; typed layer adds no string
  interpolation paths.
- **Performance considerations.** Translation cached where expressions allow.
- **Observability.** Same query telemetry as KDV-003/KDV-004.
- **Testing.** Translation matrix tests (supported + explicitly rejected constructs);
  integration parity checks against builder-produced queries.
- **Docs & examples.** Supported-operator reference; migration notes from late-bound queries.
- **Acceptance criteria.** Documented operator set translates correctly; everything outside it
  fails loudly and clearly.
- **Release classification.** v1.2.

## KDV-018 — Metadata snapshot export + environment comparison

- **User problem.** Teams cannot easily answer "what schema does this environment actually
  have, and how does it differ from that one?"
- **User story.** As a platform engineer, I want to export a metadata snapshot and diff two
  snapshots/environments, CLI-friendly, so drift is visible in pipelines.
- **Business value.** Extends metadata automation (UC-2); feeds KDV-016's snapshot input.
- **Functional requirements.** Snapshot export to a stable, versioned file format; comparison
  producing a structured diff; CLI-friendly operation (master plan §3).
- **Nonfunctional requirements.** Deterministic snapshot serialization (diffable in git).
- **Public API pointer.** Builds on `IMetadataClient` (KDV-006); snapshot/diff types subject to
  design.
- **Configuration.** Scope filters (tables, publishers) — subject to design.
- **Dependencies.** KDV-006; format co-designed with KDV-016.
- **Error conditions.** Snapshot version mismatch → explicit, versioned error.
- **Security considerations.** Snapshots contain schema, not data; documented as such for
  handling policies.
- **Performance considerations.** Paged metadata reads; snapshots streamed to disk.
- **Observability.** Standard metadata-call telemetry; export summary logging.
- **Testing.** Format round-trip tests; diff correctness matrix; integration export of a real
  environment.
- **Docs & examples.** Drift-detection pipeline recipe.
- **Acceptance criteria.** Export → re-export of unchanged environment yields empty diff;
  seeded changes are detected and categorized.
- **Release classification.** v1.2.

## KDV-019 — Solution dependency analysis

- **User problem.** Solution imports fail late due to missing dependencies; dependency data is
  hard to query and interpret.
- **User story.** As a release manager, I want to analyze solution dependencies before
  deployment so failures are predicted, not discovered.
- **Business value.** De-risks the deployment automation scenario (UC-3).
- **Functional requirements.** Dependency retrieval and analysis for solutions/components;
  report of missing dependencies against a target environment. Detailed scope to be designed in
  the 2.0 window.
- **Nonfunctional requirements.** Read-only; safe against partially readable dependency data.
- **Public API pointer.** Extends `Koras.Dataverse.Solutions`; subject to design.
- **Configuration.** Subject to design.
- **Dependencies.** KDV-007; possibly KDV-006 for component metadata.
- **Error conditions.** Classified via KDV-009; incomplete server data handled tolerantly.
- **Security considerations.** Read-only analysis; no elevated requirements beyond KDV-007.
- **Performance considerations.** Dependency graphs can be large; paged retrieval and lazy
  expansion.
- **Observability.** Standard client telemetry.
- **Testing.** Fixture-driven graph analysis tests; integration against solutions with known
  dependencies.
- **Docs & examples.** Pre-flight deployment check recipe.
- **Acceptance criteria.** Known missing dependency detected before an import that would fail.
- **Release classification.** v2.0.

## KDV-020 — Power Pages Web API helpers

- **User problem.** Power Pages exposes its own Web API surface with portal-specific auth and
  conventions; server-side code targeting it lacks SDK support.
- **User story.** As a Power Pages developer, I want helpers for the portal Web API surface so
  companion services and portal-side integrations share the Koras programming model.
- **Business value.** Serves the Power Pages persona (see
  [`../product/personas.md`](../product/personas.md)) beyond what the core client covers.
- **Functional requirements.** To be designed in the 2.0 window against the then-current Power
  Pages API surface; master plan commits the feature, not the shape.
- **Nonfunctional requirements.** Consistency with core client conventions.
- **Public API pointer.** Subject to design.
- **Configuration.** Subject to design.
- **Dependencies.** KDV-001…KDV-003, KDV-008…KDV-011 foundations.
- **Error conditions.** Normalized via KDV-009.
- **Security considerations.** Portal-facing auth is security-critical; will require its own
  threat-model addendum.
- **Performance considerations.** Subject to design.
- **Observability.** Same telemetry contract.
- **Testing.** Subject to design; integration requires a Power Pages test site.
- **Docs & examples.** Companion-API recipe (UC-5) upgraded to first-class helpers.
- **Acceptance criteria.** Defined with the design; feature gate per master plan §8 (scope
  creep risk).
- **Release classification.** v2.0.

## KDV-021 — ALM pipeline helpers

- **User problem.** Teams scripting ALM flows in .NET re-implement orchestration around
  export/import/publish and environment operations.
- **User story.** As a DevOps engineer, I want higher-level ALM building blocks over the
  solution client so common pipeline flows are a few calls.
- **Business value.** Extends UC-3 to full pipeline scenarios while staying an SDK — replacing
  pac CLI remains a non-goal (master plan §1).
- **Functional requirements.** To be designed in the 2.0 window; candidate scope: multi-step
  deployment flows composed from KDV-007/KDV-019 primitives.
- **Nonfunctional requirements.** Long-running-flow cancellation and resumability
  considerations.
- **Public API pointer.** Subject to design.
- **Configuration.** Subject to design.
- **Dependencies.** KDV-007, KDV-019.
- **Error conditions.** Normalized via KDV-009 with step context.
- **Security considerations.** Pipeline credentials guidance (service principals per
  environment).
- **Performance considerations.** Dominated by server-side job durations; polling discipline
  per KDV-007.
- **Observability.** Flow-level activities spanning steps.
- **Testing.** Flow state-machine unit tests; integration on a disposable environment pair.
- **Docs & examples.** End-to-end pipeline sample.
- **Acceptance criteria.** Defined with the design; gated against the out-of-scope list.
- **Release classification.** v2.0.

## KDV-022 — Plugin development helpers

- **User problem.** Plugin authors on .NET Framework cannot use modern SDK niceties; today they
  hand-write FetchXML strings inside plugins.
- **User story.** As a plugin developer, I want helpers — starting from the dependency-free
  netstandard2.0 FetchXML builder — usable inside the plugin sandbox.
- **Business value.** Extends Koras value into the plugin world without shipping a plugin
  *execution* runtime (explicitly out of scope, master plan §1).
- **Functional requirements.** Builds on the `Koras.Dataverse.FetchXml` netstandard2.0 base
  (master plan §3); additional helper scope to be designed in the 2.0 window within sandbox
  constraints.
- **Nonfunctional requirements.** Sandbox-compatible: no reflection emit, no I/O, no
  disallowed dependencies.
- **Public API pointer.** `Koras.Dataverse.FetchXml` plus future helper package; subject to
  design.
- **Configuration.** None (sandbox).
- **Dependencies.** KDV-004's standalone builder package.
- **Error conditions.** Builder-level validation only.
- **Security considerations.** Must not weaken sandbox isolation expectations.
- **Performance considerations.** Plugin execution budgets are tight; helpers must be
  allocation-frugal.
- **Observability.** Limited to what the sandbox permits (tracing service patterns —
  subject to design).
- **Testing.** netstandard2.0/net462-consumer compilation tests; sandbox-constraint
  architecture tests.
- **Docs & examples.** Plugin-side FetchXML building guide.
- **Acceptance criteria.** Helper assemblies load and run inside the sandbox; no dependency
  violations.
- **Release classification.** v2.0.

## KDV-023 — Elastic table / long-running operation helpers

- **User problem.** Elastic tables (NoSQL-backed) and long-running platform operations have
  semantics (partitioning, eventual consistency, async job patterns) that the standard paths
  don't express.
- **User story.** As a developer using elastic tables or long-running operations, I want
  helpers that make their special semantics explicit rather than accidental.
- **Business value.** Forward-looking coverage of newer platform capabilities.
- **Functional requirements.** Exploratory: partition-aware operations, long-running-operation
  polling patterns generalized from KDV-007. Contracted only when platform behavior is stable
  enough to test against.
- **Nonfunctional requirements.** Clearly labeled experimental; exempt from compatibility
  guarantees until promoted.
- **Public API pointer.** Subject to design; shipped under explicit preview labeling.
- **Configuration.** Subject to design.
- **Dependencies.** KDV-002, KDV-008, KDV-009.
- **Error conditions.** Normalized via KDV-009; eventual-consistency caveats documented.
- **Security considerations.** No additional surface expected beyond core.
- **Performance considerations.** Partition-key correctness is the performance story;
  documentation-led.
- **Observability.** Standard telemetry with partition tags (no data values).
- **Testing.** Gated integration tests against environments with elastic tables enabled.
- **Docs & examples.** Experimental-status guide with explicit stability caveats.
- **Acceptance criteria.** Promotion criteria (stability, test coverage, API review) defined
  before any stable release.
- **Release classification.** Experimental.

---

## Out of scope

UI components, data migration engines, plugin execution runtime, XRM tooling replacements
(pac CLI), Power Automate connectors, on-premises pre-9.x, WCF Organization Service in the core
package (master plan §1, §3). Requests in these areas are declined by policy, with pointers to
the appropriate tools.
