# Public API Design

> Expands §4 of [`docs/planning/master-plan.md`](../planning/master-plan.md). If this document
> and the master plan disagree, the master plan wins. This is a pre-implementation design
> document: signatures shown here are the design target for `0.1.0-preview.1`. Where the
> master plan does not specify a detail, the design below is conservative and marked
> **subject to implementation review**; such details may be adjusted during the preview window
> under the PublicAPI-file process (ADR-0010).

## 1. Design rules

These rules apply to every public type and member; the review checklist
([`public-api-review-checklist.md`](public-api-review-checklist.md)) enforces them per PR.

1. **Async-first.** Every I/O member is async-only (`Task`, `Task<T>`, `ValueTask<T>`,
   `IAsyncEnumerable<T>`) with the `Async` suffix. No sync counterparts, no sync-over-async.
2. **Cancellation everywhere.** Every I/O member takes `CancellationToken cancellationToken
   = default` as its last parameter. `IAsyncEnumerable<T>`-returning members also honor
   `WithCancellation` via `[EnumeratorCancellation]`.
3. **Sealed by default.** Public types are `sealed` (or `abstract`/`static` where that is the
   point). Unsealing is a deliberate, reviewed decision.
4. **No bool overload traps.** No public member takes a bare positional `bool` that changes
   behavior; use enums, options types, or distinct member names.
5. **No statics except builder entry points.** The only public static members are pure
   builder entry points (`ODataQuery.For`, `FetchXml.For`) and DI extension methods.
6. **No third-party types in Abstractions.** Nothing from Azure.*, System.Net.Http, or
   Microsoft.Extensions.* appears on any `Abstractions` signature. The single deliberate
   leak elsewhere is `UseTokenCredential(TokenCredential)` in the core package (ADR-0004).
7. **Plain CLR values** (ADR-0005): attribute values are `string`/`int`/`decimal`/`bool`/
   `Guid`/`DateTime(Offset)`/collections; `EntityReference` is the only wrapper (lookups).
8. **Records for immutable models**; mutable types only where mutation is the purpose
   (`Entity` under construction, builders, options).
9. **Nullable reference types everywhere**; annotations are contract (see
   [`backward-compatibility.md`](backward-compatibility.md)).
10. **Interfaces for everything injectable**; consumers mock `IDataverseClient` and friends
    without the implementation package.
11. **Exceptions, not result types** (ADR-0006): Dataverse failures throw
    `DataverseException`; `OperationCanceledException` is never wrapped; argument misuse
    throws standard BCL exceptions (`ArgumentNullException`, `ArgumentException`,
    `ArgumentOutOfRangeException`) — these apply to all members below and are not repeated
    per member.

Thread-safety shorthand used below: **service** = thread-safe singleton; **immutable** =
safe to share freely; **builder** = mutable, single-threaded until `Build`/send
(see [`../architecture/overview.md`](../architecture/overview.md) §3).

---

## 2. Namespace `Koras.Dataverse`

Shipped in `Koras.Dataverse.Abstractions` (contracts and models) with implementation in
`Koras.Dataverse`.

### 2.1 `IDataverseClient`

- **Purpose.** The primary client contract: CRUD + upsert (KDV-002), OData query execution
  and auto-paging (KDV-003), FetchXML execution (KDV-004), batch (KDV-005), `WhoAmI`;
  gateway to the sub-clients.
- **Thread-safety.** Service. **Lifetime.** Singleton via DI; not `IDisposable` (the
  underlying `HttpClient` lifecycle belongs to `IHttpClientFactory`).

```csharp
public interface IDataverseClient
{
    Task<Guid> CreateAsync(Entity entity, CancellationToken cancellationToken = default);

    Task<Entity> RetrieveAsync(string tableName, Guid id, ColumnSet columns,
        CancellationToken cancellationToken = default);
    Task<Entity?> TryRetrieveAsync(string tableName, Guid id, ColumnSet columns,
        CancellationToken cancellationToken = default); // null instead of NotFound throw

    Task UpdateAsync(Entity entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(string tableName, Guid id, CancellationToken cancellationToken = default);

    Task<UpsertResult> UpsertAsync(Entity entity, CancellationToken cancellationToken = default);

    Task<DataverseQueryResult> QueryAsync(ODataQuery query,
        CancellationToken cancellationToken = default);
    IAsyncEnumerable<Entity> QueryAllAsync(ODataQuery query,
        CancellationToken cancellationToken = default);

    Task<DataverseQueryResult> FetchAsync(FetchXmlQuery query,
        CancellationToken cancellationToken = default);
    IAsyncEnumerable<Entity> FetchAllAsync(FetchXmlQuery query,
        CancellationToken cancellationToken = default);

    Task<BatchResponse> ExecuteBatchAsync(BatchRequest batch,
        CancellationToken cancellationToken = default);

    Task<WhoAmIResponse> WhoAmIAsync(CancellationToken cancellationToken = default);

    IMetadataClient Metadata { get; }
    ISolutionClient Solutions { get; }
}
```

- **Notes.** Alternate-key addressing and typed POCO mapping overloads (KDV-002) extend this
  surface; their exact shapes (key-value pair addressing type, generic
  `RetrieveAsync<T>`) are **subject to implementation review**. Entity ids for
  update/delete come from `Entity.Id`/explicit parameters; `RetrieveAsync` throws
  `DataverseException` (`NotFound`) for missing rows while `TryRetrieveAsync` returns
  `null` (expected-absence pattern, ADR-0006).
- **Exceptions.** `DataverseException` for all Dataverse failures;
  `OperationCanceledException` on caller cancellation.

**Sample**

```csharp
var account = new Entity("account") { ["name"] = "Contoso", ["revenue"] = 25_000m };
Guid id = await dataverse.CreateAsync(account, ct);

Entity fetched = await dataverse.RetrieveAsync("account", id, new ColumnSet("name", "revenue"), ct);

await foreach (var row in dataverse.QueryAllAsync(
    ODataQuery.For("account").Select("name").Where(f => f.Eq("statecode", 0)), ct))
{
    Console.WriteLine(row["name"]);
}
```

### 2.2 `Entity`

- **Purpose.** Late-bound row: a table logical name, an optional id, and a bag of
  plain-CLR attribute values (ADR-0005).
- **Constructors.** `public Entity(string tableName)`,
  `public Entity(string tableName, Guid id)`.
- **Thread-safety.** Mutable while being composed by its creator; instances returned by the
  SDK are not mutated further by the SDK and should be treated as read-and-forward data.
  Not safe for concurrent mutation.

```csharp
public sealed class Entity
{
    public Entity(string tableName);
    public Entity(string tableName, Guid id);

    public string TableName { get; }
    public Guid Id { get; set; }                       // Guid.Empty until assigned/created

    public object? this[string attributeName] { get; set; }

    public bool Contains(string attributeName);
    public bool TryGetValue<T>(string attributeName, out T? value);
    public T? GetValue<T>(string attributeName);        // InvalidCastException on type mismatch
    public IReadOnlyCollection<string> AttributeNames { get; }
}
```

- **Nullability.** Indexer accepts and returns `object?`; `null` means "set attribute to
  null on write" / "attribute is null" on read; an absent attribute is distinguished via
  `Contains`/`TryGetValue`.
- **Notes.** Formatted-value access and enumeration of raw attributes are
  **subject to implementation review**. `Entity` performs no I/O and throws only argument
  and cast exceptions.

### 2.3 `EntityReference`

- **Purpose.** The single wrapper type: a lookup target (table logical name + id), also used
  for `@odata.bind` generation and `_field_value` materialization (ADR-0005).
- **Shape.** `public sealed record EntityReference(string TableName, Guid Id)`. Immutable.
- **Notes.** An alternate-key form of `EntityReference` (name + key/value pairs) is
  anticipated for KDV-002 alternate keys; its shape is **subject to implementation review**.

### 2.4 `ColumnSet`

- **Purpose.** The set of columns to retrieve.
- **Shape.**

```csharp
public sealed class ColumnSet
{
    public ColumnSet(params string[] columns);
    public static ColumnSet All { get; }        // explicit opt-in to all columns
    public IReadOnlyList<string> Columns { get; }
}
```

- **Thread-safety.** Immutable after construction.
- **Notes.** `All` translates to no `$select` (documented as a performance anti-pattern in
  guides). Static `All` property is a permitted pure accessor, not a builder entry point
  violation (it allocates nothing per call).

### 2.5 `DataverseQueryResult`

- **Purpose.** One page of query results (OData or FetchXML) plus continuation state.
- **Shape.** Immutable record: `IReadOnlyList<Entity> Entities`, `bool HasMore`,
  continuation token (OData `@odata.nextLink` or FetchXML paging cookie, normalized),
  optional total count when requested. Exact continuation-member shape is **subject to
  implementation review**; `QueryAllAsync`/`FetchAllAsync` are the primary paging surface
  and hide it entirely.
- **Thread-safety.** Immutable.

### 2.6 `WhoAmIResponse`

- **Purpose.** Result of the `WhoAmI` function; also used by the health check (KDV-012).
- **Shape.** `public sealed record WhoAmIResponse(Guid UserId, Guid BusinessUnitId,
  Guid OrganizationId);` Immutable.

### 2.7 `UpsertResult`

- **Purpose.** Result of `UpsertAsync`: the row id and whether the row was created or
  updated.
- **Shape.** `public sealed record UpsertResult(Guid Id, bool WasCreated);` Immutable.
  (`WasCreated` is a result datum, not a behavioral bool parameter — rule 4 concerns
  parameters.)

### 2.8 `DataverseClientOptions`

- **Purpose.** Options-pattern root for one (named) client (KDV-010).
- **Shape.**

```csharp
public sealed class DataverseClientOptions
{
    [Required] public Uri? EnvironmentUrl { get; set; }          // must be HTTPS
    public DataverseAuthenticationOptions Authentication { get; } // initialized, never null
    public DataverseRetryOptions Retry { get; }                   // initialized, never null
    public TimeSpan Timeout { get; set; }                         // per-request timeout; default subject to implementation review
}
```

- **Thread-safety.** Mutable during configuration; treated as immutable after validation at
  startup (validate-on-start; non-HTTPS `EnvironmentUrl` fails validation — master plan §7).
- **Constructor.** Public parameterless (options-pattern requirement).

### 2.9 `DataverseAuthenticationOptions`

- **Purpose.** Credential selection for KDV-001. Configured only through its `Use*` methods —
  it stores the selection; the core package builds the provider.

```csharp
public sealed class DataverseAuthenticationOptions
{
    public void UseClientSecret(string tenantId, string clientId, string clientSecret);
    public void UseCertificate(string tenantId, string clientId, X509Certificate2 certificate);
    public void UseManagedIdentity(string? clientId = null);   // user-assigned via clientId
    public void UseInteractive();                              // dev-time browser flow
    public void UseDefault();                                  // DefaultAzureCredential
    public void UseTokenCredential(TokenCredential credential);
    public void UseTokenProvider<TProvider>() where TProvider : class, IDataverseTokenProvider;
}
```

- **Placement note.** The `TokenCredential`- and certificate-typed members force this
  concrete type's Azure.Core/X509 references; per master plan §2/§4 the options live with
  the Abstractions error/options models while `Abstractions` must stay dependency-free.
  Resolution: the dependency-free selection state lives in Abstractions, and the
  Azure-typed `Use*` members ship as extension methods (or a derived options view) in the
  core package — the split is **subject to implementation review**; the consumer-visible
  call shape above is the contract.
- **Exceptions.** `ArgumentException` family on invalid inputs; selecting a credential twice
  replaces the prior selection (last-wins, documented).

### 2.10 `DataverseRetryOptions`

- **Purpose.** Tuning for the built-in retry handler (KDV-008, ADR-0007).
- **Shape (names/defaults subject to implementation review; semantics fixed).** Maximum
  retry attempts, base delay, maximum delay cap, overall retry budget, honor-`Retry-After`
  toggle-cap, and an off switch for consumers supplying their own outer resilience.
  Defaults follow Dataverse service-protection guidance; validated by DataAnnotations at
  startup.

---

## 3. Namespace `Koras.Dataverse.Queries`

Shipped in `Koras.Dataverse.Abstractions` (the query description is transport-neutral);
executed by the core package.

### 3.1 `ODataQuery`

- **Purpose.** Fluent, injection-safe OData query description (KDV-003).
- **Entry point.** `public static ODataQuery For(string tableName)` — one of the two
  permitted static builder entry points.
- **Thread-safety.** Builder.

```csharp
public sealed class ODataQuery
{
    public static ODataQuery For(string tableName);

    public ODataQuery Select(params string[] columns);
    public ODataQuery Where(Action<ODataFilterBuilder> filter);
    public ODataQuery OrderBy(string column);
    public ODataQuery OrderByDescending(string column);
    public ODataQuery Top(int count);                       // ArgumentOutOfRangeException if < 1
    public ODataQuery Expand(string navigationProperty, Action<ODataExpand>? configure = null);
    public ODataQuery IncludeCount();
}
```

- **Encoding.** All values pass through strict OData literal encoding (strings quoted and
  escaped, GUIDs/dates formatted invariantly) — consumers never concatenate filter strings
  (master plan §7).
- **Notes.** The rendered `$filter`/query string is produced internally at execution; whether
  a public `ToQueryString()` diagnostic member is exposed is **subject to implementation
  review**.

### 3.2 `ODataFilterBuilder`

- **Purpose.** Composes `$filter` expressions safely.
- **Thread-safety.** Builder; only valid within the `Where(...)` callback.

```csharp
public sealed class ODataFilterBuilder
{
    public ODataFilterBuilder Eq(string column, object? value);
    public ODataFilterBuilder Ne(string column, object? value);
    public ODataFilterBuilder Gt(string column, object value);
    public ODataFilterBuilder Ge(string column, object value);
    public ODataFilterBuilder Lt(string column, object value);
    public ODataFilterBuilder Le(string column, object value);
    public ODataFilterBuilder Contains(string column, string value);
    public ODataFilterBuilder StartsWith(string column, string value);
    public ODataFilterBuilder EndsWith(string column, string value);
    public ODataFilterBuilder In(string column, IEnumerable<object> values);
    public ODataFilterBuilder IsNull(string column);
    public ODataFilterBuilder IsNotNull(string column);
    public ODataFilterBuilder And(Action<ODataFilterBuilder> group);
    public ODataFilterBuilder Or(Action<ODataFilterBuilder> group);
    public ODataFilterBuilder Not(Action<ODataFilterBuilder> group);
}
```

- **Semantics.** Sibling conditions combine with `and` by default; `Or`/`And`/`Not` create
  parenthesized groups. Values are encoded per their CLR type; unsupported value types throw
  `ArgumentException` at build time, not at request time.

### 3.3 `ODataExpand`

- **Purpose.** Configures one `$expand` clause: nested `Select`, `Top`, and filter for the
  expanded navigation. Builder; members mirror the subset of `ODataQuery` valid inside
  `$expand` (**exact subset subject to implementation review**).

---

## 4. Namespace `Koras.Dataverse.FetchXml`

Shipped in the standalone `Koras.Dataverse.FetchXml` package (netstandard2.0-compatible,
zero dependencies). Produces FetchXML; never executes it.

### 4.1 `FetchXml` (entry point)

- **Purpose.** Static entry point — the second permitted static builder entry.
- **Shape.** `public static class FetchXml { public static FetchXmlQuery For(string tableName); }`

### 4.2 `FetchXmlQuery`

- **Purpose.** Fluent FetchXML builder and the built artifact handed to
  `IDataverseClient.FetchAsync` (KDV-004).
- **Thread-safety.** Builder until `Build()`; the built XML string is immutable.

```csharp
public sealed class FetchXmlQuery
{
    public FetchXmlQuery Attributes(params string[] attributeNames);
    public FetchXmlQuery AllAttributes();
    public FetchXmlQuery Where(Action<FetchFilterBuilder> filter);
    public FetchXmlQuery Link(string tableName, string from, string to,
        Action<FetchLinkEntityBuilder>? configure = null);
    public FetchXmlQuery OrderBy(string attributeName, bool descending = false);
    public FetchXmlQuery Top(int count);
    public FetchXmlQuery Page(int pageNumber, int pageSize, string? pagingCookie = null);
    public FetchXmlQuery Distinct();
    public FetchXmlQuery NoLock();

    public string Build();      // renders the FetchXML document
}
```

- **`OrderBy` note.** The `descending` parameter is an optional named-usage flag with a safe
  default, paired with the OData builder's split methods; whether FetchXml also splits into
  `OrderByDescending` for symmetry is **subject to implementation review** (rule 4 targets
  behavior-switching positional bools; call sites are expected to write
  `OrderBy("name", descending: true)`).
- **Encoding.** Every attribute name and value is XML-escaped; condition values additionally
  validated per operator (master plan §7). `Build()` throws `InvalidOperationException` on
  contradictory state (e.g., `Top` combined with `Page`).

### 4.3 `FetchFilterBuilder`

- **Purpose.** FetchXML `<filter>`/`<condition>` composition.

```csharp
public sealed class FetchFilterBuilder
{
    public FetchFilterBuilder Eq(string attribute, object? value);
    public FetchFilterBuilder Ne(string attribute, object? value);
    public FetchFilterBuilder Gt(string attribute, object value);
    public FetchFilterBuilder Ge(string attribute, object value);
    public FetchFilterBuilder Lt(string attribute, object value);
    public FetchFilterBuilder Le(string attribute, object value);
    public FetchFilterBuilder Like(string attribute, string pattern);
    public FetchFilterBuilder In(string attribute, IEnumerable<object> values);
    public FetchFilterBuilder Null(string attribute);
    public FetchFilterBuilder NotNull(string attribute);
    public FetchFilterBuilder Condition(string attribute, FetchConditionOperator op, object? value = null);
    public FetchFilterBuilder And(Action<FetchFilterBuilder> group);
    public FetchFilterBuilder Or(Action<FetchFilterBuilder> group);
}
```

- `Condition(...)` is the escape hatch to the full operator set without a named helper per
  operator.

### 4.4 `FetchLinkEntityBuilder`

- **Purpose.** Configures a `<link-entity>`: alias, attributes, link type, nested filters,
  nested links.

```csharp
public sealed class FetchLinkEntityBuilder
{
    public FetchLinkEntityBuilder Alias(string alias);
    public FetchLinkEntityBuilder Attributes(params string[] attributeNames);
    public FetchLinkEntityBuilder Outer();      // default is inner join
    public FetchLinkEntityBuilder Where(Action<FetchFilterBuilder> filter);
    public FetchLinkEntityBuilder Link(string tableName, string from, string to,
        Action<FetchLinkEntityBuilder>? configure = null);
}
```

### 4.5 `FetchConditionOperator`

- **Purpose.** Enum covering FetchXML condition operators (`Eq`, `Ne`, `Gt`, `Ge`, `Lt`,
  `Le`, `Like`, `NotLike`, `In`, `NotIn`, `Null`, `NotNull`, `On`, `OnOrAfter`,
  `OnOrBefore`, `Between`, `BeginsWith`, `EndsWith`, and the date/hierarchy operators —
  full member list finalized against the FetchXML schema, **subject to implementation
  review**). Enum members map 1:1 to FetchXML `operator` attribute values.

**Sample (from master plan §4)**

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

> `FetchAsync` accepts the built `FetchXmlQuery`. Hand-written FetchXML text is executed by
> wrapping it first: `dataverse.FetchAsync(FetchXmlQuery.FromXml(xmlText), ct)` — `FromXml`
> validates well-formedness and extracts the target table, so no raw-`string` overload exists.

---

## 5. Namespace `Koras.Dataverse.Batches`

Shipped in `Koras.Dataverse.Abstractions`.

### 5.1 `BatchRequest`

- **Purpose.** Composes a `$batch` payload (KDV-005): individual operations and atomic
  change sets, with continue-on-error control and the 1000-operation guard.
- **Thread-safety.** Builder.

```csharp
public sealed class BatchRequest
{
    public BatchRequest();

    public BatchRequest Add(BatchOperation operation);          // 1000-op guard: InvalidOperationException
    public BatchRequest AddChangeSet(params BatchOperation[] operations); // atomic group
    public bool ContinueOnError { get; set; }                   // default false
    public IReadOnlyList<BatchOperation> Operations { get; }
}
```

### 5.2 `BatchOperation`

- **Purpose.** One operation inside a batch: create/update/delete/upsert of an `Entity`.
- **Shape.** Immutable; created via static factories
  (`BatchOperation.Create(Entity)`, `.Update(Entity)`, `.Delete(string tableName, Guid id)`,
  `.Upsert(Entity)`). These factories are pure builders of an immutable description and fall
  under the builder-entry-point allowance. Content-ID referencing between change-set
  operations is **subject to implementation review**.

### 5.3 `BatchResponse`

- **Purpose.** Result of `ExecuteBatchAsync`: per-item results in request order.
- **Shape.** Immutable record: `IReadOnlyList<BatchItemResult> Results`, plus convenience
  `bool AllSucceeded`.

### 5.4 `BatchItemResult`

- **Purpose.** Outcome of one operation: success flag, created id when applicable, and the
  `DataverseError` when the item failed under continue-on-error.
- **Shape.** `public sealed record BatchItemResult(int Index, bool Succeeded, Guid? Id,
  DataverseError? Error);` Immutable. (`Succeeded` is result data, not a parameter.)
- **Error interaction.** See [`../architecture/error-model.md`](../architecture/error-model.md)
  §4: atomic change-set failures throw; continue-on-error failures land here.

---

## 6. Namespace `Koras.Dataverse.Metadata`

Interface and models in `Koras.Dataverse.Abstractions`; implementation in the core package.

### 6.1 `IMetadataClient`

- **Purpose.** Read-only metadata access (KDV-006): tables, columns, choices, relationships.
- **Thread-safety.** Service.

```csharp
public interface IMetadataClient
{
    Task<TableMetadata> GetTableAsync(string logicalName,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TableMetadata>> GetTablesAsync(
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ColumnMetadata>> GetColumnsAsync(string logicalName,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChoiceOption>> GetChoicesAsync(string logicalName, string columnLogicalName,
        CancellationToken cancellationToken = default);   // local choices
    Task<IReadOnlyList<ChoiceOption>> GetGlobalChoicesAsync(string choiceName,
        CancellationToken cancellationToken = default);   // global option sets
    Task<IReadOnlyList<RelationshipMetadata>> GetRelationshipsAsync(string logicalName,
        CancellationToken cancellationToken = default);
}
```

- **Exceptions.** `DataverseException` (`NotFound` for unknown logical names).

### 6.2 Metadata models

All immutable records; "typed lightweight models" (KDV-006) — deliberately not the full
`EntityMetadata` shape:

- `TableMetadata` — logical name, schema name, display name, primary id/name attributes,
  entity-set name, ownership type flags.
- `ColumnMetadata` — logical name, display name, column type, required level,
  max length/precision where applicable, targets for lookup columns.
- `RelationshipMetadata` — schema name, relationship kind (one-to-many / many-to-many),
  referencing/referenced tables and attributes (or intersect table for N:N).
- `ChoiceOption` — `public sealed record ChoiceOption(int Value, string Label);`

Exact property lists are **subject to implementation review**; the models above define the
minimum contract.

---

## 7. Namespace `Koras.Dataverse.Solutions`

Interface and models in `Koras.Dataverse.Abstractions`; implementation in the core package.

### 7.1 `ISolutionClient`

- **Purpose.** Solution operations (KDV-007): export, import (async-job polled),
  publish-all, query installed.
- **Thread-safety.** Service.

```csharp
public interface ISolutionClient
{
    Task<byte[]> ExportAsync(string solutionName, bool managed = false,
        CancellationToken cancellationToken = default);
    Task ImportAsync(byte[] solutionFile, SolutionImportOptions? options = null,
        CancellationToken cancellationToken = default);   // polls the async import job to completion
    Task PublishAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SolutionInfo>> GetInstalledAsync(
        CancellationToken cancellationToken = default);
}
```

- **`managed` parameter note.** Named-usage optional flag mirroring the platform's own
  export concept; call sites are expected to write `managed: true`. A two-method or
  enum-based split remains open **subject to implementation review** under rule 4.
- **Streaming.** `Stream`-based export/import overloads for large solutions are anticipated
  with file-column work (KDV-014) — not in the MVP contract.
- **Exceptions.** `DataverseException`; an import-job failure surfaces the job's error detail
  in `DataverseError.Message` with category per the mapping table.

### 7.2 `SolutionInfo`

Immutable record: unique name, friendly name, `Version` (string as reported by Dataverse),
`bool IsManaged`, publisher unique name, installed-on timestamp.

### 7.3 `SolutionImportOptions`

Mutable options object (consistent with the options family): overwrite unmanaged
customizations, publish workflows, holding-solution/upgrade behavior, poll interval for the
import job. Property list **subject to implementation review**; defaults match the platform's
conservative defaults.

---

## 8. Namespace `Koras.Dataverse.Errors`

Shipped in `Koras.Dataverse.Abstractions`. Full design in
[`../architecture/error-model.md`](../architecture/error-model.md); summarized here for the
API inventory.

- **`DataverseErrorCategory`** — enum: `Unknown = 0`, `Authentication`, `Authorization`,
  `NotFound`, `Concurrency`, `Throttling`, `Validation`, `Timeout`, `Network`, `Server`.
- **`DataverseError`** — sealed immutable record: `Category`, `DataverseErrorCode`
  (`string?`), `HttpStatusCode` (`int?`), `Message` (`string`, never null, safe to log),
  `RequestId` (`string?`), `RetryAfter` (`TimeSpan?`), `IsTransient` (`bool`).
- **`DataverseException`** — deliberately non-sealed exception (the one intentional
  inheritance point) exposing `Error` plus convenience `Category`/`IsTransient`
  pass-throughs. Public constructors accept a `DataverseError` (and optional inner
  exception); it is constructible by consumers for test doubles.
- **Thread-safety.** Immutable.

---

## 9. Namespace `Koras.Dataverse.Authentication`

### 9.1 `IDataverseTokenProvider`

- **Package.** `Koras.Dataverse.Abstractions`. **Purpose.** The auth seam (ADR-0004).
- **Thread-safety.** Implementations must be services (thread-safe, singleton-compatible).

```csharp
public interface IDataverseTokenProvider
{
    ValueTask<string> GetAccessTokenAsync(Uri environmentUrl,
        CancellationToken cancellationToken = default);
}
```

- `ValueTask` because the overwhelmingly common path is a synchronous cache hit.
  Implementations own caching and expiry (the default `TokenCredentialTokenProvider` caches
  until five minutes before expiry with single-flight refresh); the SDK passes the
  environment URL so one provider can serve multiple environments.
- **Exceptions.** Implementations should throw `DataverseException` with category
  `Authentication` for acquisition failures (the built-in provider wraps credential
  exceptions this way); `OperationCanceledException` flows through untouched.

### 9.2 Credential option helpers

The `Use*` selection members documented at §2.9 (`UseClientSecret`, `UseCertificate`,
`UseManagedIdentity`, `UseInteractive`, `UseDefault`, `UseTokenCredential`,
`UseTokenProvider`). Azure-typed members ship in the core package (see the placement note
in §2.9).

---

## 10. Namespace `Microsoft.Extensions.DependencyInjection`

Shipped in `Koras.Dataverse` (ADR-0003), except the `IDataverseClientFactory` interface,
which lives in `Abstractions` in the `Koras.Dataverse` namespace (see §10.3). *(Sections
below reflect the implemented surface.)*

### 10.1 `AddDataverse`

```csharp
public static class DataverseServiceCollectionExtensions
{
    public const string DefaultClientName = "Default";

    public static DataverseBuilder AddDataverse(this IServiceCollection services,
        Action<DataverseClientOptions> configure);
    public static DataverseBuilder AddDataverse(this IServiceCollection services,
        string name, Action<DataverseClientOptions> configure);
}
```

- Registers named options + validation (validate-on-start via `IValidateOptions`), a per-name
  cached token provider (keyed singleton), the named `HttpClient` `"Koras.Dataverse:{name}"`
  with the retry → auth → transport pipeline, the `IDataverseClientFactory` singleton, and a
  singleton `IDataverseClient` that binds to the `Default` registration (or the single named
  registration when only one exists; ambiguous otherwise).
- Returns `Koras.Dataverse.DependencyInjection.DataverseBuilder` (`Services`, `Name`,
  `HttpClientBuilder`, `AddHttpMessageHandler(...)`) for appending user `DelegatingHandler`s —
  see [`../architecture/extension-model.md`](../architecture/extension-model.md) §2.
- **Exceptions.** `ArgumentNullException`/`ArgumentException` at registration;
  `OptionsValidationException` at startup for invalid options (standard options-pattern
  behavior).

### 10.2 `AddDataverseHealthCheck`

```csharp
public static IHealthChecksBuilder AddDataverseHealthCheck(
    this IHealthChecksBuilder builder,
    string name = "dataverse",
    HealthStatus failureStatus = HealthStatus.Unhealthy,
    IEnumerable<string>? tags = null);
```

- Registers the `WhoAmI`-probe `DataverseHealthCheck` (KDV-012) against the default client.
  Probing a named client is done by registering `DataverseHealthCheck` manually with a
  factory-resolved client.

### 10.3 `IDataverseClientFactory`

```csharp
namespace Koras.Dataverse;

public interface IDataverseClientFactory
{
    IDataverseClient GetClient(string name);
}
```

- **Purpose.** Resolve named clients for multi-environment scenarios (KDV-010).
- **Thread-safety.** Service. Returned clients are cached singletons per name; callers do
  not dispose them. `GetClient` throws `InvalidOperationException` (listing registered
  names) for an unregistered name.
- **Placement.** Declared in the `Abstractions` assembly, `Koras.Dataverse` namespace, so
  test doubles stay dependency-free.

---

## 11. Cross-cutting exception summary

| Exception | Thrown by | Meaning |
|---|---|---|
| `DataverseException` | All I/O members | Dataverse operation failed (see error model) |
| `OperationCanceledException` | All I/O members | Caller's token canceled; never wrapped |
| `ArgumentNullException` / `ArgumentException` / `ArgumentOutOfRangeException` | Constructors, builders, all members | Caller bug; thrown eagerly, before I/O |
| `InvalidOperationException` | Builders (`Build`, batch guards), factory | Invalid state/composition (e.g., > 1000 batch ops, unknown client name) |
| `InvalidCastException` | `Entity.GetValue<T>` | Attribute value not of requested type |
| `OptionsValidationException` | Host startup | Invalid `DataverseClientOptions` |

No public member throws for a missing row except `RetrieveAsync` (by design, with
`TryRetrieveAsync` as the expected-absence alternative).
