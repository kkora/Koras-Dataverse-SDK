# Problem Statement — Koras Dataverse SDK

> Elaborates on §1 ("Problem") of [`docs/planning/master-plan.md`](../planning/master-plan.md).

## The repetitive-plumbing problem

Every .NET team that integrates with Microsoft Dataverse over the Web API ends up writing the
same infrastructure code, project after project:

- **Authentication.** Acquiring Entra ID tokens for the `{environmentUrl}/.default` scope,
  caching them, refreshing them before expiry, and doing so thread-safely. Most hand-rolled
  implementations get at least one of these wrong (no proactive refresh, refresh stampedes, or
  tokens accidentally written to logs).
- **Service protection limits.** Dataverse throttles with 429/503/504 responses and a
  `Retry-After` header. Correct handling requires honoring `Retry-After`, applying jittered
  backoff, bounding retries, and distinguishing transient from permanent failures. Teams
  routinely discover this in production, during their first bulk load.
- **Paging.** OData `@odata.nextLink` continuation and FetchXML paging cookies are two different
  mechanisms, both easy to implement incorrectly (dropped pages, unbounded memory, re-encoded
  cookies).
- **Batching.** The `$batch` endpoint uses multipart MIME with change sets, content-ID
  referencing, a 1000-operation ceiling, and per-item status parsing. Almost no hand-rolled
  client implements continue-on-error and per-item results correctly.
- **Query generation.** String-concatenated OData filters and FetchXML are both injection-prone
  and fragile around escaping, date formats, and option-set values.
- **Error handling.** The Web API returns OData error payloads with Dataverse-specific error
  codes. Without a taxonomy, every consumer writes ad hoc `catch` blocks keyed on status codes
  or message text.
- **Testability.** Code written directly against `HttpClient` or against the official
  ServiceClient is difficult to mock, so the integration layer is often the least-tested part
  of the system.

The official `Microsoft.PowerPlatform.Dataverse.Client` removes some of this but brings its own
costs: the legacy `IOrganizationService` programming model, heavyweight dependencies,
connection-string-oriented auth, limited async ergonomics, lock-based sync paths, and no
first-class observability (master plan §1). Neither path yields the small, injectable, observable
client that a modern ASP.NET Core or worker-service codebase expects.

## Cost to teams

- **Repeated build cost.** Days to weeks per project spent rebuilding auth/retry/paging/batch
  plumbing that has nothing to do with business value — and each rebuild is a fresh chance to
  introduce the same defects.
- **Production incidents.** Unhandled throttling and missing retry logic surface as outages
  during data-heavy operations; missing request-ID capture makes Microsoft support cases slower
  to resolve.
- **Inconsistency across a portfolio.** Each team's wrapper has different semantics for errors,
  retries, and paging, which raises onboarding cost and makes shared tooling impossible.
- **Poor observability.** Without standardized logging, traces, and metrics, Dataverse calls
  are a blind spot in otherwise well-instrumented systems.
- **Weak test coverage.** Hard-to-mock integration layers either go untested or force teams to
  run tests against shared live environments.
- **Security drift.** Connection strings with client secrets in configuration files, tokens in
  log output, and string-built queries vulnerable to injection are recurring findings in code
  reviews of hand-rolled Dataverse clients.

## What should NOT be included

Scope discipline is part of the product. Per the master plan (§1, non-goals), the SDK
deliberately excludes:

- **UI components** — no controls, no admin front ends.
- **Data migration engines** — the SDK provides the primitives (CRUD, batch, paging,
  resilience) that a migration job needs; it does not schedule, map, or orchestrate migrations.
- **Plugin *execution* runtime** — no sandbox emulation or plugin pipeline simulation. (A
  dependency-free FetchXML builder that plugin assemblies can reference is in scope; plugin
  development *helpers* are a v2.0 consideration, KDV-022.)
- **XRM tooling replacements** — pac CLI and XrmToolBox are tools; this is an embeddable SDK.
- **Power Automate connectors** — different runtime, different audience.
- **On-premises Dataverse before 9.x** — the SDK targets the modern Web API (`v9.2`).
- **WCF Organization Service in the core package** — offered only as the optional
  `Koras.Dataverse.OrganizationService` adapter package (v1.1) for organizations that need it.

## What would create unnecessary complexity

Design choices we reject because they add weight without solving the core problem:

- **An abstraction over multiple transports in the core.** Making every call path polymorphic
  over Web API and Organization Service would compromise both. The core is Web API only; the
  Organization Service adapter is a separate, clearly bounded package.
- **Wrapper value types** (`OptionSetValue`, `Money`, `EntityCollection`-style containers).
  Plain CLR values keep consumer code simple and serializer-friendly (master plan §1,
  differentiator 3).
- **Mandatory early-bound code generation.** Generated models are useful (planned as KDV-016,
  v1.2) but must never be required; the late-bound `Entity` model is the foundation.
- **A separate `.DependencyInjection` package.** DI registration lives in the main package —
  the modern convention; a split package adds friction without value (ADR-0003, master plan §2).
- **A hard OpenTelemetry dependency in the core.** The core emits via `ActivitySource`/`Meter`
  only; OTel wiring lives in the optional `Koras.Dataverse.OpenTelemetry` package (KDV-011).
- **Configuration sprawl.** Options are validated at startup via the options pattern and
  DataAnnotations (KDV-010); the SDK favors correct defaults (retry policy, timeouts, paging)
  over dozens of knobs.
- **LINQ providers ahead of their time.** A full LINQ-to-Dataverse provider invites subtle
  translation bugs; the MVP ships explicit, injection-safe builders instead, and strongly typed
  fluent queries arrive only with source-generated models (KDV-017, v1.2).
