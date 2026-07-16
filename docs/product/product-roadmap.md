# Product Roadmap — Koras Dataverse SDK

> Release trains per §8 of [`docs/planning/master-plan.md`](../planning/master-plan.md),
> mapped to the stable KDV feature IDs from §3. Detailed feature-level planning lives in
> [`../features/feature-catalog.md`](../features/feature-catalog.md),
> [`../features/mvp-scope.md`](../features/mvp-scope.md), and
> [`../features/future-roadmap.md`](../features/future-roadmap.md).

Versioning follows SemVer 2.0: 0.x versions are previews; 1.0.0 freezes the public API. No
dates are committed in this document; trains ship when their quality gates pass.

## Train overview

| Train | Purpose | Feature IDs |
|---|---|---|
| `0.1.0-preview.1` | MVP feature-complete; API feedback window opens | KDV-001 … KDV-012 (preview) |
| `0.1.0` | Stabilized MVP | KDV-001 … KDV-012 |
| `0.5.0` | Post-MVP capabilities land pre-freeze | KDV-013, KDV-014, KDV-015 |
| `1.0.0` | API freeze, signing, LTS policy | Hardening only; no new features |
| `1.1` | First post-1.0 minor | KDV-013, KDV-014, KDV-015 declared stable |
| `1.2` | Typed developer experience | KDV-016, KDV-017, KDV-018 |
| `2.0` | Platform breadth (next major) | KDV-019, KDV-020, KDV-021, KDV-022 |
| Experimental | Ships when proven, behind explicit preview labeling | KDV-023 |

## 0.1.0-preview.1 — MVP preview

**Goal.** Ship the complete MVP feature set to NuGet.org as a preview and open a deliberate API
feedback window before anything stabilizes.

**Contents.** All twelve MVP features: KDV-001 (authentication), KDV-002 (CRUD/upsert/alternate
keys), KDV-003 (OData queries + auto-paging), KDV-004 (FetchXML builder + execution), KDV-005
(batch), KDV-006 (metadata read), KDV-007 (solutions), KDV-008 (resilience), KDV-009 (error
model), KDV-010 (DI + options), KDV-011 (observability), KDV-012 (health checks).

**Packages.** `Koras.Dataverse.Abstractions`, `Koras.Dataverse.FetchXml`, `Koras.Dataverse`,
`Koras.Dataverse.OpenTelemetry`.

**Exit criteria.** Every MVP feature carries unit, error-path, and cancellation tests; docs
page and runnable sample per feature; integration test pass against a real environment;
packaging validation green (master plan §3, §6, §8 milestones 0–9).

## 0.1.0 — Stabilized MVP

**Goal.** Resolve preview feedback and stabilize the MVP surface.

**Contents.** Same feature set (KDV-001 … KDV-012). API adjustments arising from the feedback
window are made here — this is the intended place for breaking changes, while 0.x semantics
still permit them.

**Exit criteria.** No known API-shape issues held open; public API tracked via
`PublicAPI.Shipped.txt`; comparison guide vs ServiceClient and quick-start docs complete.

## 0.5.0 — Post-MVP capabilities

**Goal.** Land the next capability wave while the API can still move, so 1.0.0 freezes a
surface that has already absorbed them.

**Contents (per master plan §8):**
- KDV-013 — Impersonation (`CallerObjectId`), per-client and per-request.
- KDV-014 — File and image column upload/download with chunked streaming.
- KDV-015 — `Koras.Dataverse.OrganizationService` transport adapter package (net8.0).

These three carry the **v1.1** release classification in the feature catalog: they are
introduced pre-1.0 in this train and are declared stable in the 1.1 train.

**Exit criteria.** Same per-feature quality bar as MVP features; the OrganizationService
package proven against a real environment; no dependency leakage from the heavy adapter package
into the core.

## 1.0.0 — API freeze

**Goal.** Freeze the public API and commit to compatibility.

**Contents.** No new features. Hardening: security review, benchmarks, analyzer tightening
(milestone 8); `EnablePackageValidation` on; NuGet author signing; LTS policy in effect (latest
major plus previous major for security fixes; .NET LTS alignment).

**Exit criteria.** PublicAPI files shipped and locked; breaking changes henceforth only in
majors; `[Obsolete]` one minor before removal (master plan §4).

## 1.1 — First post-1.0 minor

**Goal.** Declare the 0.5.0 capability wave stable under the frozen-API regime.

**Contents.** KDV-013, KDV-014, KDV-015 stable, with any fixes accumulated since 0.5.0.
Additive-only API changes.

## 1.2 — Typed developer experience

**Goal.** Strongly typed development on top of the late-bound foundation.

**Contents.**
- KDV-016 — Source-generated early-bound models (Roslyn source generator from a metadata
  snapshot).
- KDV-017 — Fluent strongly typed (LINQ-style) queries, building on KDV-016.
- KDV-018 — Metadata snapshot export + environment comparison (CLI-friendly).

**Ordering rationale.** KDV-017 depends on KDV-016; KDV-018 shares the metadata snapshot
format with KDV-016. See [`../features/future-roadmap.md`](../features/future-roadmap.md).

## 2.0 — Platform breadth

**Goal.** Expand from data access into ALM and adjacent developer scenarios; a major version
allows any accumulated breaking changes.

**Contents.**
- KDV-019 — Solution dependency analysis.
- KDV-020 — Power Pages Web API helpers.
- KDV-021 — ALM pipeline helpers.
- KDV-022 — Plugin development helpers (builds on the netstandard2.0 `Koras.Dataverse.FetchXml`
  base).

## Experimental

- KDV-023 — Elastic table / long-running operation helpers. Ships only when the underlying
  platform behavior is stable enough to contract against; clearly labeled experimental and
  exempt from compatibility guarantees until promoted.

## Out of scope (all trains)

UI components, data migration engines, plugin execution runtime, XRM tooling replacements
(pac CLI), Power Automate connectors, on-premises pre-9.x support, and WCF Organization Service
in the core package (master plan §1, §3).
