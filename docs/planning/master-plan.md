# Koras Dataverse SDK — Master Plan (Source of Truth)

> This document is the canonical summary of all product, architecture, API, and delivery
> decisions for the Koras Dataverse SDK. Every other document under `docs/` elaborates on a
> section of this plan and must stay consistent with it. When a conflict is found, this file wins
> until the conflict is resolved by an ADR.

- **Package name:** `Koras.Dataverse`
- **Display name:** Koras Dataverse SDK
- **Organization:** Koras Technologies · **Root namespace:** `Koras`
- **License:** MIT · **Repository:** https://github.com/korastechnologies/koras-dataverse
- **Status:** Pre-release planning + MVP implementation (0.1.0-preview)

---

## 1. Product summary

**Vision.** Give every .NET developer working against Microsoft Dataverse a modern, fluent,
resilient SDK that removes the repetitive plumbing — authentication, retries, throttling, paging,
batching, query generation, metadata access, and error handling — behind a small, testable,
DI-native API surface.

**Problem.** The official `Microsoft.PowerPlatform.Dataverse.Client` (ServiceClient) carries a
legacy `IOrganizationService` programming model, heavyweight dependencies, connection-string
oriented auth, limited async ergonomics, and no first-class observability. Teams calling the
Dataverse Web API directly rebuild the same HTTP/auth/retry/paging/batch/error code in every
project. Both paths produce inconsistent, hard-to-test integration layers.

**Positioning.** *The HttpClientFactory-era Dataverse SDK*: Web API first, `TokenCredential`
(Azure.Identity) auth, `IAsyncEnumerable` paging, fluent + injection-safe query builders, strong
error taxonomy, ActivitySource/Meter telemetry, options-pattern configuration, health checks —
all mockable behind interfaces.

**Non-goals (out of scope).** UI components; data migration engines; plugin *execution* runtime;
XRM tooling replacements (pac CLI); Power Automate connectors; on-premises (pre-9.x) support;
anything requiring the WCF Organization Service in the core package.

### Competitive landscape (summary)

| Alternative | Gap the Koras SDK fills |
|---|---|
| `Microsoft.PowerPlatform.Dataverse.Client` | Legacy IOrganizationService model, heavy deps, weak DI/telemetry story, lock-based sync paths |
| Hand-rolled `HttpClient` + Web API | No error taxonomy, no throttling handling, repeated plumbing per project |
| `Microsoft.Xrm.Sdk` (plugins/on-prem) | Not applicable to modern service integration; .NET Framework heritage |
| `XrmToolBox`/pac CLI | Tools, not embeddable SDKs |
| Community wrappers (e.g., PowerPlatform.Api, Dataverse REST clients) | Partial coverage, low maintenance, no observability or resilience contracts |

**Differentiators.** (1) Injection-safe fluent FetchXML + OData builders with strict encoding;
(2) service-protection-limit-aware resilience by default; (3) plain CLR value model (no
`OptionSetValue`/`Money` wrappers); (4) first-class OpenTelemetry; (5) modular packages with a
dependency-free FetchXML builder usable inside plugins later; (6) documented, tested, semver-
disciplined public API.

**Adoption strategy.** OSS on GitHub (MIT), NuGet.org publication, five-minute quick start,
copy-paste recipes, samples for console/minimal API/worker, comparison guide vs ServiceClient,
answer-ready docs for common Dataverse pain (throttling, paging, batch, metadata). Monetization:
none planned; the SDK builds Koras Technologies' consulting credibility.

**Naming assessment.** `Koras.Dataverse` (NuGet ID + root namespace `Koras.Dataverse`) is short,
unambiguous, and does not collide with any existing NuGet ID (verified 2026-07). `Dataverse`
alone is Microsoft's product name; the `Koras.` prefix avoids trademark ambiguity. Prefix
reservation for `Koras.*` on NuGet.org is part of the release checklist.

---

## 2. Packages and boundaries

| Package | Contents | Dependencies | TFMs | MVP |
|---|---|---|---|---|
| `Koras.Dataverse.Abstractions` | Interfaces (`IDataverseClient`, `IMetadataClient`, `ISolutionClient`, `IDataverseTokenProvider`, `IDataverseClientFactory`), `Entity`, `EntityReference`, query/batch/metadata models, error model | `Koras.Dataverse.FetchXml` only (so `IDataverseClient.FetchAsync(FetchXmlQuery)` is strongly typed); no third-party deps | net8.0;net9.0;net10.0 | ✔ |
| `Koras.Dataverse.FetchXml` | Standalone fluent FetchXML builder (`FetchXml`, `FetchXmlQuery`, filters, links, ordering, paging cookies) | none | netstandard2.0;net8.0;net9.0;net10.0 | ✔ |
| `Koras.Dataverse` | Web API client implementation: CRUD, OData query builder + execution, FetchXML execution, `$batch`, paging, metadata client, solution client, auth (Azure.Identity), retry/throttling handler, logging, ActivitySource/Meter, health check, DI registration (`AddDataverse`) | Abstractions, FetchXml, Azure.Identity, Microsoft.Extensions.{Http, Options, Options.DataAnnotations, Logging.Abstractions, DependencyInjection.Abstractions, Diagnostics.HealthChecks.Abstractions} | net8.0;net9.0;net10.0 | ✔ |
| `Koras.Dataverse.OpenTelemetry` | `TracerProviderBuilder`/`MeterProviderBuilder` extensions to enable SDK instrumentation | Koras.Dataverse (ids only), OpenTelemetry.Api | net8.0;net9.0;net10.0 | ✔ |
| `Koras.Dataverse.OrganizationService` | Optional transport adapter over `Microsoft.PowerPlatform.Dataverse.Client` for orgs requiring `IOrganizationService` semantics | heavy MS deps | net8.0 | v1.1 |

**Dependency direction:** `Koras.Dataverse` → `Abstractions` + `FetchXml`. Nothing depends on
implementation packages. `Abstractions` and `FetchXml` have **zero** third-party dependencies.
DI registration lives in the main package (modern convention; a separate
`.DependencyInjection` package would add friction without value — see ADR-0003).
`Microsoft.Extensions.DependencyInjection` namespace is used for the `AddDataverse` extension.

**TFM strategy (ADR-0002).** net8.0 (LTS floor), net9.0, net10.0 (current LTS). No
netstandard2.0 except `Koras.Dataverse.FetchXml`, where it is genuinely valuable (usable from
Dataverse plugin assemblies on .NET Framework 4.6.2+). Microsoft.Extensions dependencies use a
single 10.0.x version line across TFMs: Azure.Core (required by Azure.Identity) already floors
the transitive graph at 10.0.x, and those packages target net8.0+, so per-TFM downpinning would
only create downgrade conflicts (ADR-0009, amended).

---

## 3. Feature catalog and classification

IDs are stable and used across docs, backlog, tests, and changelog.

| ID | Feature | Release | Notes |
|---|---|---|---|
| KDV-001 | Authentication (client secret, certificate, managed identity, interactive/dev, `DefaultAzureCredential`, custom `TokenCredential`/`IDataverseTokenProvider`) | MVP | Token cache with proactive refresh |
| KDV-002 | CRUD + upsert + alternate keys, late-bound `Entity`, typed POCO mapping (attribute-based) | MVP | Plain CLR values; `@odata.bind` handled automatically |
| KDV-003 | OData query builder + execution + `IAsyncEnumerable` auto-paging | MVP | Injection-safe filter encoding |
| KDV-004 | FetchXML builder + execution + paging-cookie paging | MVP | Builder is a standalone package |
| KDV-005 | Batch (`$batch`), atomic change sets, continue-on-error | MVP | 1000-op guard, per-item results |
| KDV-006 | Metadata: tables, columns, choices (local + global), relationships | MVP | Read-only helpers, typed lightweight models |
| KDV-007 | Solutions: export, import, publish-all, query installed | MVP | Async job polling for import |
| KDV-008 | Resilience: retry w/ Retry-After, throttling awareness, timeout, jittered backoff | MVP | Service-protection limits (429/503/504) |
| KDV-009 | Strong error model: `DataverseError`, categories, transient flag, request id | MVP | Normalizes Web API error payloads |
| KDV-010 | DI + options: `AddDataverse`, named clients, `IDataverseClientFactory`, startup validation | MVP | Options pattern + DataAnnotations validation |
| KDV-011 | Observability: `ILogger` categories, `ActivitySource` "Koras.Dataverse", `Meter` counters/histograms; OTel helper package | MVP | No OTel dependency in core |
| KDV-012 | Health checks (`WhoAmI` probe) | MVP | `AddDataverseHealthCheck()` |
| KDV-013 | Impersonation (`CallerObjectId`) | v1.1 | Per-client + per-request |
| KDV-014 | File & image column upload/download | v1.1 | Chunked streaming |
| KDV-015 | Organization Service transport package | v1.1 | Separate heavy package |
| KDV-016 | Source-generated early-bound models | v1.2 | Roslyn source generator from metadata snapshot |
| KDV-017 | Fluent strongly typed (LINQ-style) queries | v1.2 | Builds on KDV-016 |
| KDV-018 | Metadata snapshot export + environment comparison | v1.2 | CLI-friendly |
| KDV-019 | Solution dependency analysis | v2.0 | |
| KDV-020 | Power Pages Web API helpers | v2.0 | |
| KDV-021 | ALM pipeline helpers | v2.0 | |
| KDV-022 | Plugin development helpers | v2.0 | Depends on FetchXml ns2.0 base |
| KDV-023 | Elastic table / long-running operation helpers | Experimental | |
| — | UI, migration engines, connector runtimes, on-prem <9.x | Out of scope | |

Every MVP feature ships with: unit tests, error-path tests, cancellation tests, docs page,
runnable sample usage, and XML IntelliSense docs. See `docs/features/feature-catalog.md`.

---

## 4. Public API direction (summary)

Full design in `docs/api/public-api-design.md`. Guiding rules: async-first with mandatory
`CancellationToken` parameters (defaulted), interfaces for everything injectable, no statics
except pure builders' entry points, no third-party types in `Abstractions`, plain CLR values,
records for immutable models, `sealed` by default, nullable reference types everywhere.

```csharp
// Registration
services.AddDataverse(o =>
{
    o.EnvironmentUrl = new Uri("https://contoso.crm.dynamics.com");
    o.Authentication.UseClientSecret(tenantId, clientId, secret); // or UseManagedIdentity(), UseDefault(), UseCertificate(), UseInteractive(), UseTokenCredential(cred)
});

// Usage
public sealed class InvoiceService(IDataverseClient dataverse)
{
    public async Task<Guid> CreateAccountAsync(string name, CancellationToken ct)
    {
        var account = new Entity("account") { ["name"] = name, ["revenue"] = 25_000m };
        return await dataverse.CreateAsync(account, ct);
    }

    public IAsyncEnumerable<Entity> ActiveAccounts(CancellationToken ct) =>
        dataverse.QueryAllAsync(
            ODataQuery.For("account").Select("name", "revenue")
                .Where(f => f.Eq("statecode", 0)).OrderBy("name"), ct);
}

// FetchXML (standalone package)
var fetch = FetchXml.For("account")
    .Attributes("name", "revenue")
    .Where(f => f.Eq("statecode", 0).And(a => a.Like("name", "Contoso%")))
    .Link("contact", from: "primarycontactid", to: "contactid",
          l => l.Alias("pc").Attributes("fullname"))
    .OrderBy("name").Top(50)
    .Build();
var page = await dataverse.FetchAsync(fetch, ct);
```

Key types by namespace:

- `Koras.Dataverse` — `IDataverseClient`, `Entity`, `EntityReference`, `ColumnSet`,
  `DataverseQueryResult`, `WhoAmIResponse`, `UpsertResult`, `DataverseClientOptions`,
  `DataverseAuthenticationOptions`, `DataverseRetryOptions`.
- `Koras.Dataverse.Queries` — `ODataQuery`, `ODataFilterBuilder`, `ODataExpand`.
- `Koras.Dataverse.FetchXml` — `FetchXml` (entry), `FetchXmlQuery`, `FetchFilterBuilder`,
  `FetchLinkEntityBuilder`, `FetchConditionOperator`.
- `Koras.Dataverse.Batches` — `BatchRequest`, `BatchOperation`, `BatchResponse`, `BatchItemResult`.
- `Koras.Dataverse.Metadata` — `IMetadataClient`, `TableMetadata`, `ColumnMetadata`,
  `RelationshipMetadata`, `ChoiceOption`.
- `Koras.Dataverse.Solutions` — `ISolutionClient`, `SolutionInfo`, `SolutionImportOptions`.
- `Koras.Dataverse.Errors` — `DataverseException`, `DataverseError`, `DataverseErrorCategory`.
- `Koras.Dataverse.Authentication` — `IDataverseTokenProvider`, credential option helpers.
- `Microsoft.Extensions.DependencyInjection` — `AddDataverse`, `AddDataverseHealthCheck`,
  `IDataverseClientFactory`.

**Compatibility strategy:** Public API tracked by `PublicAPI.Shipped.txt`/`Unshipped.txt`
(Microsoft.CodeAnalysis.PublicApiAnalyzers) + `EnablePackageValidation` once 1.0 ships. Breaking
changes only in majors; `[Obsolete]` one minor before removal; see `docs/api/backward-compatibility.md`.

---

## 5. Architecture (summary)

- **Transport:** `HttpClient` via `IHttpClientFactory` named client `"Koras.Dataverse:{name}"`,
  pipeline: `AuthenticationHandler` → `RetryHandler` → (user handlers) → network. Telemetry is
  emitted by the client layer (`ActivitySource`), not a handler, so activities wrap retries.
- **Auth:** `IDataverseTokenProvider` abstraction; default implementation adapts
  `Azure.Core.TokenCredential` with scope `{environmentUrl}/.default`, caching tokens until 5
  minutes before expiry (thread-safe, single-flight refresh).
- **Error lifecycle:** non-success HTTP → parse OData error payload → `DataverseError`
  (category, Dataverse code, HTTP status, request id, transient flag) → throw
  `DataverseException`. Retries happen below this mapping; only post-retry failures surface.
- **Threading:** all public client types are thread-safe and registered as singletons;
  builders (`ODataQuery`, FetchXML) are mutable-until-`Build`, not thread-safe, documented.
- **Cancellation:** every I/O path takes `CancellationToken`; token combined with per-request
  timeout via linked CTS; `OperationCanceledException` is never swallowed or wrapped.
- **Time:** `TimeProvider` injected for retry delays and token expiry — fully testable.
- **Versioning:** SemVer 2.0; 0.x = preview; 1.0 freezes the public API.

Diagrams live in `docs/architecture/diagrams.md`; decisions in
`docs/architecture/decision-records/` (ADR-0001..ADR-0008).

---

## 6. Testing strategy (summary)

- **Unit tests** (`tests/Koras.Dataverse.UnitTests`): builders, encoding, error mapping, retry
  policy, token caching, batch payload generation/parsing, DI registration, options validation,
  entity conversion. Fake `HttpMessageHandler` for client-level tests. xUnit + NSubstitute +
  `Microsoft.Extensions.TimeProvider.Testing`-style fake time (own tiny fake to avoid extra dep).
- **Architecture tests** (`tests/Koras.Dataverse.ArchitectureTests`): NetArchTest — dependency
  direction, namespace rules, `Abstractions` has no implementation refs, public types sealed or
  abstract, async suffix conventions.
- **Integration tests** (`tests/Koras.Dataverse.IntegrationTests`): run against a real Dataverse
  environment when `KORAS_DATAVERSE_URL` + credentials env vars are present; otherwise skipped.
  CI-safe by default. Cover CRUD round-trip, paging, batch, metadata, WhoAmI.
- **Package/compat tests**: `dotnet pack` output validated (README, icon, symbols, deps, TFMs);
  a consumer project installs the produced `.nupkg` from a local feed and compiles against each
  TFM.
- **Coverage target:** ≥ 80 % line / ≥ 70 % branch on `Koras.Dataverse` + `FetchXml`, enforced
  as a ratchet not a gate-only number; meaningful behavior coverage prioritized.
- Assertions: xUnit built-ins (FluentAssertions ≥ v8 licensing makes it unsuitable for an MIT
  library — documented in the test strategy).

---

## 7. Security (summary)

Threat model in `docs/security/threat-model.md`. Highlights: secrets only via options/user
secret stores (never connection strings in code); certificate and managed identity preferred
over client secrets; tokens never logged; FetchXML/OData builders encode all values (XML
escaping, OData literal encoding) to block injection; HTTPS enforced (non-HTTPS environment URL
rejected); no deserialization of polymorphic payloads; bounded retries; CodeQL + dependency
review + Dependabot + `dotnet list package --vulnerable` in CI; SBOM generation on release;
NuGet author signing planned at 1.0; SECURITY.md defines private vulnerability reporting.

---

## 8. Delivery plan

**Milestones**

| # | Milestone | Contents |
|---|---|---|
| 0 | Repository foundation | Build props, CPM, analyzers, editorconfig, CI, community files, CLAUDE.md |
| 1 | Abstractions & core models | Entity model, error model, options, interfaces |
| 2 | Core implementation | Auth, HTTP pipeline, CRUD, queries, paging, batch |
| 3 | DI & configuration | AddDataverse, named clients, factory, validation |
| 4 | Feature packages | FetchXml package, metadata client, solution client |
| 5 | ASP.NET Core integration | Health checks, minimal API sample wiring |
| 6 | Observability | Activities, metrics, logging, OTel package |
| 7 | Samples & documentation | Console/MinimalApi/Worker samples + full docs tree |
| 8 | Hardening | Security review, benchmarks, analyzer tightening |
| 9 | Packaging & release | pack validation, publish workflow, 0.1.0-preview.1 |

**Release plan:** `0.1.0-preview.1` (MVP feature-complete, API feedback window) →
`0.1.0` (stabilized MVP) → `0.5.0` (impersonation, file columns, OrganizationService package) →
`1.0.0` (API freeze, package signing, LTS policy: latest major + previous major security fixes,
.NET LTS alignment).

**Top risks**

| Risk | Impact | Mitigation |
|---|---|---|
| Microsoft ships a modern first-party Web API SDK | Adoption | Differentiate on DX/observability; adapters over official transport |
| Web API contract drift across versions | Correctness | Pin `v9.2`, integration test matrix, error-tolerant parsing |
| Auth complexity (sovereign clouds, ADFS) | Support load | Standard `TokenCredential` escape hatch; document clouds via `EnvironmentUrl` |
| Throttling semantics change | Reliability | Central retry policy, options to tune, Retry-After always honored |
| API frozen too early | Maintenance | 0.x preview window + PublicAPI tracking before 1.0 |
| Scope creep (ALM, generators) | Focus | Feature gates by release train; out-of-scope list enforced in reviews |

**First five implementation tasks:** (1) repo foundation (props/CPM/analyzers/solution);
(2) `Abstractions`: Entity/EntityReference/errors/options/interfaces; (3) `FetchXml` builder +
tests; (4) core HTTP pipeline (auth handler, retry handler, error mapping) + tests;
(5) CRUD + OData query execution + paging + tests.
