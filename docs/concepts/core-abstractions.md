# Core Abstractions

Every core type in `Koras.Dataverse.Abstractions` (plus the FetchXML query type), with its
purpose and a minimal snippet. All of these are safe to reference from libraries and test
projects without pulling in the implementation package.

## IDataverseClient

The whole client behind one interface: CRUD, OData and FetchXML queries with automatic paging,
association management, batches, `WhoAmI`, and the `Metadata`/`Solutions` sub-clients.
Thread-safe; registered as a singleton. All failures surface as `DataverseException`;
cancellation as `OperationCanceledException`.

```csharp
public sealed class AccountService(IDataverseClient dataverse)
{
    public Task<Entity> GetAsync(Guid id, CancellationToken ct) =>
        dataverse.RetrieveAsync("account", id, ColumnSet.Of("name", "revenue"), ct);
}
```

## Entity

A late-bound row: table logical name + id + attribute bag with plain CLR values (`string`,
`int`, `decimal`, `bool`, `DateTimeOffset`, `Guid`, `EntityReference` for lookups; choice
columns are `int`, money columns are `decimal`).

```csharp
var contact = new Entity("contact")
{
    ["firstname"] = "Ada",
    ["lastname"] = "Lovelace",
    ["birthdate"] = new DateTimeOffset(1815, 12, 10, 0, 0, 0, TimeSpan.Zero),
};

string? last = contact.GetValue<string>("lastname");   // typed read; missing -> default
object? raw = contact["firstname"];                    // raw read; missing -> null
EntityReference reference = contact.ToReference();     // requires a non-empty Id
```

`FormattedValues` holds server-provided display strings (choice labels, formatted dates) when
annotations are enabled. Not thread-safe — do not mutate one instance concurrently.

## EntityReference

An immutable record pointing at a row — the value type of lookup columns.

```csharp
var owner = new EntityReference("systemuser", userId);
account["primarycontactid"] = new EntityReference("contact", contactId); // written as @odata.bind
```

`Name` is populated from annotations on reads. `ToString()` renders `tableName(id)`.

## ColumnSet

Which columns a retrieve returns. Prefer explicit columns.

```csharp
ColumnSet columns = ColumnSet.Of("name", "revenue");
ColumnSet everything = ColumnSet.All; // use sparingly
```

## DataverseQueryResult

One page of query results: `Entities`, `NextLink` (OData), `PagingCookie` (FetchXML),
`TotalCount` (when requested), and `MoreRecords`.

```csharp
DataverseQueryResult page = await dataverse.QueryAsync(query, ct);
foreach (Entity row in page.Entities) { /* … */ }
bool hasMore = page.MoreRecords;
```

## ODataQuery (Koras.Dataverse.Queries)

Fluent, injection-safe OData query builder. Addressed by table logical name; the client
resolves the entity set name. Not thread-safe while being configured.

```csharp
var query = ODataQuery.For("account")
    .Select("name", "revenue")
    .Where(f => f.Eq("statecode", 0).And(a => a.Gt("revenue", 100_000m)))
    .OrderByDescending("revenue")
    .Expand("primarycontactid", "fullname")
    .IncludeCount()
    .PageSize(1000);
```

The companion `ODataFilterBuilder` offers `Eq/Ne/Gt/Ge/Lt/Le`, `Contains/StartsWith/EndsWith`,
`IsNull/IsNotNull`, `In`, nested `And/Or/Not` groups, and an explicit `Raw` escape hatch for
trusted text.

## FetchXmlQuery (Koras.Dataverse.FetchXml)

An immutable, validated FetchXML document. Build fluently or wrap existing XML.

```csharp
FetchXmlQuery fetch = FetchXml.For("account")
    .Attributes("name", "revenue")
    .Where(f => f.Eq("statecode", 0).And(a => a.Like("name", "Contoso%")))
    .Link("contact", from: "contactid", to: "primarycontactid",
          l => l.Alias("pc").Attributes("fullname"))
    .OrderBy("name")
    .Top(50)
    .Build();

FetchXmlQuery wrapped = FetchXmlQuery.FromXml("<fetch><entity name=\"account\"/></fetch>");
```

Immutable and shareable once built; the builder itself is not thread-safe.

## BatchRequest / BatchResponse (Koras.Dataverse.Batches)

Up to 1,000 write operations in one `$batch` request. `Atomic` defaults to `true` (one change
set — all succeed or all roll back).

```csharp
var batch = new BatchRequest { Atomic = false }; // continue on error
batch.AddCreate(new Entity("account") { ["name"] = "A" })
     .AddUpdate(new Entity("account", existingId) { ["name"] = "B" })
     .AddUpsert(new Entity("account", knownId) { ["name"] = "C" })
     .AddDelete("account", obsoleteId);

BatchResponse response = await dataverse.ExecuteBatchAsync(batch, ct);
foreach (BatchItemResult item in response.Results)
{
    if (!item.Succeeded)
    {
        Console.WriteLine($"op {item.Index} failed: {item.Error}");
    }
}
```

In an atomic batch a failure throws `DataverseException` (the change set rolled back); in a
non-atomic batch failures are reported per item and `ExecuteBatchAsync` returns normally.

## IMetadataClient (Koras.Dataverse.Metadata)

Read-only metadata for the environment, via `dataverse.Metadata`.

```csharp
TableMetadata table = await dataverse.Metadata.GetTableAsync("account", ct);
IReadOnlyList<ColumnMetadata> columns = await dataverse.Metadata.GetColumnsAsync("account", ct);
IReadOnlyList<ChoiceOption> options = await dataverse.Metadata.GetChoicesAsync("account", "accountcategorycode", ct);
string entitySet = await dataverse.Metadata.GetEntitySetNameAsync("account", ct); // "accounts"
```

Also: `GetTablesAsync`, `GetRelationshipsAsync`, `GetGlobalChoicesAsync`.

## ISolutionClient (Koras.Dataverse.Solutions)

Solution lifecycle helpers, via `dataverse.Solutions`. These are long-running server-side —
budget generous timeouts.

```csharp
byte[] zip = await dataverse.Solutions.ExportAsync("my_solution", managed: true, ct);
await dataverse.Solutions.ImportAsync(zip, new SolutionImportOptions { PublishWorkflows = true }, ct);
await dataverse.Solutions.PublishAllAsync(ct);
SolutionInfo? installed = await dataverse.Solutions.FindAsync("my_solution", ct);
```

## IDataverseTokenProvider (Koras.Dataverse.Authentication)

The token-supply abstraction. The default implementation adapts an `Azure.Core.TokenCredential`
(scope `{environmentUrl}/.default`, cached, refreshed five minutes before expiry). Implement it
yourself to plug in any token source — implementations must be thread-safe.

```csharp
public sealed class StaticTokenProvider(string token) : IDataverseTokenProvider
{
    public ValueTask<string> GetAccessTokenAsync(Uri environmentUrl, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(token);
}

options.Authentication.UseTokenProvider(new StaticTokenProvider(token));
```

## IDataverseClientFactory

Resolves named clients in multi-environment applications; see
[dependency injection](../getting-started/dependency-injection.md).

```csharp
IDataverseClient prod = factory.GetClient("crm-prod");
```

## Errors, mapping, and results

- `DataverseException` / `DataverseError` / `DataverseErrorCategory` — see
  [error handling](error-handling.md).
- `[DataverseTable]` / `[DataverseColumn]` + `EntityMapper` — attribute-based POCO mapping; see
  [common scenarios](../recipes/common-scenarios.md).
- `UpsertResult(Id, Created)` — outcome of an upsert.
- `WhoAmIResponse(UserId, BusinessUnitId, OrganizationId)` — identity probe result.
