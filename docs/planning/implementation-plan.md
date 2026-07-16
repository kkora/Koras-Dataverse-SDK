# Implementation Plan

> Phased execution plan for the MVP (0.1.0-preview.1), mapping
> [master plan §8](master-plan.md#8-delivery-plan) milestones 0–9 to concrete work with
> entry/exit criteria. Deliverable/validation detail per milestone is in
> [milestones.md](milestones.md); task-level breakdown in [backlog.md](backlog.md);
> quality bars in [definition-of-done.md](definition-of-done.md).

## Ground rules

- Milestones are sequential gates, but work may be pipelined one milestone ahead once
  the current milestone's exit criteria are demonstrably close (no milestone is
  *closed* out of order).
- Every milestone closes only when its exit criteria are met **and** everything it
  shipped meets the task-level Definition of Done — tests and docs land with code, not
  in a later "hardening" IOU. Milestone 8 hardens; it does not backfill.
- Feature ids (KDV-xxx) and package boundaries come from master plan §2–§3 and are not
  renegotiated inside milestones; scope changes go through the plan first.
- The first five implementation tasks named in master plan §8 map to milestones 0–2 and
  are the critical path.

---

## Milestone 0 — Repository foundation

**Goal:** a contributor can clone, build, test (empty suites), and pack on any
supported OS with all quality rails active from commit one.

**Work:**
- Solution layout per master plan §2: `src/Koras.Dataverse.Abstractions/`,
  `src/Koras.Dataverse.FetchXml/`, `src/Koras.Dataverse/`,
  `src/Koras.Dataverse.OpenTelemetry/`, `tests/...` (empty project shells compile).
- `Directory.Build.props` / `Directory.Packages.props` (Central Package Management,
  per-TFM Microsoft.Extensions pins), `.editorconfig`, analyzers (NET analyzers +
  PublicApiAnalyzers wired with empty `PublicAPI.*.txt`), nullable + warnings-as-errors,
  deterministic build + SourceLink, `EnablePackageValidation`.
- CI workflows: PR build/test matrix (Linux+Windows × net8/9/10), pack + validation,
  coverage collection (coverlet) + ratchet plumbing, CodeQL, dependency review,
  Dependabot config, `dotnet list package --vulnerable --include-transitive` step,
  secret scanning/push protection on.
- Community files: README (positioning per master plan §1), LICENSE (MIT present),
  CONTRIBUTING, CODE_OF_CONDUCT, SECURITY.md (private reporting), issue/PR templates,
  CLAUDE.md.
- Package metadata scaffolding (icon, per-package README stubs, tags) so pack output is
  valid from day one.

**Entry criteria:** master plan approved; repository exists.
**Exit criteria:** `dotnet build && dotnet test && dotnet pack` green in CI on all
matrix legs; analyzers and PublicAPI tracking demonstrably fail the build on a probe
violation (verified once, then reverted); all §8/milestone-0 files present; branch
protection + environments configured.

## Milestone 1 — Abstractions & core models

**Goal:** `Koras.Dataverse.Abstractions` complete for MVP: every contract the
implementation and consumers code against.

**Work (KDV-002/009/010 model halves + interfaces):**
- `Entity`, `EntityReference`, `ColumnSet`, `DataverseQueryResult`, `WhoAmIResponse`,
  `UpsertResult` — plain CLR values, records where immutable, `sealed` by default.
- Error model: `DataverseError`, `DataverseErrorCategory`, `DataverseException`
  (`Koras.Dataverse.Errors`).
- Options: `DataverseClientOptions`, `DataverseAuthenticationOptions`,
  `DataverseRetryOptions` with DataAnnotations + HTTPS rule.
- Interfaces: `IDataverseClient`, `IMetadataClient`, `ISolutionClient`,
  `IDataverseTokenProvider`, `IDataverseClientFactory`; batch/metadata/solution model
  types (`Koras.Dataverse.Batches/.Metadata/.Solutions` namespaces).
- Architecture tests project online: zero-dependency rule, namespace layout,
  sealed/abstract, Async+CancellationToken conventions — enforced from here on.

**Entry criteria:** M0 exit; API design doc section for these types drafted from master
plan §4.
**Exit criteria:** Abstractions compiles on all TFMs with zero third-party refs
(arch-test-proven); unit tests for model behavior (entity attribute semantics, error
model construction, options validation) green; `PublicAPI.Unshipped.txt` reviewed
against master plan §4; XML docs on all public members.

## Milestone 2 — Core implementation

**Goal:** a working client: auth, pipeline, CRUD, OData queries, paging, batch —
the SDK's engine (KDV-001, 002, 003, 005, 008, 009 execution halves).

**Work:**
- Token provider: `TokenCredential` adapter, `{environmentUrl}/.default` scope, cache
  with 5-minute early refresh, single-flight, `TimeProvider`-driven.
- Pipeline: named `HttpClient` `"Koras.Dataverse:{name}"`,
  `AuthenticationHandler` → `RetryHandler`; retry with Retry-After/jittered backoff,
  per-request timeout via linked CTS.
- Error mapping: OData payload → `DataverseError` → `DataverseException`, transient
  flags, request ids, size caps.
- CRUD + upsert + alternate keys; `@odata.bind`; POCO mapping; System.Text.Json
  serialization (culture-invariant, no polymorphism).
- `ODataQuery`/`ODataFilterBuilder`/`ODataExpand` with strict literal encoding;
  `QueryAsync` + `QueryAllAsync` (`IAsyncEnumerable`, nextLink paging, streaming).
- `$batch`: payload assembly, change sets, 1000-op guard, per-item parsing,
  continue-on-error.
- Fake `HttpMessageHandler` + fake `TimeProvider` test infrastructure; the bulk of the
  unit/component suites from the [test matrix](../testing/test-matrix.md) for these
  features, including the hostile-input encoding corpus and redaction tests.

**Entry criteria:** M1 exit.
**Exit criteria:** all KDV-001/002/003/005/008/009 unit+component suites green
(happy/invalid/boundary/cancellation/failure per test matrix); encoding corpus green;
no real-network or real-clock waits anywhere in the suites; coverage on
`Koras.Dataverse` trending toward targets (gate at M8).

## Milestone 3 — DI & configuration

**Goal:** the documented registration story (KDV-010): `AddDataverse`, named clients,
factory, startup validation.

**Work:** `AddDataverse` (default + named), options binding from `IConfiguration`,
DataAnnotations + custom validation on start, `IDataverseClientFactory`, singleton
lifetimes, per-name isolation (options, HttpClient, token cache); DI test suite;
configuration-validation suite; sample appsettings shape wired to
[secure-configuration.md](../security/secure-configuration.md).

**Entry criteria:** M2 exit (a client exists to register).
**Exit criteria:** KDV-010 matrix rows green; a console smoke app (throwaway, becomes
the M7 sample seed) configures via appsettings + user-secrets and runs against the fake
handler; misconfiguration fails at host start with actionable messages.

## Milestone 4 — Feature packages

**Goal:** FetchXML builder + execution (KDV-004), metadata client (KDV-006), solution
client (KDV-007).

**Work:**
- `Koras.Dataverse.FetchXml`: fluent builder (`FetchXml.For`, filters, links, ordering,
  paging cookies, `Top`), XML-writer-based generation, netstandard2.0 target, zero
  deps; its own unit test project incl. escaping corpus.
- Execution in core: `FetchAsync`, fetch-all with paging cookies; raw-FetchXML escape
  hatch with documented injection warning.
- `IMetadataClient`: tables, columns, choices (local + global), relationships → typed
  lightweight models.
- `ISolutionClient`: export, import with async-job polling (TimeProvider-driven),
  publish-all, installed-solution query.

**Entry criteria:** M2 exit (pipeline available); M3 not strictly required but
typically done.
**Exit criteria:** KDV-004/006/007 matrix rows green; FetchXml package proves zero-dep
+ ns2.0 build in CI; polling paths fully cancelable with no real-time waits in tests.

## Milestone 5 — ASP.NET Core integration

**Goal:** health checks (KDV-012) and confirmed minimal-API ergonomics.

**Work:** `AddDataverseHealthCheck()` (WhoAmI probe; healthy/degraded/unhealthy mapping,
no secrets in descriptions), health-check test suite; minimal API wiring exercise that
seeds the M7 sample; any DI ergonomics fixes it exposes.

**Entry criteria:** M3 exit.
**Exit criteria:** KDV-012 matrix rows green; minimal API host wires
`AddDataverse` + health check with the documented three lines and `/health` behaves
correctly against the fake handler.

## Milestone 6 — Observability

**Goal:** KDV-011 complete: logging categories, `ActivitySource "Koras.Dataverse"`,
`Meter`, plus the `Koras.Dataverse.OpenTelemetry` helper package.

**Work:** activity emission at the client layer (wrapping retries, per master plan §5),
counters/histograms, structured logging with stable property names and the
[data-protection](../security/data-protection.md) redaction/tag rules; no-listener
zero-cost path; OTel package with `TracerProviderBuilder`/`MeterProviderBuilder`
extensions (OpenTelemetry.Api only); observability test suite (activity/metric capture,
redaction, tag policy); arch test: core has no OTel dependency.

**Entry criteria:** M2 exit; ideally after M4/M5 so all operations get instrumented
once.
**Exit criteria:** KDV-011 matrix rows green; tag/log policy tests green; OTel package
consumption verified in the package tests.

## Milestone 7 — Samples & documentation

**Goal:** the adoption surface: runnable samples and the full docs tree.

**Work:** `samples/` console, minimal API, worker (all restore against local pack
output); five-minute quick start; recipes for throttling/paging/batch/metadata pain
points; comparison guide vs ServiceClient; feature docs per KDV-001..012; XML docs
completeness sweep; docs cross-check against every planning/security/testing doc.

**Entry criteria:** M5 + M6 exits (samples show health checks + telemetry).
**Exit criteria:** all three samples build and run in CI against packed bits; quick
start timed at ≤ 5 minutes by someone who didn't write it; docs review sign-off; every
MVP feature's ships-with list (master plan §3) satisfied.

## Milestone 8 — Hardening

**Goal:** security review, performance baseline, live-environment proof, analyzer
tightening — release-quality bar.

**Work:** integration test suite completed and run against a real environment
([integration-testing.md](../testing/integration-testing.md)); nightly workflow on;
full [threat model](../security/threat-model.md) review against implemented code;
[security checklist](../security/security-checklist.md) dry run; benchmark suites
implemented ([benchmarks.md](../performance/benchmarks.md)) and baseline captured;
coverage gate enforced at ≥ 80 %/70 %; analyzer severities tightened to final levels;
fuzz-ish corpus expansion for parsers (error payloads, batch multiparts).

**Entry criteria:** M7 exit (feature-complete MVP).
**Exit criteria:** live suite green in nightly CI; security dry-run findings fixed or
tracked with owners; benchmark baseline archived; coverage gate on and met; zero
analyzer suppressions without justification.

## Milestone 9 — Packaging & release

**Goal:** ship `0.1.0-preview.1`.

**Work:** release workflow implemented end-to-end per
[release-process.md](../release/release-process.md) (tag-triggered: verify → build/test
→ live gate → benchmarks → pack → sign-when-configured → environment-gated publish →
GitHub Release with notes/symbols/SBOMs); `nuget-release` environment + secrets per
[nuget-publishing.md](../release/nuget-publishing.md); NuGet.org org/API key setup;
dry-run against a local feed; changelog; execute the
[release](../release/release-checklist.md) + [security](../security/security-checklist.md)
checklists; tag and publish; post-release verification; file `Koras.*` prefix
reservation.

**Entry criteria:** M8 exit.
**Exit criteria:** `0.1.0-preview.1` installable from NuGet.org, listing correct,
symbols resolve, checklists archived, feedback window announced. Post-exit: collect API
feedback toward `0.1.0` per the release plan.

---

## Cross-milestone tracking

- Progress is tracked per backlog task id (KDV-xxx-Tn) on the GitHub project board;
  a milestone's exit review walks its tasks against
  [definition-of-done.md](definition-of-done.md).
- Risks are re-reviewed at every milestone exit against [risks.md](risks.md) triggers.
- Any scope or API change discovered mid-milestone goes back through the master plan
  (and an ADR when architectural) before implementation continues — the master plan
  stays the source of truth.
