# Thread Safety

The SDK's threading contract in one page. The rule of thumb: **clients and built results are
shareable; builders and entities are not.**

## Thread-safe: the client and everything injectable

`IDataverseClient` implementations (and the `Metadata`/`Solutions` sub-clients) are thread-safe
and designed to live as **singletons** — one instance per environment for the process lifetime.
Any number of concurrent requests may flow through one client:

```csharp
// Safe: one client, many parallel operations.
await Task.WhenAll(
    dataverse.CreateAsync(new Entity("account") { ["name"] = "A" }, ct),
    dataverse.CreateAsync(new Entity("account") { ["name"] = "B" }, ct),
    dataverse.WhoAmIAsync(ct));
```

The token cache behind the client is also thread-safe with single-flight refresh: under
concurrency, exactly one token request goes to the identity provider while other callers wait.

`IDataverseClientFactory` and custom `IDataverseTokenProvider` implementations must be (and the
built-in ones are) thread-safe.

## Not thread-safe while configuring: the builders

`ODataQuery`, `ODataFilterBuilder`, `FetchXmlBuilder`, `FetchFilterBuilder`,
`FetchLinkEntityBuilder`, and `BatchRequest` are mutable while being configured. Build each on
a single thread:

```csharp
// WRONG: two threads mutating one builder
var query = ODataQuery.For("account");
Parallel.Invoke(
    () => query.Select("name"),
    () => query.OrderBy("name")); // data race

// RIGHT: build locally, then use
var query = ODataQuery.For("account").Select("name").OrderBy("name");
```

Sharing a fully configured `ODataQuery` or `BatchRequest` for concurrent *reads* (executing the
same query from several tasks) is fine as long as no thread keeps mutating it.

`FetchXmlQuery` is different: once `Build()` (or `FromXml`) returns, the query is **immutable**
and freely shareable across threads — cache it in a static field if you like. `WithPage`
returns a new instance rather than mutating.

## Not thread-safe: Entity

`Entity` is a mutable bag (`Attributes`, `FormattedValues`, `Id`). Do not mutate a shared
instance concurrently, and do not mutate an entity while an SDK call using it is in flight.
Entities streamed from `QueryAllAsync`/`FetchAllAsync` are fresh instances per row — safe to
hand off to another thread, as long as only one thread touches each instance at a time.

`EntityReference`, by contrast, is an immutable record — share freely.

## Immutable results

Everything the client returns is safe to share once you have it:

- `DataverseQueryResult` — read-only page (do not mutate the `Entity` instances inside it from
  multiple threads),
- `BatchResponse` / `BatchItemResult`,
- `WhoAmIResponse`, `UpsertResult` (records),
- `DataverseError` (record), `DataverseException`,
- metadata models (`TableMetadata`, `ColumnMetadata`, `RelationshipMetadata`, `ChoiceOption`),
- `SolutionInfo`.

## Options: configure once, then leave alone

`DataverseClientOptions` (including `Authentication`, `Retry`, `EntitySetNameOverrides`) is
meant to be configured during startup — inside the `AddDataverse` lambda or before
`DataverseClient.Create` — and not mutated afterwards. Runtime mutation of options a live
client was built from is unsupported.
