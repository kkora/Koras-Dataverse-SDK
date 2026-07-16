# API Reference Overview

The public surface, indexed by package and namespace, with one-line summaries.

Every public type and member ships with **XML documentation** in the packages, so IntelliSense
carries usage guidance, contracts (thread safety, exceptions), and examples into your editor.
Generated browsable reference pages (DocFX) are planned; until then, this index plus
IntelliSense is the reference, and the linked concept pages carry the depth.

## Koras.Dataverse.Abstractions

### `Koras.Dataverse`

| Type | Summary |
|---|---|
| `IDataverseClient` | The client contract: CRUD, OData/FetchXML queries with auto-paging, associations, batches, `WhoAmI`, and the `Metadata`/`Solutions` sub-clients |
| `IDataverseClientFactory` | Resolves named clients in multi-environment applications |
| `Entity` | Late-bound row: table logical name, id, attribute bag of plain CLR values, `FormattedValues`, typed `GetValue<T>`/`TryGetValue<T>` |
| `EntityReference` | Immutable record pointing at a row; the value of lookup columns |
| `ColumnSet` | Column selection for retrieves (`Of(…)` / `All`) |
| `DataverseQueryResult` | One query result page: `Entities`, `NextLink`, `PagingCookie`, `TotalCount`, `MoreRecords` |
| `UpsertResult` | Upsert outcome: `Id` + `Created` flag |
| `WhoAmIResponse` | `UserId`, `BusinessUnitId`, `OrganizationId` of the authenticated caller |

### `Koras.Dataverse.Queries`

| Type | Summary |
|---|---|
| `ODataQuery` | Fluent OData query (`For`, `Select`, `Where`, `OrderBy`, `Expand`, `Top`, `IncludeCount`, `PageSize`, `ToQueryString`) |
| `ODataFilterBuilder` | Injection-safe `$filter` builder: comparisons, string functions, null checks, `In`, nested `And`/`Or`/`Not`, `Raw` escape hatch, static `Literal` |

### `Koras.Dataverse.Batches`

| Type | Summary |
|---|---|
| `BatchRequest` | Up to 1,000 write operations for one `$batch`; `Atomic` (default true), `AddCreate`/`AddUpdate`/`AddUpsert`/`AddDelete` |
| `BatchOperation` / `BatchOperationType` | One queued operation and its kind |
| `BatchResponse` | Per-item results in request order; `Succeeded` convenience |
| `BatchItemResult` | Index, HTTP status, created id, and normalized error per operation |

### `Koras.Dataverse.Errors`

| Type | Summary |
|---|---|
| `DataverseException` | The single exception type for Dataverse failures; exposes `Error`, `Category`, `IsTransient` |
| `DataverseError` | Normalized failure record: category, message, HTTP status, Dataverse error code, request id, `Retry-After`, transient flag |
| `DataverseErrorCategory` | Stable failure taxonomy: `Authentication`, `Authorization`, `NotFound`, `Concurrency`, `Throttling`, `Validation`, `Timeout`, `Network`, `Server`, `Unknown` |

### `Koras.Dataverse.Metadata`

| Type | Summary |
|---|---|
| `IMetadataClient` | Read-only metadata: tables, columns, relationships, choices, entity set names |
| `TableMetadata` / `ColumnMetadata` / `RelationshipMetadata` (+ `RelationshipKind`) | Lightweight typed metadata models |
| `ChoiceOption` | One choice option: value, label, optional color |

### `Koras.Dataverse.Solutions`

| Type | Summary |
|---|---|
| `ISolutionClient` | Solution export/import/publish-all/find |
| `SolutionInfo` | Installed-solution summary (unique name, version, managed flag, …) |
| `SolutionImportOptions` | Import behavior: overwrite customizations, publish workflows, convert to managed, import job id |

### `Koras.Dataverse.Authentication`

| Type | Summary |
|---|---|
| `IDataverseTokenProvider` | Token-supply abstraction; implement to plug in any token source |

### `Koras.Dataverse.Mapping`

| Type | Summary |
|---|---|
| `DataverseTableAttribute` / `DataverseColumnAttribute` | Map a class/property to a table/column logical name |
| `EntityMapper` | `ToEntity(poco)`, `entity.ToObject<T>()`, `TableNameOf<T>()`; reflection cached per type |

## Koras.Dataverse.FetchXml

### `Koras.Dataverse.FetchXml`

| Type | Summary |
|---|---|
| `FetchXml` | Entry point: `FetchXml.For(tableName)` starts a builder |
| `FetchXmlBuilder` | Fluent query: attributes, distinct, filters, ordering, links, top/page |
| `FetchXmlQuery` | Immutable validated query; `FromXml` wraps existing text, `WithPage` repositions for paging |
| `FetchFilterBuilder` | XML-encoded conditions (`Eq`…`Le`, `Like`, `In`, null checks, date operators) with nested `And`/`Or` groups; explicit `Condition(column, operator, values)` |
| `FetchLinkEntityBuilder` | `link-entity` joins: alias, attributes, filters, nested links |
| `FetchConditionOperator` | The supported FetchXML operators |
| `FetchLinkType` | `Inner` / `Outer` join |

## Koras.Dataverse

### `Koras.Dataverse`

| Type | Summary |
|---|---|
| `DataverseClient` | The `IDataverseClient` implementation over the Web API; `Create(options, …)` for non-DI use, public `HttpClient`-taking constructor for tests |
| `DataverseClientOptions` | Environment URL, API version, timeout, annotations, retry, auth, entity set overrides |
| `DataverseRetryOptions` | `MaxRetries`, `BaseDelay`, `MaxDelay`, `RespectRetryAfter` |
| `DataverseAuthenticationOptions` (+ `DataverseAuthenticationKind`) | The `Use…` credential selection methods and resulting state |

### `Koras.Dataverse.Authentication`

| Type | Summary |
|---|---|
| `TokenCredentialTokenProvider` | Default provider: adapts `Azure.Core.TokenCredential`, caches, single-flight refresh five minutes before expiry |

### `Koras.Dataverse.HealthChecks`

| Type | Summary |
|---|---|
| `DataverseHealthCheck` | `IHealthCheck` probing connectivity + auth with `WhoAmI` |

### `Koras.Dataverse.Diagnostics`

| Type | Summary |
|---|---|
| `DataverseDiagnostics` | Public constants: `ActivitySourceName` / `MeterName`, both `"Koras.Dataverse"` |

### `Koras.Dataverse.DependencyInjection`

| Type | Summary |
|---|---|
| `DataverseBuilder` | Fluent continuation of `AddDataverse`: `AddHttpMessageHandler`, `Services`, `Name`, `HttpClientBuilder` |

### `Microsoft.Extensions.DependencyInjection`

| Type | Summary |
|---|---|
| `DataverseServiceCollectionExtensions` | `AddDataverse(configure)`, `AddDataverse(name, configure)`, `AddDataverseHealthCheck(…)`, `DefaultClientName` |

## Koras.Dataverse.OpenTelemetry

### `Koras.Dataverse.OpenTelemetry`

| Type | Summary |
|---|---|
| `DataverseOpenTelemetryExtensions` | `AddKorasDataverseInstrumentation()` for `TracerProviderBuilder` and `MeterProviderBuilder` |

## Related

- [Core abstractions with snippets](../concepts/core-abstractions.md)
- [All configuration options](../configuration/all-options.md)
- [Public API design principles](../api/public-api-design.md)
