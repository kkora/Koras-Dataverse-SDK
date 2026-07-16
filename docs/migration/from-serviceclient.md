# Migrating from ServiceClient

A mapping guide from `Microsoft.PowerPlatform.Dataverse.Client` (ServiceClient /
`IOrganizationService`) to the Koras Dataverse SDK. The programming models differ — this is a
translation table plus the idioms that change, not a mechanical rename.

## Type and concept mapping

| ServiceClient world | Koras Dataverse SDK | Notes |
|---|---|---|
| `ServiceClient` / `IOrganizationService` | `IDataverseClient` / `DataverseClient` | Interface-first, async-only, singleton |
| Connection string auth | `DataverseClientOptions` + `Authentication.Use…` | Options pattern; `TokenCredential`-based |
| `Microsoft.Xrm.Sdk.Entity` | `Koras.Dataverse.Entity` | Same late-bound idea, plain CLR values |
| `OptionSetValue` | `int` | Label via `Entity.FormattedValues` or metadata |
| `Money` | `decimal` | |
| `EntityReference` | `EntityReference` | Record `(TableName, Id)`; `@odata.bind` handled automatically |
| `ColumnSet` | `ColumnSet` | `ColumnSet.Of("a", "b")` / `ColumnSet.All` |
| `QueryExpression` | `ODataQuery` (or FetchXML) | Fluent, injection-safe |
| FetchXML strings | `FetchXml.For(…)…Build()` / `FetchXmlQuery.FromXml(xml)` | Builder encodes all values; existing XML wraps as-is |
| `RetrieveMultiple` + manual `PagingCookie` loop | `QueryAllAsync` / `FetchAllAsync` | `IAsyncEnumerable<Entity>`; paging automatic |
| `ExecuteMultipleRequest` | `BatchRequest` + `ExecuteBatchAsync` | Atomic change set by default; `Atomic = false` ≈ `ContinueOnError` |
| `UpsertRequest` | `UpsertAsync` (by id or alternate key) | Returns `UpsertResult(Id, Created)` |
| `AssociateRequest` / `DisassociateRequest` | `AssociateAsync` / `DisassociateAsync` | |
| `WhoAmIRequest` | `WhoAmIAsync` | |
| `RetrieveEntityRequest` / metadata requests | `client.Metadata` (`IMetadataClient`) | Lightweight typed models |
| Solution `ExportSolutionRequest` / `ImportSolutionRequest` | `client.Solutions` (`ISolutionClient`) | |
| `FaultException<OrganizationServiceFault>` | `DataverseException` (`DataverseError`) | Stable categories instead of fault codes |
| (retry/telemetry: roll your own) | Built in | Retry-After-aware retries; `ActivitySource`/`Meter` |

## Side by side

### Connect

```csharp
// ServiceClient
var service = new ServiceClient(
    "AuthType=ClientSecret;Url=https://contoso.crm.dynamics.com;ClientId=…;ClientSecret=…");

// Koras
builder.Services.AddDataverse(options =>
{
    options.EnvironmentUrl = new Uri("https://contoso.crm.dynamics.com");
    options.Authentication.UseClientSecret(tenantId, clientId, clientSecret);
});
// then inject IDataverseClient
```

### Create with typed values

```csharp
// ServiceClient
var account = new Microsoft.Xrm.Sdk.Entity("account");
account["name"] = "Contoso";
account["revenue"] = new Money(25000m);
account["accountcategorycode"] = new OptionSetValue(1);
account["primarycontactid"] = new Microsoft.Xrm.Sdk.EntityReference("contact", contactId);
Guid id = service.Create(account);

// Koras
var account = new Entity("account")
{
    ["name"] = "Contoso",
    ["revenue"] = 25_000m,                                    // no Money
    ["accountcategorycode"] = 1,                              // no OptionSetValue
    ["primarycontactid"] = new EntityReference("contact", contactId),
};
Guid id = await dataverse.CreateAsync(account, ct);
```

### Query with paging

```csharp
// ServiceClient: QueryExpression + manual paging loop
var query = new QueryExpression("account")
{
    ColumnSet = new ColumnSet("name"),
    Criteria = { Conditions = { new ConditionExpression("statecode", ConditionOperator.Equal, 0) } },
    PageInfo = new PagingInfo { PageNumber = 1, Count = 5000 },
};
EntityCollection page;
do
{
    page = service.RetrieveMultiple(query);
    foreach (var row in page.Entities) { /* … */ }
    query.PageInfo.PageNumber++;
    query.PageInfo.PagingCookie = page.PagingCookie;
} while (page.MoreRecords);

// Koras: one enumeration, paging handled
var query = ODataQuery.For("account").Select("name").Where(f => f.Eq("statecode", 0));
await foreach (Entity row in dataverse.QueryAllAsync(query, ct)) { /* … */ }
```

### Bulk writes

```csharp
// ServiceClient
var requests = new ExecuteMultipleRequest
{
    Settings = new ExecuteMultipleSettings { ContinueOnError = true, ReturnResponses = true },
    Requests = new OrganizationRequestCollection(),
};
// …add CreateRequest per row…
var responses = (ExecuteMultipleResponse)service.Execute(requests);

// Koras
var batch = new BatchRequest { Atomic = false }; // ≈ ContinueOnError = true
foreach (var row in rows)
{
    batch.AddCreate(row);
}
BatchResponse response = await dataverse.ExecuteBatchAsync(batch, ct);
foreach (BatchItemResult item in response.Results.Where(r => !r.Succeeded)) { /* … */ }
```

### Error handling

```csharp
// ServiceClient
try { service.Create(account); }
catch (FaultException<OrganizationServiceFault> fault)
{
    if (fault.Detail.ErrorCode == -2147220937) { /* duplicate */ }
}

// Koras
try { await dataverse.CreateAsync(account, ct); }
catch (DataverseException exception) when (exception.Category == DataverseErrorCategory.Concurrency)
{
    // duplicate/conflict — no magic numbers; Error.ErrorCode still has the raw code if needed
}
```

## Behavioral differences to plan for

- **Async everywhere.** There are no sync methods. Callers become `async` up the chain.
- **Choice/money values change type.** Anything that pattern-matched `OptionSetValue`/`Money`
  now reads `int`/`decimal`. Formatted labels move to `Entity.FormattedValues`.
- **Early-bound classes don't carry over.** CrmSvcUtil-generated types are tied to
  `Microsoft.Xrm.Sdk`. Use attribute-mapped POCOs for now
  ([recipes](../recipes/common-scenarios.md)); source-generated models are planned.
- **Update is strict.** `UpdateAsync` never creates (the SDK sends `If-Match: *`); use
  `UpsertAsync` for create-or-update semantics.
- **Retry/throttling handling is built in** — delete your custom 429 wrappers; tune
  `options.Retry` instead.
- **No `IOrganizationService` messages.** Specialized messages beyond the mapped surface
  (CRUD, batch, associate, metadata, solutions, WhoAmI) have no direct equivalent yet; the
  planned v1.1 `Koras.Dataverse.OrganizationService` package targets code that genuinely needs
  the message model.
- **Plugins stay on `IOrganizationService`.** Only the FetchXML builder package is usable in
  plugin assemblies.
