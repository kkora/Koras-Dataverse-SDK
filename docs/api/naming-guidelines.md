# Naming Guidelines

> Applies .NET Framework Design Guidelines naming conventions to this SDK, specialized with
> the domain vocabulary fixed by §4 of
> [`docs/planning/master-plan.md`](../planning/master-plan.md). If this document and the
> master plan disagree, the master plan wins. Enforced by architecture tests
> ([`../architecture/dependency-rules.md`](../architecture/dependency-rules.md) §4) and the
> [review checklist](public-api-review-checklist.md).

## 1. General .NET conventions (baseline)

- PascalCase for types, members, and namespaces; camelCase for parameters and locals.
- Interfaces prefixed `I`; no other Hungarian or prefix notation.
- Acronyms follow guideline casing: two-letter acronyms uppercase (`Id` is treated as a
  word: `RequestId`, not `RequestID`), longer acronyms PascalCase (`OData` is a brand
  spelling and keeps its capital D: `ODataQuery`).
- No abbreviations in public identifiers except industry-standard ones already fixed by the
  plan (`FetchXml`, `OData`, `Xml` in type names follows brand/guideline casing:
  `FetchXmlQuery`).
- US English spelling throughout (`Serializer`, `Color`-style rules; e.g., "canceled" in
  docs, `Cancellation` in identifiers per BCL).

## 2. Async suffix

- Every public member returning `Task`, `Task<T>`, `ValueTask`, `ValueTask<T>`, or
  `IAsyncEnumerable<T>` ends in `Async`: `CreateAsync`, `QueryAllAsync`, `WhoAmIAsync`,
  `ExecuteBatchAsync`, `GetTokenAsync`.
- This includes `IAsyncEnumerable<T>`-returning members (`QueryAllAsync`, `FetchAllAsync`):
  they represent I/O even though enumeration is deferred.
- No `Async`-suffixed member may exist without an async return type, and no sync
  counterpart pairs are permitted (async-first, no sync-over-async — master plan §4/§5).

## 3. Domain vocabulary

Consistent words for consistent concepts, everywhere (API, docs, log placeholders, telemetry
tags):

| Concept | Term | Never |
|---|---|---|
| Dataverse table | *table*; parameters named `tableName` holding the logical name (`"account"`) | `entityName`, `entityLogicalName` (legacy Xrm vocabulary) |
| Metadata identifier of a table/column/choice | `logicalName` (and `columnLogicalName`, `choiceName` where disambiguation is needed) | `name` alone in metadata APIs |
| Table columns in data APIs | *column(s)* (`ColumnSet`, `Select(params string[] columns)`) | mixing `attribute` into OData-side APIs |
| FetchXML side | *attribute* is acceptable where FetchXML itself says attribute (`Attributes(...)`), matching the schema consumers see | inventing `Columns` for FetchXML elements |
| Row identity | `id` (`Guid id`), `Entity.Id` | `entityId`, `recordId` |
| Choice values | *choice* (`ChoiceOption`, `GetChoicesAsync`, `GetGlobalChoicesAsync`) | `optionSet` in public API (legacy vocabulary; may appear in docs when explaining the platform) |

Rationale: `tableName`/`column`/`choice` is the modern Dataverse (Power Platform)
vocabulary; `entity`/`attribute`/`option set` is the legacy Xrm vocabulary. The SDK speaks
modern Dataverse except where a wire format (FetchXML) makes the legacy word the accurate
one. The type name `Entity` itself is fixed by master plan §4 as the late-bound row type —
its *parameters* still say `tableName`.

## 4. Options and configuration

- Options classes end in `Options`: `DataverseClientOptions`,
  `DataverseAuthenticationOptions`, `DataverseRetryOptions`, `SolutionImportOptions`.
- Options properties are nouns without prefixes: `EnvironmentUrl`, `Timeout`, `Retry`,
  `Authentication`.
- Configuration selector methods use the `Use` prefix: `UseClientSecret`,
  `UseManagedIdentity`, `UseTokenCredential`, `UseTokenProvider<T>`.
- Boolean options are affirmative and unambiguous: `ContinueOnError`, `IsManaged` — never
  negated names (`DisableX` only when the default-on feature makes `EnableX` impossible).

## 5. Properties and methods

- **No `Get` prefix on properties**; properties are nouns (`Operations`, `AttributeNames`,
  `Metadata`, `Solutions`). `Get*Async` is reserved for methods that perform I/O
  (`GetTableAsync`) — the prefix signals work, the property signals state.
- `Try*` methods return `bool`/nullable and never throw for the "miss" case
  (`TryGetValue`, `TryRetrieveAsync`); the throwing counterpart keeps the plain name
  (`RetrieveAsync`).
- Factory-style creation methods use `Create` (`IDataverseClientFactory.CreateClient`).
- Event-free SDK: no public events; no `On*` names.

## 6. Builder verbs

Builders read as the query they produce. Fixed verb set (master plan §4):

| Verb | Meaning | Where |
|---|---|---|
| `For(tableName)` | Static entry point naming the target table | `ODataQuery.For`, `FetchXml.For` |
| `Select(...)` / `Attributes(...)` | Column projection | OData / FetchXML respectively |
| `Where(f => …)` | Filter group | Both builders |
| `OrderBy(...)` (+ `OrderByDescending` on OData) | Sort | Both builders |
| `Top(n)` | Row limit | Both builders |
| `Link(...)` | Join (`<link-entity>`) | FetchXML |
| `Expand(...)` | `$expand` navigation | OData |
| `Build()` | Terminal: render the immutable artifact | FetchXML (and any builder with a distinct built artifact) |

- Chainable members return the builder type; only `Build()` (or handing the builder to a
  client method, for `ODataQuery`) terminates the chain.
- Filter condition methods are terse operator names matching the underlying protocol:
  `Eq`, `Ne`, `Gt`, `Ge`, `Lt`, `Le`, `Like`, `In`, `Contains`, `StartsWith`, `EndsWith`,
  `IsNull`/`Null`, `IsNotNull`/`NotNull` (OData/FetchXML flavor respectively), with
  `And`/`Or`/`Not` for grouping.

## 7. Namespaces

Public namespaces are fixed by master plan §4 and map to packages as follows (the mapping is
per type-group, not strictly per package — contracts in the `Koras.Dataverse` namespace ship
in the `Abstractions` assembly):

| Namespace | Contents | Shipping package |
|---|---|---|
| `Koras.Dataverse` | `IDataverseClient`, `Entity`, `EntityReference`, `ColumnSet`, results, options | Abstractions (contracts/models); implementation in `Koras.Dataverse` |
| `Koras.Dataverse.Queries` | `ODataQuery`, `ODataFilterBuilder`, `ODataExpand` | Abstractions |
| `Koras.Dataverse.FetchXml` | `FetchXml`, `FetchXmlQuery`, `FetchFilterBuilder`, `FetchLinkEntityBuilder`, `FetchConditionOperator` | FetchXml |
| `Koras.Dataverse.Batches` | `BatchRequest`, `BatchOperation`, `BatchResponse`, `BatchItemResult` | Abstractions |
| `Koras.Dataverse.Metadata` | `IMetadataClient` + metadata models | Abstractions (contracts); implementation in `Koras.Dataverse` |
| `Koras.Dataverse.Solutions` | `ISolutionClient` + solution models | Abstractions (contracts); implementation in `Koras.Dataverse` |
| `Koras.Dataverse.Errors` | `DataverseException`, `DataverseError`, `DataverseErrorCategory` | Abstractions |
| `Koras.Dataverse.Authentication` | `IDataverseTokenProvider`, credential option helpers | Abstractions (seam); Azure-typed helpers in `Koras.Dataverse` |
| `Microsoft.Extensions.DependencyInjection` | `AddDataverse`, `AddDataverseHealthCheck`, `IDataverseClientFactory` | `Koras.Dataverse` (extensions); factory interface per master plan §2 |

- No public types outside these namespaces (until a new namespace is added by plan/ADR).
- Implementation-only namespaces (e.g., internal handler/serialization namespaces) contain
  no public types and are not part of any contract.
- DI extension methods live in `Microsoft.Extensions.DependencyInjection` per platform
  convention so they surface without extra `using` directives.

## 8. Telemetry, logging, and diagnostics names

Not .NET identifiers, but named artifacts with the same discipline (see
[`../architecture/observability.md`](../architecture/observability.md)):

- ActivitySource/Meter name: `Koras.Dataverse`; span `dataverse.execute`; tags
  `dataverse.operation`, `dataverse.table`, `dataverse.request_id`,
  `http.response.status_code` (dot.case, OTel-style).
- Instruments: `koras.dataverse.client.*` (dot.case, lowercase).
- Logger categories: `Koras.Dataverse`, `Koras.Dataverse.Http` (PascalCase dotted, matching
  namespace convention).
- Named HttpClient: `Koras.Dataverse:{name}`.

## 9. Naming checklist for new API

1. Does an existing plan/§4 term cover the concept? Use it; do not coin synonyms.
2. Async return type ⇔ `Async` suffix (both directions).
3. `tableName` for table logical names in data APIs; `logicalName` in metadata APIs.
4. Options type ⇒ `Options` suffix; selector methods ⇒ `Use` prefix.
5. Properties: nouns, no `Get` prefix, no verbs.
6. Builders: verb set from §6 only; terminal member is `Build()`.
7. Namespace from the table in §7; nothing public elsewhere.
8. US English; guideline casing for acronyms.
