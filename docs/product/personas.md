# Personas — Koras Dataverse SDK

> Five representative users of the SDK. Feature IDs reference
> [`../features/feature-catalog.md`](../features/feature-catalog.md).

## 1. Priya — Power Platform pro-developer

**Background.** Senior developer at a mid-size company standardizing on Power Platform. Writes
C# daily: plugins, Azure Functions that react to Dataverse events, and custom APIs. Fluent in
the Dataverse data model, less patient with its client libraries.

**Goals.** Ship integrations quickly; keep function apps small and fast to cold-start; write
unit tests without a live environment.

**Pains.** ServiceClient's dependency weight and sync-over-async behavior in Functions;
hand-rolled `HttpClient` code duplicated across function apps; FetchXML built by string
concatenation; no clean way to mock Dataverse access.

**How the SDK helps.** `AddDataverse` + `IDataverseClient` give her a singleton, thread-safe,
mockable client (KDV-010); managed identity auth removes secrets from function apps (KDV-001);
fluent OData and FetchXML builders replace string concatenation (KDV-003, KDV-004); the
standalone `Koras.Dataverse.FetchXml` package (netstandard2.0) can later serve her plugin
assemblies.

**Success criteria.** First working query in under five minutes; her service classes unit-test
against a substituted `IDataverseClient`; no throttling incidents during bulk operations.

## 2. Marcus — Dynamics 365 enterprise developer

**Background.** Ten years on the XRM stack at a large enterprise running Dynamics 365 Sales and
Customer Service. Deeply familiar with `IOrganizationService`, early-bound entities, and plugin
pipelines. Now asked to build modern .NET 8 services alongside the legacy estate.

**Goals.** Reuse his Dataverse knowledge in modern services; satisfy enterprise requirements
(observability, security review, support traceability); avoid betting on unmaintained community
wrappers.

**Pains.** The official client feels like .NET Framework code in a .NET 8 world; enterprise
observability standards (OpenTelemetry) are hard to bolt on; error diagnostics lack the request
IDs his support process needs.

**How the SDK helps.** Plain CLR values and a familiar late-bound `Entity` keep the learning
curve shallow (KDV-002); `DataverseError` carries category, Dataverse code, HTTP status, and
request ID for support cases (KDV-009); ActivitySource/Meter telemetry plugs into the corporate
OTel pipeline via `Koras.Dataverse.OpenTelemetry` (KDV-011); the planned
`Koras.Dataverse.OrganizationService` adapter (KDV-015, v1.1) covers orgs that still mandate
`IOrganizationService` semantics.

**Success criteria.** Passes internal architecture and security review; production incidents
are diagnosable from traces and request IDs; no regression in his team's delivery pace.

## 3. Elena — Integration architect

**Background.** Designs integration landscapes across a portfolio of systems — ERP, commerce,
data platform, CRM. Sets standards for how dozens of teams build services; reviews rather than
writes most of the code.

**Goals.** One consistent, supportable pattern for Dataverse access across all teams;
predictable behavior under load; vendor-risk clarity before endorsing a dependency.

**Pains.** Every team has a slightly different Dataverse wrapper with different retry and error
semantics; throttling behavior is rediscovered per project; no consistent telemetry across the
integration estate.

**How the SDK helps.** Options-pattern configuration with startup validation and named clients
per environment gives her an enforceable standard (KDV-010); service-protection-aware
resilience is on by default and centrally tunable (KDV-008); a uniform error taxonomy (KDV-009)
and uniform telemetry (KDV-011) make cross-team dashboards and runbooks possible; health checks
integrate with the standard probing infrastructure (KDV-012); MIT license, semver discipline,
and a public API contract address governance concerns.

**Success criteria.** She can write a one-page internal standard ("use Koras.Dataverse,
configure it like this") and retire per-team wrappers; load-test behavior under throttling is
consistent and documented.

## 4. Tomás — Microsoft-stack consultant

**Background.** Independent consultant delivering Power Platform and Dynamics 365 projects for
multiple clients per year: migrations, integrations, ALM setup, rescue engagements.

**Goals.** Reusable, defensible technical foundations he can bring to every engagement; fast
delivery without leaving clients an unmaintainable snowflake; tooling for solution deployment
and data loads that works in any client's pipeline.

**Pains.** Rebuilding the same plumbing at each client; justifying custom wrappers to client
architects; migrations that trip service protection limits; solution import scripts that poll
badly or not at all.

**How the SDK helps.** A public, documented, MIT-licensed SDK is easier to hand over than
bespoke code; batch operations with per-item results and the 1000-op guard are built for his
data loads (KDV-005); `ISolutionClient` export/import with async job polling scripts his
deployments (KDV-007); alternate-key upsert makes his loads re-runnable (KDV-002); interactive
and `DefaultAzureCredential` auth flows fit his ad hoc admin tooling (KDV-001).

**Success criteria.** He delivers the integration layer in days, not weeks; the client's own
team can maintain it after handover; the same recipes work across engagements.

## 5. Aisha — Power Pages developer

**Background.** Builds customer-facing portals on Power Pages. Comfortable with Liquid, web
templates, and JavaScript; increasingly building companion .NET APIs for logic the portal
cannot run safely client-side.

**Goals.** A safe server-side path to Dataverse for privileged operations; simple hosting on
App Service with managed identity; clear mapping from Dataverse errors to user-facing responses.

**Pains.** Portal-side Web API limits push logic server-side, where she has to learn raw
Dataverse Web API mechanics; auth setup is the hardest part of every companion API;
user-supplied filter values risk query injection.

**How the SDK helps.** Managed identity via `UseManagedIdentity()` removes secret management
(KDV-001); injection-safe query builders encode user input by design (KDV-003, KDV-004); the
error model's categories map cleanly to HTTP responses (KDV-009); health checks and DI fit App
Service hosting conventions (KDV-010, KDV-012). Future KDV-020 (Power Pages Web API helpers,
v2.0) targets her portal-specific scenarios directly.

**Success criteria.** Companion API stood up in a day; security review finds no injection or
secret-handling issues; portal users get meaningful error messages instead of raw payloads.
