# Feature Planning — KDV-006 Metadata

> Planning-level design document (pre-implementation). Catalog entry:
> [`feature-catalog.md`](feature-catalog.md#kdv-006--metadata-read).
> Source of truth: [`docs/planning/master-plan.md`](../planning/master-plan.md) §3 (KDV-006),
> §4. Release classification: **MVP**.

## Overview

KDV-006 provides read-only access to Dataverse schema metadata — tables, columns, choices
(local and global), and relationships — through `IMetadataClient`, returning typed,
lightweight models instead of the Web API's verbose `EntityMetadata` contract. It powers
metadata automation (UC-2) and is the foundation for future snapshot/codegen features
(KDV-016, KDV-018).

## Requirements

**Functional**

1. `IMetadataClient` operations (read-only, master plan §3):
   - list tables / get a table by logical name → `TableMetadata`;
   - list columns of a table / get a column → `ColumnMetadata`;
   - get local choice options for a choice column and global choice sets → `ChoiceOption`
     collections;
   - list relationships of a table (one-to-many, many-to-one, many-to-many) →
     `RelationshipMetadata`.
2. Models are lightweight and typed: logical/schema/display names, types, requiredness, and
   the structurally important properties — not a 1:1 mirror of the full server contract
   (member lists subject to implementation).
3. No metadata **write** operations in this feature (creation/updates of schema are not MVP
   scope and not part of KDV-006).
4. `IMetadataClient` is injectable and mockable, registered by `AddDataverse` (KDV-010).

**Nonfunctional.** Models are immutable records (master plan §4 guiding rules); parsing is
tolerant of server-side contract additions (unknown properties ignored); thread-safe client.

## Proposed public API

Types fixed by master plan §4: `IMetadataClient`, `TableMetadata`, `ColumnMetadata`,
`RelationshipMetadata`, `ChoiceOption` in `Koras.Dataverse.Metadata`.

Conservative proposal, subject to implementation:

```csharp
public interface IMetadataClient
{
    Task<TableMetadata> GetTableAsync(string logicalName,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TableMetadata>> GetTablesAsync(
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ColumnMetadata>> GetColumnsAsync(string tableLogicalName,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RelationshipMetadata>> GetRelationshipsAsync(string tableLogicalName,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChoiceOption>> GetGlobalChoiceAsync(string name,
        CancellationToken cancellationToken = default);
}
```

Exact member set, filtering parameters (e.g., include-columns flags), and whether table
listing pages or returns a full list are decided during implementation against real payload
sizes.

## Configuration

None feature-specific in MVP. Metadata caching is deliberately **left to consumers** and
documented as such — a correct built-in cache (invalidation, versioning) is out of MVP scope
and would add complexity ahead of evidence (see
[`../product/problem-statement.md`](../product/problem-statement.md)); revisited with KDV-018.

## Error conditions

| Condition | Behavior |
|---|---|
| Unknown table/column/choice name | `DataverseException`, not-found category |
| Insufficient privilege to read metadata | Permission category with request id |
| Contract surprises (missing expected property) | Tolerant mapping; genuinely unusable payloads produce a classified parsing error, never a raw serializer exception |
| Throttling | KDV-008 as usual — metadata endpoints are subject to service protection too |
| Cancellation | `OperationCanceledException` unwrapped |

## Security

- Logical names are URL-encoded into `EntityDefinitions(...)`-style request paths safely.
- Metadata is schema, not business data, but display names/descriptions can carry
  organizational information — docs note this for log/output handling.
- Read-only surface: no code path in this feature can mutate an environment.

## Performance

- Requests use `$select` narrowing to fetch only mapped properties — full `EntityMetadata`
  payloads are notoriously large.
- Full-table listing avoids retrieving column collections unless asked.
- Consumers advised to cache results for hot paths (documented, with a sample pattern).

## Observability

- One `Activity` per metadata call, tagged with operation and logical name (KDV-011).
- Standard operation metrics; no metadata-specific instruments planned.
- Debug logging of request targets; never full payload bodies at information level.

## Test plan

**Unit** (fixture payloads captured from a real environment, fake `HttpMessageHandler`):
- Mapping correctness per model family: tables, columns (each major column type incl. choice
  columns), local/global choices, all three relationship kinds.
- Tolerance: fixtures with extra unknown properties map cleanly.
- URL construction and `$select` narrowing assertions.
- Error paths: 404 for unknown names, 403 permission; cancellation per operation.

**Integration** (env-var gated, master plan §6): read metadata of a well-known standard table
(e.g., `account`): table info, columns including a choice column with options, relationships;
read one global choice set; unknown-name calls produce classified not-found.

## Acceptance criteria

1. All four model families retrievable through `IMetadataClient` with correct values verified
   against a real environment.
2. Local and global choices both supported (master plan §3).
3. Unknown names yield classified not-found errors, not raw HTTP or serializer failures.
4. Models are immutable, dependency-free (Abstractions rules), and contain no third-party
   types.
5. Payload narrowing via `$select` verified in request assertions.
6. Error, cancellation, and mapping tests present per the MVP bar.
