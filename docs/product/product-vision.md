# Product Vision — Koras Dataverse SDK

> Elaborates on §1 of [`docs/planning/master-plan.md`](../planning/master-plan.md). The master
> plan is the source of truth; this document explains the reasoning behind it.

- **Package:** `Koras.Dataverse` (NuGet) · **Organization:** Koras Technologies
- **License:** MIT · **Status:** Pre-release planning (0.1.0-preview)

## Vision

Give every .NET developer working against Microsoft Dataverse a modern, fluent, resilient SDK
that removes the repetitive plumbing — authentication, retries, throttling, paging, batching,
query generation, metadata access, and error handling — behind a small, testable, DI-native API
surface.

The one-line positioning: **the HttpClientFactory-era Dataverse SDK.** Web API first,
`TokenCredential` (Azure.Identity) authentication, `IAsyncEnumerable` paging, fluent and
injection-safe query builders, a strong error taxonomy, `ActivitySource`/`Meter` telemetry,
options-pattern configuration, and health checks — all mockable behind interfaces.

## Why this package should exist

Every team that integrates .NET services with Dataverse today faces the same fork:

1. **Use `Microsoft.PowerPlatform.Dataverse.Client` (ServiceClient).** It works, but it carries
   the legacy `IOrganizationService` programming model, heavyweight dependencies,
   connection-string-oriented authentication, limited async ergonomics, and no first-class
   observability. It predates the patterns that modern ASP.NET Core services are built on
   (`IHttpClientFactory`, the options pattern, OpenTelemetry, nullable reference types).
2. **Call the Dataverse Web API directly with `HttpClient`.** This gives full control, but every
   project rebuilds the same HTTP, auth, retry, paging, batch, and error-parsing code — usually
   incompletely, and usually without handling service protection limits correctly.

Both paths produce inconsistent, hard-to-test integration layers, over and over, in every
project. That repeated plumbing is the gap the Koras Dataverse SDK fills (see
[`problem-statement.md`](problem-statement.md)).

## What would make developers trust it

Trust in an infrastructure library is earned through discipline, not marketing:

- **Semver discipline and API stability.** Public API tracked with
  `PublicAPI.Shipped.txt`/`Unshipped.txt` and package validation; breaking changes only in major
  versions; `[Obsolete]` one minor before removal (master plan §4).
- **Tests as a contract.** Every MVP feature ships with unit tests, error-path tests, and
  cancellation tests; integration tests run against a real Dataverse environment; coverage
  targets of ≥ 80 % line / ≥ 70 % branch on the core packages (master plan §6).
- **Security posture.** No secrets in code or connection strings, tokens never logged,
  injection-safe query builders, CodeQL and dependency scanning in CI, SBOM on release, a
  published SECURITY.md, and NuGet author signing planned at 1.0 (master plan §7).
- **Honest scope.** A published out-of-scope list (no UI, no migration engines, no plugin
  execution runtime, no pac CLI replacement) so users know the project will not sprawl.
- **Mockability.** Everything injectable is an interface (`IDataverseClient`,
  `IMetadataClient`, `ISolutionClient`, `IDataverseTokenProvider`), so consumers can unit test
  their own code without a live environment.
- **Documentation that answers real questions.** Throttling, paging, batch limits, and metadata
  access are the questions developers actually search for; the docs are structured around them.

## What could prevent adoption

We plan for these risks openly (see also master plan §8):

- **"Why not just use the Microsoft client?"** The official package has default status. The
  answer must be demonstrable in a five-minute quick start and a side-by-side comparison guide,
  not asserted.
- **Microsoft ships a modern first-party Web API SDK.** Mitigation: differentiate on developer
  experience and observability, and remain willing to adapt over official transports (the
  planned `Koras.Dataverse.OrganizationService` package already establishes the adapter
  pattern).
- **Single-vendor maintenance risk.** A new OSS project from a small organization must prove
  responsiveness: triage SLAs, a public roadmap, and a preview feedback window before the 1.0
  API freeze.
- **Web API contract drift and throttling semantics changes.** Mitigation: pin `v9.2`,
  error-tolerant parsing, integration test matrix, tunable central retry policy that always
  honors `Retry-After`.
- **Trust cliff for auth.** Authentication libraries fail in enterprise edge cases (sovereign
  clouds, ADFS). Mitigation: standard `TokenCredential` escape hatch and explicit documentation
  of cloud support via `EnvironmentUrl`.

## Positioning

- **Web API first.** The core package speaks the Dataverse Web API (`v9.2`) only. The WCF-era
  Organization Service is available later as an optional, clearly separated transport package
  (`Koras.Dataverse.OrganizationService`, v1.1) for organizations that require
  `IOrganizationService` semantics.
- **Differentiators** (master plan §1): injection-safe fluent FetchXML and OData builders with
  strict encoding; service-protection-limit-aware resilience by default; a plain CLR value model
  (no `OptionSetValue`/`Money` wrappers); first-class OpenTelemetry; modular packages including
  a dependency-free FetchXML builder usable inside plugins later; and a documented, tested,
  semver-disciplined public API.
- **Not positioned as** a tool (XrmToolBox, pac CLI), a low-code accelerator, or a data
  migration product. It is an embeddable SDK for professional .NET developers.

## Adoption strategy

1. **Five-minute quick start.** `dotnet add package Koras.Dataverse`, one `AddDataverse` call,
   one `IDataverseClient` injection, first query running.
2. **Copy-paste recipes** for the pain points developers search for: throttling and 429
   handling, paging large result sets, `$batch` with change sets, metadata reads, alternate-key
   upserts.
3. **Runnable samples** for the three dominant hosting models: console, ASP.NET Core minimal
   API, and worker service.
4. **Comparison guide vs ServiceClient** — honest, task-by-task, including cases where the
   official client remains the right choice (see
   [`competitive-analysis.md`](competitive-analysis.md)).
5. **Preview feedback window.** `0.1.0-preview.1` ships MVP-complete specifically to gather API
   feedback before the surface stabilizes at `0.1.0` and freezes at `1.0.0`.

## Open-source strategy

- **MIT license**, developed in the open at
  `https://github.com/korastechnologies/koras-dataverse`.
- Published to **NuGet.org** with `Koras.*` prefix reservation as part of the release checklist.
- Public roadmap driven by the stable KDV feature IDs (see
  [`product-roadmap.md`](product-roadmap.md) and
  [`../features/feature-catalog.md`](../features/feature-catalog.md)).
- Architecture decisions recorded as ADRs under `docs/architecture/decision-records/`, so
  contributors can see *why*, not only *what*.
- Community health files (CONTRIBUTING, CODE_OF_CONDUCT, SECURITY, issue/PR templates) are part
  of Milestone 0, not an afterthought.

## Community contribution strategy

- **Low-friction first contributions.** Good-first-issue labeling, a documented local build
  (`dotnet build` + `dotnet test` with no external environment required — integration tests
  skip themselves without credentials), and architecture tests that make the rules enforceable
  rather than tribal.
- **Contribution boundaries.** The out-of-scope list is enforced in review; feature proposals
  map to the release-train classification (MVP / v1.1 / v1.2 / v2.0 / Experimental) before code
  is written.
- **API changes gated.** Public API analyzers make any surface change explicit in the diff, so
  contributors and maintainers negotiate API shape deliberately.
- **Recognition and stewardship.** Changelog credits, and a path from repeated contributor to
  triager to maintainer as the project matures.

## Monetization stance

**None planned.** The SDK is free, MIT-licensed, and will not have paid tiers, paid support
SKUs, or open-core feature gating. Its commercial value to Koras Technologies is indirect: it
builds consulting credibility by demonstrating, in public, the engineering standards the company
applies to client work. This alignment is deliberate — it removes any incentive to hold features
back from the open-source package.

## Naming assessment

- **NuGet ID / root namespace:** `Koras.Dataverse`. Short, unambiguous, and verified (2026-07)
  not to collide with any existing NuGet ID.
- **Trademark posture:** `Dataverse` alone is Microsoft's product name; the `Koras.` prefix
  avoids trademark ambiguity while keeping the package discoverable by the term developers
  actually search for.
- **Package family:** `Koras.Dataverse.Abstractions`, `Koras.Dataverse.FetchXml`,
  `Koras.Dataverse`, `Koras.Dataverse.OpenTelemetry`, and later
  `Koras.Dataverse.OrganizationService` — names state their contents and follow .NET ecosystem
  conventions (an `.Abstractions` split, an OpenTelemetry companion package).
- **Namespace design:** feature areas live in sub-namespaces (`Koras.Dataverse.Queries`,
  `.FetchXml`, `.Batches`, `.Metadata`, `.Solutions`, `.Errors`, `.Authentication`), with DI
  extensions in `Microsoft.Extensions.DependencyInjection` per ecosystem convention (master
  plan §4).
- **Prefix reservation** for `Koras.*` on NuGet.org is on the release checklist to protect
  consumers from squatting.
