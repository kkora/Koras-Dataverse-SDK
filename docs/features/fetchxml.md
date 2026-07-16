# Feature Planning — KDV-004 FetchXML

> Planning-level design document (pre-implementation). Catalog entry:
> [`feature-catalog.md`](feature-catalog.md#kdv-004--fetchxml-builder--execution--paging-cookie-paging).
> Source of truth: [`docs/planning/master-plan.md`](../planning/master-plan.md) §2, §3
> (KDV-004), §4, §5, §7. Release classification: **MVP**.

## Overview

KDV-004 has two halves. First, a **standalone fluent FetchXML builder** shipped as
`Koras.Dataverse.FetchXml` — zero dependencies, targeting netstandard2.0 plus
net8.0/9.0/10.0 so it is usable from Dataverse plugin assemblies on .NET Framework 4.6.2+
(master plan §2). Second, **execution** in the core package via `IDataverseClient.FetchAsync`,
including paging-cookie continuation. All generated XML is strictly escaped; injection through
attribute names or values is designed out.

## Requirements

**Functional**

1. Builder entry `FetchXml.For("account")` with fluent members per master plan §4:
   `Attributes(...)`, `Where(f => ...)` using `FetchFilterBuilder` (composition shown in the
   plan: `f.Eq("statecode", 0).And(a => a.Like("name", "Contoso%"))`), `Link(...)` with
   `FetchLinkEntityBuilder` (`Alias`, `Attributes`), `OrderBy(...)`, `Top(...)`, and `Build()`
   producing a `FetchXmlQuery`.
2. Condition operators enumerated by `FetchConditionOperator`; the builder covers the standard
   FetchXML condition set (equality, likes, ranges, null checks, in-lists — full list finalized
   in implementation; `Eq` and `Like` fixed by the plan sample).
3. Nested filter composition (and/or) and nested link-entities.
4. Execution: `await dataverse.FetchAsync(fetch, ct)` returns a page of results (master plan
   §4); paging-cookie continuation retrieves subsequent pages.
5. The builder package works with raw FetchXML strings as escape hatch input for execution
   (proposed, subject to implementation) — porting existing queries is a primary use case.

**Nonfunctional.** Builder package has **zero** dependencies (master plan §2, enforced by
architecture tests, §6); output XML is well-formed and schema-valid; builder is
mutable-until-`Build`, not thread-safe, documented (master plan §5). `FetchXmlQuery` output of
`Build()` is immutable (proposed, subject to implementation).

## Proposed public API

Fixed by master plan §4:

```csharp
var fetch = FetchXml.For("account")
    .Attributes("name", "revenue")
    .Where(f => f.Eq("statecode", 0).And(a => a.Like("name", "Contoso%")))
    .Link("contact", from: "primarycontactid", to: "contactid",
          l => l.Alias("pc").Attributes("fullname"))
    .OrderBy("name").Top(50)
    .Build();
var page = await dataverse.FetchAsync(fetch, ct);
```

Types: `FetchXml` (entry), `FetchXmlQuery`, `FetchFilterBuilder`, `FetchLinkEntityBuilder`,
`FetchConditionOperator` in `Koras.Dataverse.FetchXml` (master plan §4).

Conservative proposal, subject to implementation: the page result of `FetchAsync` reuses
`DataverseQueryResult` semantics (entities + continuation carrying the paging cookie), and a
`FetchAllAsync` streaming counterpart mirroring KDV-003's `QueryAllAsync` is a design option
evaluated during implementation.

## Configuration

- Builder: none — it is a pure library.
- Execution: page size (`count`/`page` semantics) with a safe default — proposed, subject to
  implementation.

## Error conditions

| Condition | Behavior |
|---|---|
| Invalid builder state (empty entity name, `Top` out of range, alias collision) | Failure at the offending call or at `Build()`, before any I/O, with precise message |
| Server rejects FetchXML (unknown attribute, bad operator usage) | `DataverseException` via KDV-009 with request id |
| Malformed paging cookie passed to continuation | Classified client-side error before I/O (subject to implementation) |
| Throttling during fetch | KDV-008 handles below the surface |
| Cancellation | `OperationCanceledException` unwrapped |

## Security

- **XML escaping everywhere.** Entity/attribute names, values, aliases, and order expressions
  are escaped; no caller string reaches the document unescaped (master plan §7).
- Injection attempts (`"/><entity ...`, `]]>`, quote-breaking) must serialize as inert text —
  covered by a dedicated test matrix.
- Raw-FetchXML escape-hatch execution is documented as the caller's responsibility to keep
  free of untrusted input.

## Performance

- Builder allocation proportional to query size; no LINQ-expression machinery.
- netstandard2.0 target constrains dependencies, not performance: no runtime reflection in the
  build path.
- Paging cookies are passed through opaquely; the SDK does not re-parse cookie internals
  beyond what continuation requires.

## Observability

- Execution: one `Activity` per fetch page, tagged with entity name and page number; no query
  content in tags by default (KDV-011).
- Builder itself emits no telemetry (works in plugin contexts with no logging infrastructure).

## Test plan

**Unit — builder** (pure, no I/O; runs on all TFMs including netstandard2.0 consumers):
- XML snapshot tests for every fluent member and combination in master plan §4.
- Escaping matrix: XML-special characters in names, values, aliases; hostile payloads inert.
- `FetchConditionOperator` coverage: each operator serializes to the correct FetchXML operator
  string.
- Builder-state validation errors.

**Unit — execution** (fake `HttpMessageHandler`): request shape for fetch queries; paging
cookie extraction and reuse across pages; error mapping; cancellation.

**Architecture** (master plan §6): `Koras.Dataverse.FetchXml` has zero package references.

**Integration** (env-var gated): paged fetch (> 1 page) returns complete, non-overlapping
results; link-entity query returns aliased columns.

## Acceptance criteria

1. The master plan §4 sample compiles and runs unmodified.
2. Generated XML is schema-valid for all builder paths; snapshots stable.
3. Hostile input cannot alter document structure (escaping matrix passes).
4. Multi-page fetch via paging cookies returns complete results with no duplicates.
5. The builder package compiles and is consumable from a netstandard2.0 / .NET Framework
   4.6.2 test consumer with zero additional dependencies.
6. Error, cancellation, and paging tests present per the MVP bar.
