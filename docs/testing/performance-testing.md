# Performance Testing

> Planning document. Defines how SDK performance is measured and how regressions are
> caught. Companion documents: [../performance/benchmarks.md](../performance/benchmarks.md)
> (suite definitions) and
> [../performance/performance-guide.md](../performance/performance-guide.md) (consumer
> guidance). No benchmark numbers appear in this repository until they are produced by the
> published methodology; we do not fabricate results.

## 1. What performance means for this SDK

End-to-end throughput against Dataverse is dominated by the service itself (network
latency plus service protection limits — see the performance guide). The SDK's measurable
responsibilities are therefore **client-side CPU and allocation costs**:

- Query construction: FetchXML XML generation, OData query-string generation.
- Serialization: `Entity` → request JSON, response JSON → `Entity`/models.
- Error parsing: OData error payload → `DataverseError`.
- Batch payload assembly and multipart response parsing.
- Pipeline overhead per request: auth header attachment (cached-token path), retry
  handler bookkeeping, activity/metric emission when no listener is attached.

Goals (directional, enforced as regression policy rather than absolute numbers):

1. The no-listener telemetry path and the cached-token auth path add near-zero
   allocation per request.
2. Builders and serialization scale linearly with input size, with no accidental
   quadratic string concatenation.
3. Paging via `QueryAllAsync` holds memory proportional to one page, never the full
   result set ([../performance/memory-management.md](../performance/memory-management.md)).

## 2. Tooling: BenchmarkDotNet

Project: `benchmarks/Koras.Dataverse.Benchmarks` (console app, Release-only, excluded from
`dotnet test`).

- **BenchmarkDotNet** with `[MemoryDiagnoser]` on every benchmark class — allocated
  bytes/op and Gen0/Gen1/Gen2 collection counts are first-class results, not an
  afterthought. For pipeline benchmarks, `ThreadingDiagnoser` is added where lock
  contention could hide.
- Jobs: one job per shipped TFM (net8.0, net9.0, net10.0) so runtime-specific
  regressions (e.g., a net10.0 JIT change) are visible. FetchXml additionally gets a
  .NET Framework 4.6.2 job on Windows runs to keep the ns2.0 path honest.
- I/O-free by construction: benchmarks that involve the HTTP pipeline use the same fake
  `HttpMessageHandler` seam as component tests, returning canned in-memory responses, so
  results measure SDK code rather than sockets.
- Inputs are parameterized over realistic sizes (`[Params]`): e.g., 5/25/100 attributes
  per entity, 1/10/100 filter conditions, 10/100/1000 batch operations, 1/50 KB error
  payloads. Suites and exact cases are defined in
  [../performance/benchmarks.md](../performance/benchmarks.md).

## 3. Execution model

| Context | What runs | Purpose |
|---|---|---|
| Local, on demand | `dotnet run -c Release --project benchmarks/Koras.Dataverse.Benchmarks -- --filter '*'` | Development and optimization work |
| PR with label `performance` | Affected suites, results posted as PR artifact | Evaluate perf-sensitive changes before merge |
| Per release (milestone 8 onward) | Full suite on a fixed CI runner class, results archived | The published per-release record |

Benchmarks are **not** part of the default PR gate: shared CI runners are too noisy for
millisecond-level assertions, and false-positive gates train people to ignore red.
Regression control instead works as follows.

## 4. Regression policy

1. **Baseline artifacts.** Each release's full BenchmarkDotNet results (JSON + Markdown
   exports, plus environment metadata: runner class, OS, CPU, .NET versions) are stored
   under the corresponding GitHub Release. These are the baselines.
2. **Comparison on demand.** Perf-sensitive PRs (anything touching serialization,
   builders, the pipeline handlers, or error parsing — enforced by a CI path filter that
   auto-applies the `performance` label) run the affected suites twice on the same
   runner: base commit and head commit, interleaved. Same-machine, same-run comparison
   sidesteps most runner variance.
3. **Thresholds.**
   - **Allocation regressions are strict:** any increase in allocated bytes/op on a hot
     path (cached-token request path, per-row deserialization, per-page paging step,
     no-listener telemetry) must be justified in the PR or fixed. Allocation counts are
     stable across runners, so this gate is reliable.
   - **Time regressions are advisory at ±10 %** on same-machine comparisons: a >10 %
     mean regression on any suite requires an explanation in the PR (accepted trade-off,
     measurement noise with evidence, or a fix).
4. **Release gate.** The release checklist requires: full suite ran, results compared
   against the previous release's baseline, and any regression beyond the thresholds
   either fixed or explicitly accepted in the release notes. A release never ships with
   an unexplained hot-path allocation regression.
5. **No fabricated numbers.** Documentation and README never quote performance figures
   that are not reproducible from an archived benchmark run with its methodology.

## 5. Micro-optimization guardrails

- Optimize only what a benchmark demonstrates; PRs claiming performance motivation must
  attach before/after BenchmarkDotNet output.
- Readability wins ties: an optimization that saves nothing measurable but complicates
  code is rejected.
- `unsafe`, pooling, and `stackalloc` require both a demonstrated win and a note in the
  affected code; pooling policy lives in
  [../performance/memory-management.md](../performance/memory-management.md).
