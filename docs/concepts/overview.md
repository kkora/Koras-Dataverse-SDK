# Concepts Overview

The mental model for the SDK in one page.

## The client and its pipeline

You program against **`IDataverseClient`** — one instance per Dataverse environment, registered
as a singleton. Every operation (create, retrieve, query, batch, …) travels the same path:

```text
IDataverseClient
   │  validate input, build the Web API request, start telemetry span,
   │  enforce the per-operation Timeout
   ▼
RetryHandler          retries 429/502/503/504 and network failures,
   │                  exponential backoff + jitter, honors Retry-After
   ▼
AuthenticationHandler attaches a bearer token (cached, refreshed ~5 min before expiry)
   │
   ▼  (your custom DelegatingHandlers, if any)
Dataverse Web API     {environmentUrl}/api/data/v9.2/…
```

Because retry sits *outside* authentication, every retry attempt re-attaches a token — a retry
never fails because a token expired mid-backoff. Because telemetry is emitted by the client
layer, a span covers the whole operation *including* its retries.

Failures come back to you in exactly two shapes: **`DataverseException`** (with a normalized
[`DataverseError`](error-handling.md)) for anything Dataverse- or transport-related, and
**`OperationCanceledException`** for [cancellation](cancellation.md). Nothing else escapes.

## The Entity model: late-bound, plain CLR values

`Entity` is a late-bound row: a table logical name, an id, and a dictionary of attributes.
There are no wrapper types — no `OptionSetValue`, no `Money`:

```csharp
var account = new Entity("account")
{
    ["name"] = "Contoso",                 // string column
    ["revenue"] = 25_000m,                // money column  -> decimal
    ["accountcategorycode"] = 1,          // choice column -> int
    ["lastonholdtime"] = DateTimeOffset.UtcNow,
    ["primarycontactid"] = new EntityReference("contact", contactId), // lookup
};
```

Reading uses the indexer (returns `object?`) or the typed helpers:

```csharp
string? name = account.GetValue<string>("name");
decimal? revenue = account.GetValue<decimal?>("revenue");
if (account.TryGetValue("statecode", out int state)) { /* … */ }
```

When annotations are enabled (the default), `Entity.FormattedValues` carries display strings —
choice labels, formatted dates, lookup display names — keyed by column logical name.

## EntityReference for lookups

A lookup value is an `EntityReference(tableName, id)`. On writes the SDK serializes it as the
Web API's `@odata.bind` reference automatically; on reads, lookup columns are materialized back
into `EntityReference` instances (with `Name` populated from annotations when available).

## Logical names in, entity set names handled for you

Everything is addressed by table **logical name** (`account`, `contact`, `new_project`). The
client derives the Web API entity set name (`accounts`, `contacts`, `new_projects`) using
Dataverse's pluralization rules, with `EntitySetNameOverrides` as the escape hatch for
irregular names.

## Queries: two builders, one result shape

- **`ODataQuery.For("account")`** — fluent OData (`$select`, `$filter`, `$orderby`, `$expand`,
  `$top`), with injection-safe literal encoding.
- **`FetchXml.For("account")…Build()`** — fluent FetchXML for joins, `like`, distinct, and
  everything OData can't express. Ships as a standalone, dependency-free package.

Both execute to a `DataverseQueryResult` page; `QueryAllAsync`/`FetchAllAsync` stream *all*
rows as `IAsyncEnumerable<Entity>`, following next links and paging cookies for you.

## Sub-clients

`IDataverseClient` exposes two scoped helpers for the same environment:

- **`Metadata`** (`IMetadataClient`) — read-only tables, columns, relationships, choices, and
  entity set names.
- **`Solutions`** (`ISolutionClient`) — export, import, publish-all, and lookup of installed
  solutions.

## Read the rest

- [Architecture](architecture.md) — layers and package boundaries
- [Core abstractions](core-abstractions.md) — every type, with snippets
- [Operation lifecycle](lifecycle.md) — one call, step by step
- [Error handling](error-handling.md) · [Cancellation](cancellation.md) · [Thread safety](thread-safety.md)
