# Feature Planning — KDV-002 CRUD Operations

> Planning-level design document (pre-implementation). Catalog entry:
> [`feature-catalog.md`](feature-catalog.md#kdv-002--crud-upsert-alternate-keys-entity-model).
> Source of truth: [`docs/planning/master-plan.md`](../planning/master-plan.md) §3 (KDV-002),
> §4, §5. Release classification: **MVP**.

## Overview

KDV-002 delivers the record-operation core: create, retrieve, update, delete, and upsert
(including alternate-key addressing) over the Dataverse Web API, built on a late-bound
`Entity` model with **plain CLR values** — no `OptionSetValue`/`Money` wrappers — plus
attribute-based typed POCO mapping. Lookup values expressed as `EntityReference` are translated
to `@odata.bind` automatically (master plan §3).

## Requirements

**Functional**

1. Operations on `IDataverseClient`: `CreateAsync`, `RetrieveAsync`, `UpdateAsync`,
   `DeleteAsync`, `UpsertAsync`; all async with `CancellationToken` (defaulted) per master plan
   §4.
2. Late-bound model: `Entity` (logical name + indexer over attribute values),
   `EntityReference`, `ColumnSet` for retrieve column selection.
3. Plain CLR values: `string`, `int`, `decimal`, `bool`, `DateTime`/`DateTimeOffset`, `Guid`,
   option sets as `int`, money as `decimal`, lookups as `EntityReference`.
4. `@odata.bind` handled automatically when attribute values are `EntityReference` instances.
5. Alternate keys usable to address records for retrieve, update, upsert, and delete.
6. `UpsertAsync` returns `UpsertResult` indicating whether the record was created or updated
   and its id.
7. Attribute-based typed POCO mapping: map a POCO to/from `Entity` via mapping attributes
   (attribute names/shape subject to implementation); mapping is optional sugar — `Entity`
   remains the foundation.

**Nonfunctional.** Thread-safe client (singleton registration, master plan §5);
nullable-annotated; no polymorphic deserialization (master plan §7).

## Proposed public API

Fixed by master plan §4 (usage sample):

```csharp
public sealed class InvoiceService(IDataverseClient dataverse)
{
    public async Task<Guid> CreateAccountAsync(string name, CancellationToken ct)
    {
        var account = new Entity("account") { ["name"] = name, ["revenue"] = 25_000m };
        return await dataverse.CreateAsync(account, ct);
    }
}
```

Types: `IDataverseClient`, `Entity`, `EntityReference`, `ColumnSet`, `UpsertResult` in
`Koras.Dataverse` (master plan §4).

Conservative signatures, subject to implementation:

```csharp
Task<Guid>   CreateAsync(Entity entity, CancellationToken cancellationToken = default);
Task<Entity> RetrieveAsync(string entityLogicalName, Guid id, ColumnSet columns,
                           CancellationToken cancellationToken = default);
Task         UpdateAsync(Entity entity, CancellationToken cancellationToken = default);
Task         DeleteAsync(string entityLogicalName, Guid id,
                         CancellationToken cancellationToken = default);
Task<UpsertResult> UpsertAsync(Entity entity, CancellationToken cancellationToken = default);
```

Alternate-key addressing is expected as overloads accepting a key-value set (shape subject to
implementation). POCO mapping surfaces as generic overloads or a mapper service — decided
during implementation against the constraint that `Abstractions` stays dependency-free.

## Configuration

None feature-specific. Behavior governed by `DataverseClientOptions` (environment URL, auth,
retry). Serialization conventions (date handling, formatted values) are fixed by design and
documented rather than configurable in MVP.

## Error conditions

| Condition | Behavior |
|---|---|
| Retrieve/update/delete of nonexistent record | `DataverseException`, not-found category, HTTP 404 preserved |
| Duplicate alternate key / key not defined | Classified `DataverseException` with Dataverse error code preserved |
| Validation failures (bad attribute, business rule) | Classified via KDV-009; message safe to log |
| Permission denied | Permission category; request id captured for support |
| Cancellation | `OperationCanceledException` unwrapped (master plan §5) |
| POCO mapping error (unmapped/ill-typed member) | Immediate, descriptive client-side exception before any I/O |

## Security

- Attribute values serialized with strict JSON encoding; logical names and key values encoded
  into URLs safely.
- No dynamic/polymorphic type resolution when deserializing responses (master plan §7).
- Record payloads are never logged at information level; diagnostic payload logging, if any, is
  opt-in and documented as sensitive.

## Performance

- Retrieve requires an explicit `ColumnSet` to discourage over-fetching (an all-columns option
  exists but is deliberate).
- Serialization path designed for minimal allocation; POCO mapping avoids per-call reflection
  via cached mapping plans (subject to implementation).
- Client is a singleton over pooled `HttpClient` handlers (KDV-010).

## Observability

- One `Activity` per operation (source `"Koras.Dataverse"`), wrapping retries (master plan
  §5), tagged with operation kind and entity logical name — never attribute values.
- Metrics (KDV-011): operation counter and duration histogram tagged by operation and outcome.
- Logs: per-operation debug logging; failures logged once with KDV-009 fields.

## Test plan

**Unit** (fake `HttpMessageHandler`, master plan §6):
- Payload generation: plain CLR values, `@odata.bind` from `EntityReference`, null handling,
  date/Guid/decimal formatting.
- URL construction: ids, alternate keys (encoding of string key values), `ColumnSet`.
- Response parsing: created id extraction, `UpsertResult` created-vs-updated, entity
  materialization round-trip.
- POCO mapping: to/from `Entity`, error cases.
- Error paths: 404/400/403 mapped per KDV-009; cancellation tests on every operation.

**Integration** (env-var gated): full CRUD round-trip on a standard table; alternate-key upsert
executed twice proving create-then-update; delete verified by failed retrieve.

## Acceptance criteria

1. CRUD + upsert round-trip passes against a real environment.
2. Alternate keys work for retrieve, update, upsert, and delete.
3. `EntityReference` attributes produce correct `@odata.bind` payloads without caller
   involvement.
4. Public surface exposes plain CLR values only — no wrapper value types.
5. `UpsertResult` correctly distinguishes created from updated.
6. Every operation has error-path and cancellation tests per the MVP quality bar (master plan
   §3).
