# Benchmarks

> Planned benchmark suites for the Koras Dataverse SDK. **This document contains no
> performance numbers, and none may be added to it by hand.** Results are produced by
> the published methodology and attached to each GitHub Release; fabricating or
> hand-copying numbers into docs is prohibited (see
> [performance-testing.md](../testing/performance-testing.md) for the regression
> policy).

## 1. Methodology

- **Tool:** BenchmarkDotNet, Release configuration, project
  `benchmarks/Koras.Dataverse.Benchmarks` (console runner; excluded from `dotnet test`).
- **Diagnosers:** `[MemoryDiagnoser]` on every class — allocated bytes/op and GC
  collection counts are reported alongside time for every benchmark, always.
  `ThreadingDiagnoser` on pipeline/concurrency suites.
- **Jobs:** one per shipped TFM — net8.0, net9.0, net10.0; FetchXml suites additionally
  run a .NET Framework 4.6.2 job on Windows to exercise the netstandard2.0 asset.
- **No network:** pipeline suites use the fake `HttpMessageHandler` seam with canned
  in-memory responses, so results measure SDK code, not sockets.
- **Environment disclosure:** every published run includes BenchmarkDotNet's
  environment banner (CPU, OS, runtime versions) plus the CI runner class; results are
  only comparable within like environments, and publications say so.
- **Publication:** per release, full JSON + Markdown exports are attached to the GitHub
  Release ([release-process.md](../release/release-process.md)). Release notes may
  summarize *changes versus the previous release's baseline on the same runner class*,
  linking the raw artifacts.

## 2. Planned suites

Benchmarks measure the public API surface defined in master plan §4 — no
benchmark-only internals are exposed to make numbers look better.

### 2.1 `FetchXmlBuildBenchmarks`

FetchXML builder → XML string (`FetchXml.For(...)...Build()`), the hot path for
plugin-embedded usage.

| Case | Parameters |
|---|---|
| Minimal query (entity + 3 attributes) | — |
| Filtered query | 1 / 10 / 100 conditions, mixed operators |
| Nested filters (And/Or trees) | depth 2 / 4 |
| Link-entities | 1 / 3 / 5 links with aliases + attributes |
| Full composite (attributes + filters + links + order + top) | small / large |
| Escaping-heavy values (quotes, angle brackets, non-ASCII) | — |

### 2.2 `ODataQueryBuildBenchmarks`

`ODataQuery` → encoded query string.

| Case | Parameters |
|---|---|
| Select-only | 3 / 25 columns |
| Filter encoding | 1 / 10 / 100 conditions; string vs GUID vs date vs decimal literals |
| Order + top + count combinations | — |
| Expand | 1 / 3 expands with nested selects |
| Escaping-heavy string values | — |

### 2.3 `EntitySerializationBenchmarks`

`Entity` → request JSON, and response JSON → `Entity`; plus attribute-based POCO
mapping (KDV-002) both directions.

| Case | Parameters |
|---|---|
| Serialize entity | 5 / 25 / 100 attributes; mixed CLR types |
| Serialize with lookups (`@odata.bind`) | 1 / 5 lookups |
| Deserialize single entity | 5 / 25 / 100 attributes; with `@odata.*` annotations present |
| Deserialize query page | 50 / 500 rows per page |
| POCO map to/from `Entity` | 5 / 25 properties |

### 2.4 `ErrorParsingBenchmarks`

Non-success response body → `DataverseError` (KDV-009).

| Case | Parameters |
|---|---|
| Standard OData error payload | 1 KB |
| Nested inner errors | depth 3 |
| Large payload (cap behavior) | 50 KB |
| Non-JSON body fallback | HTML page |
| Service-protection 429 payload with Retry-After | — |

### 2.5 `BatchPayloadBenchmarks`

`BatchRequest` → multipart payload; multipart response → `BatchResponse` (KDV-005).

| Case | Parameters |
|---|---|
| Assemble batch payload | 10 / 100 / 1000 operations; with/without change sets |
| Parse batch response | 10 / 100 / 1000 item results; all-success vs mixed-error |
| Content-ID reference resolution | 10 chained operations |

### 2.6 `PipelineOverheadBenchmarks`

Per-request SDK overhead through the real handler chain against the fake transport.

| Case | Parameters |
|---|---|
| CRUD round trip, cached token, no telemetry listener | — (the headline hot path) |
| Same, with an `ActivityListener` + metrics listener attached | — |
| Token cache hit vs single-flight refresh under 1 / 16 concurrent callers | — |
| Retry path: 2×429-then-success (fake `TimeProvider`, zero real delay) | — |
| `QueryAllAsync` per-page step overhead | page size 50 |

## 3. Reading the results

- **Allocated bytes/op** is the primary regression signal (stable across machines);
  time is secondary and only compared same-machine
  ([performance-testing.md §4](../testing/performance-testing.md)).
- The no-listener telemetry case (§2.6) exists to prove observability is pay-for-play;
  the listener-attached case documents the real cost of enabling it.
- Suites deliberately overlap the unit-test hot paths so a regression flagged by a
  benchmark has a matching test area to fix against.

## 4. Adding a benchmark

New public-surface hot paths (e.g., file columns in v1.1) must add a suite in the same
PR that introduces the feature's implementation milestone, with `[MemoryDiagnoser]`,
parameterized realistic sizes, and no network. Benchmarks are code-reviewed like
product code.
