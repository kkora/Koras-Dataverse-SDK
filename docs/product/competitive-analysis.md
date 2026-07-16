# Competitive Analysis — Koras Dataverse SDK

> Expands the competitive landscape table in §1 of
> [`docs/planning/master-plan.md`](../planning/master-plan.md). This analysis is honest by
> design: the alternatives are listed with real strengths, and with the cases where they remain
> the better choice.

## Summary table

| Alternative | Type | Strengths | Weaknesses | Gap the Koras SDK fills |
|---|---|---|---|---|
| `Microsoft.PowerPlatform.Dataverse.Client` | Official SDK | First-party support, full operation coverage, huge install base | Legacy `IOrganizationService` model, heavy deps, weak DI/telemetry story, lock-based sync paths | Modern DI-native, observable, async-first client |
| Raw `HttpClient` + Web API | DIY | Full control, zero dependencies, always current with the API | No error taxonomy, no throttling handling, plumbing rebuilt per project | The plumbing, done once, tested |
| `Microsoft.Xrm.Sdk` | Official SDK (plugins/on-prem) | The only option inside plugins; canonical types | Not applicable to modern service integration; .NET Framework heritage | Modern service-side story (plus a ns2.0 FetchXML builder plugins can use) |
| pac CLI / XrmToolBox | Tools | Excellent for interactive and pipeline tasks | Tools, not embeddable SDKs | An SDK your code can call |
| Community wrappers (e.g., PowerPlatform.Api, Dataverse REST clients) | OSS libraries | Prove the demand; sometimes good ideas | Partial coverage, low maintenance, no observability or resilience contracts | Coverage + maintenance discipline + contracts |

## Microsoft.PowerPlatform.Dataverse.Client (ServiceClient)

**What it is.** The official .NET client for Dataverse, wrapping the Web API and Organization
Service behind the `IOrganizationService` interface family.

**Strengths.**
- First-party: Microsoft support, documentation, and long-term backing.
- Complete operation coverage, including messages the Web API exposes awkwardly.
- Familiar to every Dynamics developer; enormous amount of existing sample code.
- Battle-tested at scale for years.

**Weaknesses (per master plan §1).**
- Legacy `IOrganizationService` programming model: `OptionSetValue`/`Money` wrapper types,
  request/response message classes, `EntityCollection` containers.
- Heavyweight dependency graph for services that only need CRUD and queries.
- Connection-string-oriented authentication rather than `TokenCredential` composition.
- Limited async ergonomics and lock-based sync paths.
- Weak dependency-injection and telemetry story; no first-class `ActivitySource`/`Meter`
  instrumentation.

**When to still use it.**
- You need operations or messages the Koras SDK does not cover, especially in the MVP window.
- Organizational policy requires first-party Microsoft libraries.
- A large legacy codebase is already built on `IOrganizationService` and rewriting has no
  business case. (The planned `Koras.Dataverse.OrganizationService` adapter, KDV-015 / v1.1,
  targets mixed estates.)

## Raw HttpClient + Dataverse Web API

**What it is.** Calling `https://{env}/api/data/v9.2/...` directly with `HttpClient` and your
own token acquisition.

**Strengths.**
- Total control over requests, headers, and payloads.
- No third-party dependencies; nothing between you and the API.
- Always able to use the newest Web API capability the day it ships.

**Weaknesses.**
- Every project rebuilds auth, token caching, retries, throttling, paging, `$batch` MIME
  handling, and error parsing — the exact repetitive plumbing described in
  [`problem-statement.md`](problem-statement.md).
- No error taxonomy; consumers parse OData error payloads ad hoc.
- Service protection limits are usually handled late, after production incidents.
- The resulting layer is rarely mockable or well-tested.

**When to still use it.**
- A single tiny call in a context where any dependency is unwanted.
- Endpoints or preview features outside the SDK's surface (the Koras SDK does not try to wrap
  every endpoint). Note that `IDataverseClient` usage and raw calls can coexist in one codebase.

## Microsoft.Xrm.Sdk

**What it is.** The classic XRM assembly: the type system and `IOrganizationService` contract
used inside plugins, workflow activities, and on-premises deployments.

**Strengths.**
- The only supported programming surface inside the plugin sandbox.
- Canonical types understood by every Dynamics tool and developer.

**Weaknesses.**
- Not applicable to modern service integration; .NET Framework heritage (master plan §1).
- Ties consuming code to WCF-era patterns and types.

**When to still use it.**
- Always, inside plugins and workflow activities — that is its home, and the Koras SDK does not
  compete there. The Koras contribution to that world is deliberately narrow:
  `Koras.Dataverse.FetchXml` targets netstandard2.0 so plugin assemblies (.NET Framework
  4.6.2+) can build FetchXML fluently (master plan §2), and KDV-022 (plugin development
  helpers) is a v2.0 consideration.

## pac CLI and XrmToolBox

**What they are.** Tools: the Power Platform CLI for scripting environment/solution operations,
and XrmToolBox as the community's interactive Swiss-army knife.

**Strengths.**
- Excellent for interactive administration, exploration, and pipeline steps.
- pac CLI is Microsoft's strategic ALM tooling; XrmToolBox has a deep plugin ecosystem.

**Weaknesses (as alternatives to an SDK).**
- They are tools, not embeddable SDKs — you cannot inject them into an application, mock them
  in tests, or get typed results in-process.
- Pipeline use means shelling out: exit-code error handling, no structured errors, no shared
  telemetry.

**When to still use them.**
- Interactive administration and exploration (XrmToolBox is unmatched here).
- ALM pipelines where a CLI step is the natural shape — replacing pac CLI is an explicit
  non-goal (master plan §1). The SDK's `ISolutionClient` (KDV-007) serves the cases where
  deployment logic must live *inside* a .NET application or custom pipeline agent.

## Community wrappers

**What they are.** OSS libraries wrapping the Dataverse Web API (e.g., PowerPlatform.Api and
various Dataverse REST client projects).

**Strengths.**
- They demonstrate real demand for exactly this product category.
- Some contain genuinely good API ideas and lightweight designs.

**Weaknesses.**
- Partial coverage: typically CRUD plus simple queries, without batch, metadata, or solutions.
- Low maintenance activity and bus-factor risk; slow response to Web API changes.
- No observability or resilience contracts — the two hardest parts to retrofit.
- Rarely any public API stability discipline.

**When to still use them.**
- If one already covers your exact narrow need and is maintained, it may be the smaller
  dependency. Evaluate maintenance signals honestly — the same test the Koras SDK must pass.

## Differentiators (from master plan §1)

1. **Injection-safe fluent FetchXML + OData builders** with strict encoding (XML escaping,
   OData literal encoding) — string-concatenation injection is designed out.
2. **Service-protection-limit-aware resilience by default** — Retry-After honored, jittered
   backoff, transient classification, no opt-in required.
3. **Plain CLR value model** — no `OptionSetValue`/`Money` wrappers.
4. **First-class OpenTelemetry** — `ActivitySource`/`Meter` in the core, OTel wiring in a
   dedicated package with no OTel dependency in the core.
5. **Modular packages** — dependency-free `Abstractions` and `FetchXml` (the latter usable
   inside plugins later).
6. **Documented, tested, semver-disciplined public API** — PublicAPI tracking, package
   validation, breaking changes only in majors.

## Honest limitations of the Koras SDK

- It is new: no install base, no track record, and pre-1.0 the API may change (that is what the
  preview window is for).
- It intentionally covers less than ServiceClient (no full message catalog, no WCF transport in
  core) — see the out-of-scope list in the master plan §1.
- It is maintained by a small organization; mitigations (public roadmap, tests as contract,
  MIT license so users are never trapped) are described in
  [`product-vision.md`](product-vision.md).
