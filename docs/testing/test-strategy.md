# Test Strategy

> Expands [master plan §6](../planning/master-plan.md#6-testing-strategy-summary). This is a
> pre-implementation planning document: it defines the tests we will write, not tests that
> already exist. If it conflicts with the master plan, the master plan wins.

## 1. Goals

1. Every MVP feature (KDV-001..KDV-012) ships with unit tests, error-path tests, and
   cancellation tests before it is considered done (see
   [definition-of-done.md](../planning/definition-of-done.md)).
2. Tests document behavior: a reader should be able to learn the SDK's contract (encoding
   rules, retry semantics, error taxonomy) from test names alone.
3. The suite must run fast and deterministically on any contributor machine and in CI with
   **no network access and no Dataverse environment** — live-environment coverage is a
   separate, opt-in layer ([integration-testing.md](integration-testing.md)).
4. Coverage is a ratchet, not a vanity number: ≥ 80 % line / ≥ 70 % branch on
   `Koras.Dataverse` + `Koras.Dataverse.FetchXml`, with meaningful-behavior coverage
   prioritized over line-count chasing.

## 2. Test pyramid

```
        ┌────────────────────────┐
        │  Live integration      │  opt-in, env-var gated, ~dozens of tests
        ├────────────────────────┤
        │  Package consumption   │  per release / per CI pipeline, per-TFM
        ├────────────────────────┤
        │  Contract + API compat │  PublicAPI analyzer, package validation, arch tests
        ├────────────────────────┤
        │  Component (fake HTTP) │  client-level tests through the real pipeline
        ├────────────────────────┤
        │  Unit                  │  builders, mapping, options, parsing — the bulk
        └────────────────────────┘
```

The wide base is pure unit tests with zero I/O. The component layer exercises the real
`HttpClient` pipeline (authentication handler → retry handler) against a fake
`HttpMessageHandler`, so serialization, header handling, retry timing, and error mapping are
tested exactly as they run in production — minus the network.

## 3. Test projects

| Project | Purpose |
|---|---|
| `tests/Koras.Dataverse.UnitTests` | Unit + component tests for `Koras.Dataverse` and `Koras.Dataverse.Abstractions` |
| `tests/Koras.Dataverse.FetchXml.UnitTests` | FetchXML builder tests (multi-targeted, including netstandard2.0 consumption via net4x/net8 test TFMs) |
| `tests/Koras.Dataverse.ArchitectureTests` | NetArchTest.Rules structural rules |
| `tests/Koras.Dataverse.IntegrationTests` | Live-environment tests, skipped unless configured |
| `tests/Koras.Dataverse.PackageTests` | Consume the packed `.nupkg` from a local feed, compile + run per TFM |
| `benchmarks/Koras.Dataverse.Benchmarks` | BenchmarkDotNet suites (not run as tests; see [performance-testing.md](performance-testing.md)) |

## 4. Test categories

Categories are applied with xUnit traits (`[Trait("Category", "…")]`) so CI can slice runs.

### 4.1 Unit

Pure in-memory tests of a single type or small cluster. Primary targets:

- **Builders:** `ODataQuery`, `ODataFilterBuilder`, `ODataExpand`, `FetchXml`/`FetchXmlQuery`,
  `FetchFilterBuilder`, `FetchLinkEntityBuilder` — output strings/XML are asserted exactly.
- **Encoding:** OData literal encoding (strings, GUIDs, dates, decimals, enums, null) and
  FetchXML XML escaping. These are the injection-safety tests demanded by the threat model.
- **Entity model:** `Entity`, `EntityReference`, plain CLR value conversion, `@odata.bind`
  generation for lookups, alternate-key addressing, typed POCO mapping.
- **Error mapping:** OData error payload → `DataverseError` (category, code, HTTP status,
  request id, transient flag), including malformed and empty payloads.
- **Batch:** multipart payload generation, change-set atomicity markers, 1000-operation
  guard, per-item response parsing (`BatchItemResult`), continue-on-error semantics.
- **Options:** `DataverseClientOptions` DataAnnotations validation, HTTPS enforcement,
  authentication option helpers.

### 4.2 Integration (live)

Opt-in tests against a real Dataverse environment; see
[integration-testing.md](integration-testing.md). Not part of the default CI gate.

### 4.3 Contract

Tests that pin the SDK's *wire contract* without a live server:

- Request-shape tests: given a client call, the fake handler asserts method, URL (including
  encoded query string), headers (`OData-Version`, `Prefer`, `Authorization` presence — never
  the token value), and body JSON.
- Response-shape tests: recorded/representative Web API v9.2 response payloads (stored as
  test resources) are fed through deserialization and must produce the documented models.
  Payloads are hand-authored from public Web API documentation, not captured secrets.

### 4.4 API compatibility

- `Microsoft.CodeAnalysis.PublicApiAnalyzers` with `PublicAPI.Shipped.txt` /
  `PublicAPI.Unshipped.txt` in every shipped project — any public surface change requires an
  explicit, reviewable diff.
- `EnablePackageValidation` (with a baseline version once 1.0 ships) catches TFM asymmetry
  and binary breaking changes at pack time. Details in
  [compatibility-testing.md](compatibility-testing.md).

### 4.5 Architecture

NetArchTest.Rules assertions run as ordinary xUnit facts:

- `Koras.Dataverse.Abstractions` and `Koras.Dataverse.FetchXml` reference **zero**
  third-party assemblies.
- Dependency direction: implementation → abstractions, never the reverse.
- Public types are `sealed` or `abstract` (per master plan §4).
- All public async methods end in `Async` and expose a `CancellationToken` parameter.
- Namespace layout matches master plan §4 (`Koras.Dataverse`, `.Queries`, `.FetchXml`,
  `.Batches`, `.Metadata`, `.Solutions`, `.Errors`, `.Authentication`).

### 4.6 Serialization

- Round-trip tests for entity JSON: write → parse → identical `Entity` content, covering
  every supported CLR value type (string, int, long, decimal, double, bool, `DateTime`/
  `DateTimeOffset`, `Guid`, `EntityReference`, null).
- Culture invariance: serialization output is identical under `en-US`, `de-DE`, `tr-TR`
  (the classic `I`/`i` and decimal-comma traps).
- Tolerant reading: unknown properties and `@odata.*` annotations in responses never throw.
- No polymorphic deserialization anywhere (security requirement, master plan §7) — asserted
  by architecture test banning the relevant `System.Text.Json` polymorphism attributes and
  `JsonSerializer` settings.

### 4.7 Concurrency / thread safety

- Token provider: N parallel callers during an expired-token window produce exactly **one**
  token request (single-flight refresh), verified with a counting fake credential and a fake
  `TimeProvider`.
- Client singletons: parallel CRUD calls through one client instance against the fake handler
  complete without shared-state corruption (run under high task counts; not a proof, but a
  smoke alarm).
- Builders are documented as not thread-safe; no tests pretend otherwise.

### 4.8 Cancellation

Every public I/O method gets at least:

- Pre-canceled token → `OperationCanceledException` before any request is sent.
- Mid-flight cancellation (fake handler awaits a gate) → `OperationCanceledException`,
  never wrapped in `DataverseException`, never swallowed.
- Cancellation during retry backoff aborts the delay immediately (fake `TimeProvider`).
- `QueryAllAsync` / paging enumerators honor cancellation between pages, and enumerator
  disposal stops further page fetches.

### 4.9 Timeout

- Per-request timeout produces the documented timeout error (distinct from caller
  cancellation), driven by fake `TimeProvider` + linked CTS — no real clock waits.
- Timeout is applied per attempt vs. across retries exactly as
  `DataverseRetryOptions` documents, and the tests pin that choice.

### 4.10 Retry / throttling

- 429/503/504 with `Retry-After` (seconds and HTTP-date forms) → delay honors the header
  exactly; measured via fake `TimeProvider`, zero real waiting.
- Without `Retry-After`: jittered exponential backoff within configured bounds.
- Retry budget exhaustion surfaces the **last** error with `IsTransient = true`.
- Non-transient statuses (400, 401, 403, 404, 412) are never retried.
- Retries occur below error mapping: intermediate failures do not throw.

### 4.11 Error mapping

Table-driven tests: (HTTP status, Dataverse error code, payload shape) →
(`DataverseErrorCategory`, transient flag, request id, message). Includes: service protection
codes on 429, `0x80060891` not-found style codes, concurrency (412), permission (403),
completely unparseable bodies (fallback error preserves raw status and request id header).

### 4.12 Dependency injection

- `AddDataverse` registers `IDataverseClient`, `IMetadataClient`, `ISolutionClient`,
  `IDataverseTokenProvider`, `IDataverseClientFactory` with the documented lifetimes
  (singleton clients).
- Named clients: two registrations resolve independently configured clients via
  `IDataverseClientFactory`; the named `HttpClient` is `"Koras.Dataverse:{name}"`.
- Re-registration and double-`AddDataverse` behavior is pinned.
- `AddDataverseHealthCheck` registers the WhoAmI probe with the health check service.

### 4.13 Configuration validation

- Missing `EnvironmentUrl`, non-HTTPS `EnvironmentUrl`, missing authentication settings, and
  out-of-range retry options fail **at startup** (options validation), not at first call.
- Binding from `IConfiguration` (appsettings shape from
  [secure-configuration.md](../security/secure-configuration.md)) round-trips correctly.

### 4.14 Security-focused

- Injection corpus: hostile inputs (`'`, `"`, `<`, `&`, `%`, newline, Unicode controls,
  OData operators, XML entities) pushed through every builder value position must appear
  fully encoded in output — asserted against expected encodings, plus FetchXML output is
  re-parsed with a secure `XmlReader` to prove well-formedness.
- Log redaction: capture-logger tests assert no `Authorization` header, token, or client
  secret substring ever appears in log output at any level, including exception paths.
- HTTPS enforcement and no cross-host redirect following (fake handler returns 30x to a
  different host; client must not follow) — see
  [threat-model.md](../security/threat-model.md).

### 4.15 Performance regression

BenchmarkDotNet suites tracked per release; not part of the unit test run. Policy and suites
in [performance-testing.md](performance-testing.md) and
[../performance/benchmarks.md](../performance/benchmarks.md).

### 4.16 Package consumption

Packed `.nupkg` files are installed from a local feed into throwaway consumer projects, one
per TFM, which must restore, compile, and run a smoke program (builder construction +
DI registration against a fake handler). Details in
[compatibility-testing.md](compatibility-testing.md).

## 5. Tooling

| Tool | Role | Rationale |
|---|---|---|
| **xUnit** (v3 line) | Test framework | De facto .NET OSS standard; first-class `dotnet test` + parallelization |
| **xUnit built-in assertions** | Assertions | See §5.1 |
| **NSubstitute** | Test doubles for interfaces (`IDataverseTokenProvider`, logging, etc.) | Friendly syntax, actively maintained, BSD-licensed |
| **NetArchTest.Rules** | Architecture tests | Lightweight, assertion-style structural rules |
| **Microsoft Code Coverage collector** (`--collect "Code Coverage;Format=cobertura"`, ships with Microsoft.NET.Test.Sdk) | Coverage | Cross-platform cobertura output, feeds the ratchet. `coverlet.collector` remains referenced as a fallback, but the 10.x line produced empty reports against .NET 10 test hosts, so CI uses the Microsoft collector |
| **BenchmarkDotNet** | Benchmarks | Industry standard; MemoryDiagnoser for allocation tracking |
| **Microsoft.CodeAnalysis.PublicApiAnalyzers** | API surface tracking | Reviewable public API diffs |

Deliberately **not** used: mocking of `HttpClient`-adjacent concrete classes (we fake at the
`HttpMessageHandler` seam instead), auto-fixture-style anonymous data (obscures intent for a
contract-heavy SDK), and testcontainer-style Dataverse emulators (none exist with fidelity
worth trusting).

### 5.1 Assertion library: xUnit built-ins (FluentAssertions rationale)

FluentAssertions changed its license starting with **v8** (January 2025): versions ≥ 8 are
distributed under a proprietary license from Xceed that requires a paid license for
commercial use, replacing the Apache 2.0 license of v7 and earlier. For an MIT-licensed
library intended for broad community contribution this is unacceptable:

- Contributors and forks would need to reason about a commercial license just to run tests.
- Pinning to the v7 line forever means depending on an unmaintained major with no security
  or runtime-support future.
- Assertion needs in this codebase are mostly exact string/XML/JSON equality and typed
  exception checks — xUnit's `Assert.Equal`, `Assert.Throws(Async)`, `Assert.Collection`,
  and a handful of small internal helper extensions (e.g., normalized-XML comparison) cover
  them without any third-party dependency.

Decision: **xUnit built-in assertions only**, plus small project-owned test helpers where
readability demands it. Shouldly/AwesomeAssertions and similar alternatives are not adopted
either — one less dependency to vet (see
[dependency-security.md](../security/dependency-security.md)).

## 6. Fake `HttpMessageHandler` approach

A single reusable test double, `FakeHttpMessageHandler` (test-project-internal), is the seam
for all component-level tests:

- **Scripted responses:** enqueue `(matcher, response factory)` pairs; unmatched requests
  fail the test loudly (no silent 200s).
- **Request capture:** every outgoing `HttpRequestMessage` is recorded with a *copied* body
  (content streams are buffered before the handler disposes them) so assertions can inspect
  method, URI, headers, and payload after the fact.
- **Sequenced failures:** first N responses 429-with-Retry-After, then 200 — the standard
  retry-path script.
- **Gates:** responses can await a `TaskCompletionSource` so tests can cancel mid-flight
  deterministically.
- The handler is installed via the same `IHttpClientFactory` registration path used in
  production (`ConfigurePrimaryHttpMessageHandler` on the named client), so the real
  authentication and retry handlers run in every component test.

No test ever binds to a real socket or localhost server in the unit/component layer.

## 7. Fake `TimeProvider` approach

The SDK takes `TimeProvider` everywhere time matters (retry delays, token expiry — master
plan §5). Tests use a small project-owned fake (`tests/**/FakeTimeProvider.cs`) rather than
adding a package dependency:

- `GetUtcNow()` returns a controllable instant; `Advance(TimeSpan)` moves it.
- `CreateTimer(...)` records due times and fires callbacks when `Advance` crosses them,
  which makes `Task.Delay(delay, timeProvider, ct)` resolve synchronously under test.
- Canceling during a pending fake delay must complete the delay task as canceled —
  this is what makes the cancellation-during-backoff tests (§4.8) run in microseconds.

The fake mirrors the semantics of `Microsoft.Extensions.TimeProvider.Testing`'s
`FakeTimeProvider`; we own a tiny copy to keep the test tree dependency-light (consistent
with master plan §6). If maintaining the fake ever costs more than the dependency, an ADR
can revisit this.

## 8. Coverage targets and policy

- **Targets:** ≥ 80 % line and ≥ 70 % branch on `Koras.Dataverse` and
  `Koras.Dataverse.FetchXml` combined. `Abstractions` is mostly models and interfaces and
  `Koras.Dataverse.OpenTelemetry` is two subscription extensions; both are measured but not
  gated — a deliberate scope choice, not an oversight.
- **Gate active since 2026-07-16:** the `Tests` workflow merges coverlet cobertura output via
  ReportGenerator (filtered to the two gated assemblies) and fails CI below 80 % line or
  70 % branch.
- **Ratchet, not gate-only:** CI records coverage per PR; a PR may not drop coverage below
  the recorded high-water mark by more than a small tolerance (1 percentage point) without
  an explicit maintainer waiver in the PR description. Once the targets are reached they
  become the floor.
- **Meaningful behavior first:** encoding tables, retry timing, error taxonomy, and
  cancellation paths are covered before trivial property getters. Coverage-driven tests
  that assert nothing behavioral are rejected in review.
- Exclusions must be explicit (`[ExcludeFromCodeCoverage]` with a justification comment) and
  are limited to generated code and pure DTO records.

## 9. What is NOT unit tested, and why

| Not unit tested | Why | Covered instead by |
|---|---|---|
| Real Entra ID token acquisition (`Azure.Identity` internals) | Third-party code; mocking MSAL adds no signal | Live integration tests (KDV-001 happy path) |
| Real Dataverse server behavior (actual throttling, actual error payloads over time) | Requires a live tenant; non-deterministic | Live integration tests + pinned contract fixtures |
| TLS negotiation, DNS, proxies | Platform/`SocketsHttpHandler` responsibility | Not our code; HTTPS *enforcement* (our rule) is unit tested |
| `IHttpClientFactory` lifetime internals | BCL behavior | DI tests assert our registration; the factory itself is trusted |
| Wall-clock timing accuracy of backoff | Real sleeps make suites slow and flaky | Fake `TimeProvider` asserts requested delays exactly |
| Benchmark performance numbers | Machine-dependent | BenchmarkDotNet suites with per-release published results |
| Generated NuGet metadata rendering on NuGet.org | External system | Pack validation + post-release verification checklist |

The principle: unit tests own **our** logic and contracts; live integration tests own the
assumption that our contracts match reality; nothing pretends to test code we do not own.

## 10. CI execution model

- Every PR: unit + component + architecture + serialization + DI/configuration + security
  suites on all TFMs (net8.0/net9.0/net10.0), Linux + Windows; coverage ratchet enforced.
- Every PR: pack + package validation + package-consumption smoke (Linux).
- Nightly/manual: live integration suite in a protected environment with repository secrets
  (see [integration-testing.md](integration-testing.md)).
- Release: full matrix + benchmarks snapshot + the
  [release checklist](../release/release-checklist.md).
