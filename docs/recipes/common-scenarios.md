# Recipes: Common Scenarios

Copy-paste recipes for everyday Dataverse work. All assume an injected
`IDataverseClient dataverse` and a `CancellationToken ct`.

## Create a row with a lookup

Set an `EntityReference` as the attribute value; the SDK writes it as `@odata.bind`:

```csharp
var contact = new Entity("contact")
{
    ["firstname"] = "Ada",
    ["lastname"] = "Lovelace",
    ["parentcustomerid"] = new EntityReference("account", accountId), // lookup
};

Guid contactId = await dataverse.CreateAsync(contact, ct);
```

Need the server-calculated columns back immediately? Use `CreateAndReturnAsync`:

```csharp
Entity created = await dataverse.CreateAndReturnAsync(contact, ct);
DateTimeOffset? createdOn = created.GetValue<DateTimeOffset?>("createdon");
```

## Upsert by alternate key

Address the row by key columns instead of an id — creates when absent, updates when present:

```csharp
var account = new Entity("account")
{
    ["name"] = "Contoso Ltd",
    ["revenue"] = 25_000m,
};

UpsertResult result = await dataverse.UpsertAsync(
    account,
    new Dictionary<string, object> { ["accountnumber"] = "ACC-1001" },
    ct);

Console.WriteLine(result.Created
    ? $"Created {result.Id}"
    : $"Updated {result.Id}");
```

(Upsert by id: `await dataverse.UpsertAsync(new Entity("account", knownId) { … }, ct)`.)

## Retrieve with an explicit column set

```csharp
Entity account = await dataverse.RetrieveAsync(
    "account", accountId, ColumnSet.Of("name", "revenue", "_primarycontactid_value"), ct);

string? name = account.GetValue<string>("name");
EntityReference? primaryContact = account.GetValue<EntityReference>("_primarycontactid_value");
```

Omitting the column set (or passing `ColumnSet.All`) returns every column — fine for
exploration, wasteful in production.

Note the asymmetry the Web API imposes on lookups: you **write** them under the navigation
property name (`["primarycontactid"] = new EntityReference(…)`), but responses return them
under the value-column name (`_primarycontactid_value`), which is where the materialized
`EntityReference` lands (with `Name` populated when annotations are on).

## Query active rows

```csharp
using Koras.Dataverse.Queries;

var query = ODataQuery.For("account")
    .Select("name", "revenue")
    .Where(f => f.Eq("statecode", 0))
    .OrderByDescending("revenue")
    .Top(25);

DataverseQueryResult page = await dataverse.QueryAsync(query, ct);
foreach (Entity account in page.Entities)
{
    Console.WriteLine($"{account.GetValue<string>("name")}: {account.GetValue<decimal?>("revenue")}");
}
```

Richer filters compose with nested groups:

```csharp
var query = ODataQuery.For("account")
    .Select("name")
    .Where(f => f
        .Eq("statecode", 0)
        .Or(g => g.Gt("revenue", 1_000_000m).Contains("name", "Holdings")));
```

## Stream all pages

`QueryAllAsync` follows `@odata.nextLink` until exhausted; you just enumerate:

```csharp
var query = ODataQuery.For("contact")
    .Select("fullname", "emailaddress1")
    .Where(f => f.IsNotNull("emailaddress1"))
    .PageSize(1000); // rows per round-trip

await foreach (Entity contact in dataverse.QueryAllAsync(query, ct))
{
    Console.WriteLine(contact.GetValue<string>("emailaddress1"));
}
```

FetchXML equivalent (paging cookies handled internally):

```csharp
using Koras.Dataverse.FetchXml;

FetchXmlQuery fetch = FetchXml.For("contact")
    .Attributes("fullname", "emailaddress1")
    .Where(f => f.IsNotNull("emailaddress1"))
    .Build();

await foreach (Entity contact in dataverse.FetchAllAsync(fetch, pageSize: 5000, ct))
{
    // …
}
```

## Batch-upsert 500 rows

One atomic `$batch` — all 500 succeed or all roll back:

```csharp
using Koras.Dataverse.Batches;

IReadOnlyList<(Guid Id, string Name, decimal Revenue)> source = LoadSourceRows();

var batch = new BatchRequest(); // Atomic = true by default
foreach ((Guid id, string name, decimal revenue) in source.Take(500))
{
    batch.AddUpsert(new Entity("account", id)
    {
        ["name"] = name,
        ["revenue"] = revenue,
    });
}

BatchResponse response = await dataverse.ExecuteBatchAsync(batch, ct);
// Atomic batch: reaching this line means every operation succeeded.
```

Prefer independent operations (keep going past failures)? Set `Atomic = false` and inspect
per-item results:

```csharp
var batch = new BatchRequest { Atomic = false };
// …AddUpsert as above…

BatchResponse response = await dataverse.ExecuteBatchAsync(batch, ct);
foreach (BatchItemResult item in response.Results.Where(r => !r.Succeeded))
{
    Console.WriteLine($"row {item.Index} failed: {item.Error!.Category} — {item.Error.Message}");
}
```

A batch holds at most `BatchRequest.MaxOperations` (1,000) operations — chunk larger loads.

## Read choice labels via FormattedValues

Choice columns hold the underlying `int`; the display label rides along in `FormattedValues`
when annotations are enabled (the default):

```csharp
Entity account = await dataverse.RetrieveAsync(
    "account", accountId, ColumnSet.Of("name", "accountcategorycode"), ct);

int? categoryValue = account.GetValue<int?>("accountcategorycode");        // e.g. 1
account.FormattedValues.TryGetValue("accountcategorycode", out string? label); // e.g. "Preferred Customer"

Console.WriteLine($"{account.GetValue<string>("name")}: {label ?? categoryValue?.ToString() ?? "-"}");
```

Formatted values also cover money (`"$25,000.00"`), dates, and lookup display names. They are
read-only conveniences — never sent on writes. To enumerate *all* labels of a choice column,
use metadata: `await dataverse.Metadata.GetChoicesAsync("account", "accountcategorycode", ct)`.

## Map rows to POCOs

Attribute-based mapping for a typed view over late-bound rows:

```csharp
using Koras.Dataverse.Mapping;

[DataverseTable("account")]
public sealed class Account
{
    [DataverseColumn("accountid")] public Guid Id { get; set; }
    [DataverseColumn("name")] public string? Name { get; set; }
    [DataverseColumn("revenue")] public decimal? Revenue { get; set; }
}

// Entity -> POCO
Account typed = entity.ToObject<Account>();

// POCO -> Entity (null properties are skipped, not sent as clears)
Entity row = EntityMapper.ToEntity(typed);
Guid id = await dataverse.CreateAsync(row, ct);
```

## More

- [Advanced scenarios](advanced-scenarios.md) — custom auth, handlers, solutions, metadata
- [Production configuration](production-configuration.md)
- [Testing recipes](testing-recipes.md)
