# Feature Planning — KDV-003 OData Queries

> Planning-level design document (pre-implementation). Catalog entry:
> [`feature-catalog.md`](feature-catalog.md#kdv-003--odata-query-builder--execution--auto-paging).
> Source of truth: [`docs/planning/master-plan.md`](../planning/master-plan.md) §3 (KDV-003),
> §4, §5, §7. Release classification: **MVP**.

## Overview

KDV-003 provides the primary read path: a fluent, injection-safe OData query builder
(`ODataQuery`, `ODataFilterBuilder`, `ODataExpand`), single-page execution returning
`DataverseQueryResult`, and `QueryAllAsync` streaming full result sets as
`IAsyncEnumerable<Entity>` with automatic `@odata.nextLink` continuation. Injection safety via
strict OData literal encoding is a headline differentiator (master plan §1).

## Requirements

**Functional**

1. Builder entry `ODataQuery.For("account")` with fluent members per master plan §4:
   `Select(...)`, `Where(f => ...)` using `ODataFilterBuilder` (e.g., `f.Eq("statecode", 0)`),
   `OrderBy(...)`, and expansion via `ODataExpand`.
2. Filter builder covers the standard comparison and composition operators (equality,
   inequality, ranges, string functions, and/or/not grouping — exact operator list finalized in
   implementation; `Eq` is fixed by the plan sample).
3. Every filter value is encoded per OData literal rules (strings quoted and escaped, GUIDs,
   dates, numbers, booleans typed correctly). No raw-string filter injection API in MVP.
4. Single-page execution returns `DataverseQueryResult` (entities + continuation state).
5. `IDataverseClient.QueryAllAsync(query, ct)` yields `IAsyncEnumerable<Entity>` across all
   pages, following `@odata.nextLink` until exhausted (master plan §4).
6. Builders are mutable until built/executed and documented as not thread-safe (master plan
   §5).

**Nonfunctional.** Deterministic query-string output; bounded memory during streaming;
cancellation observed between and within page fetches.

## Proposed public API

Fixed by master plan §4:

```csharp
public IAsyncEnumerable<Entity> ActiveAccounts(CancellationToken ct) =>
    dataverse.QueryAllAsync(
        ODataQuery.For("account").Select("name", "revenue")
            .Where(f => f.Eq("statecode", 0)).OrderBy("name"), ct);
```

Types: `ODataQuery`, `ODataFilterBuilder`, `ODataExpand` in `Koras.Dataverse.Queries`;
`DataverseQueryResult` in `Koras.Dataverse` (master plan §4).

Conservative proposal, subject to implementation: a single-page execution method on
`IDataverseClient` (e.g., `QueryAsync(ODataQuery, CancellationToken)` returning
`DataverseQueryResult` with a continuation for manual paging) complements `QueryAllAsync`;
additional builder members (`Top`, `Filter` grouping helpers, `Expand(...)` overloads taking
`ODataExpand`) follow the same fluent pattern.

## Configuration

- Default page size (`odata.maxpagesize` preference) with per-query override — proposed,
  subject to implementation; server limits always win.
- No other configuration; encoding behavior is not configurable by design.

## Error conditions

| Condition | Behavior |
|---|---|
| Invalid builder usage detectable client-side (e.g., empty entity set name) | Immediate `ArgumentException`-family failure at builder/build time, before I/O |
| Server rejects query (bad column, malformed expand) | `DataverseException` via KDV-009 with Dataverse code and request id |
| Throttling mid-enumeration | Handled by KDV-008 below the surface; enumeration continues after retry |
| Cancellation mid-stream | `OperationCanceledException` propagated promptly; no further page fetched |
| nextLink loop/malformed continuation | Defensive guard fails the enumeration with a classified error (subject to implementation) |

## Security

- **Injection safety is the contract.** All values pass through OData literal encoding; there
  is no public API that splices caller strings into filter expressions unencoded (master plan
  §7).
- Column and entity names are validated/encoded as identifiers.
- Query strings may appear in traces/logs at debug level; documentation warns that filter
  values can be business data and points at logging configuration.

## Performance

- Streaming: one page in memory at a time; `QueryAllAsync` never materializes the full result
  set.
- Query-string construction allocates proportionally to query complexity; builders may be
  reused for repeated execution only as documented (not thread-safe).
- `Select` strongly encouraged in docs and samples to limit payload width.

## Observability

- One `Activity` per page request, child of the logical operation, tagged with entity set and
  page index; row counts added where cheap (KDV-011).
- Metrics: query counter, per-page duration histogram; pages-per-query histogram is a
  candidate, subject to implementation.
- No filter values in tags by default.

## Test plan

**Unit** (builder + fake `HttpMessageHandler`):
- Builder output snapshots for every operator/combination; property ordering determinism.
- Encoding matrix: quotes and apostrophes in strings, GUIDs, `DateTimeOffset`, numbers,
  booleans, unicode; verified against expected OData literals.
- Injection attempts (`' or 1 eq 1`, `&$expand=...`, `%00`) remain inert data.
- Paging: multi-page fake responses; continuation followed; enumeration ends at last page.
- Cancellation: mid-stream cancel stops before next page request (asserted on handler).
- Error mapping for 400-class responses.

**Integration** (env-var gated): multi-page query against a seeded table (paging covered per
master plan §6); `Select`/`OrderBy`/filter round-trip correctness.

## Acceptance criteria

1. The master plan §4 sample compiles and runs unmodified.
2. Builder output is byte-stable for a given builder state.
3. No hostile filter value can change query semantics (encoding test matrix passes).
4. `QueryAllAsync` returns exactly the union of all pages, in server order, with bounded
   memory.
5. Cancellation mid-enumeration issues no further HTTP requests.
6. Error, cancellation, and paging behavior covered by tests per the MVP bar.
