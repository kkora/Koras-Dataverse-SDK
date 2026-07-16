# Future Roadmap — Post-MVP Features

> Covers the v1.1, v1.2, v2.0, and Experimental release classifications from §3 of
> [`docs/planning/master-plan.md`](../planning/master-plan.md). Release-train mechanics
> (version numbers, exit criteria) live in
> [`../product/product-roadmap.md`](../product/product-roadmap.md); per-feature entries in
> [`feature-catalog.md`](feature-catalog.md). Everything here is planned, not implemented.

## v1.1 — Enterprise completeness

Introduced in the `0.5.0` train (pre-freeze, per master plan §8) and declared stable in 1.1.

| ID | Feature | Rationale |
|---|---|---|
| KDV-013 | Impersonation (`CallerObjectId`), per-client + per-request | Most-requested enterprise integration capability; small additive surface, deliberately kept out of the MVP feedback window |
| KDV-014 | File & image column upload/download (chunked streaming) | Common real-world need; a self-contained sub-protocol that must not delay MVP |
| KDV-015 | Organization Service transport package (`Koras.Dataverse.OrganizationService`) | Adoption bridge for organizations mandating `IOrganizationService` semantics; heavy Microsoft dependencies stay isolated in their own net8.0 package |

**Dependency ordering.** All three depend only on the frozen MVP surface. KDV-015 should land
last within the train: it implements the abstractions the other two may extend (e.g.,
impersonation parity in the adapter), so its conformance suite must run against the final 1.x
shape of KDV-013.

## v1.2 — Typed developer experience

| ID | Feature | Rationale |
|---|---|---|
| KDV-016 | Source-generated early-bound models (Roslyn generator from metadata snapshot) | Compile-time safety without runtime reflection or external codegen; only sensible once the late-bound model and metadata contracts are frozen at 1.0 |
| KDV-017 | Fluent strongly typed (LINQ-style) queries | The payoff of KDV-016; explicitly scoped operator set to avoid LINQ-provider trap (see [`../product/problem-statement.md`](../product/problem-statement.md)) |
| KDV-018 | Metadata snapshot export + environment comparison (CLI-friendly) | Extends metadata automation to drift detection; produces the snapshot format KDV-016 consumes |

**Dependency ordering.** KDV-018's snapshot format and KDV-016 must be co-designed first (the
format is the shared contract); KDV-016 ships the generator; KDV-017 follows, since it is
specified to build on KDV-016 (master plan §3). Recommended sequence: format design →
KDV-018 export → KDV-016 generator → KDV-018 comparison → KDV-017.

## v2.0 — Platform breadth

A major version, allowing any breaking changes accumulated since 1.0.

| ID | Feature | Rationale |
|---|---|---|
| KDV-019 | Solution dependency analysis | Turns deployment automation (KDV-007) from reactive to predictive; prerequisite for useful ALM helpers |
| KDV-020 | Power Pages Web API helpers | First-class support for the Power Pages persona beyond companion-API patterns; deferred until the portal API surface justifies a contract |
| KDV-021 | ALM pipeline helpers | Composes KDV-007 + KDV-019 into flow-level building blocks while honoring the "no pac CLI replacement" non-goal |
| KDV-022 | Plugin development helpers | Grows from the netstandard2.0 `Koras.Dataverse.FetchXml` base into sandbox-safe plugin utilities; the plugin *execution* runtime remains out of scope |

**Dependency ordering.** KDV-019 before KDV-021 (analysis feeds pipeline pre-flight checks).
KDV-020 and KDV-022 are independent of each other and of the ALM pair; KDV-022 depends only on
the long-stable FetchXml package. Each v2.0 feature passes a scope-gate review against the
out-of-scope list before design begins (master plan §8, scope-creep risk).

## Experimental

| ID | Feature | Rationale |
|---|---|---|
| KDV-023 | Elastic table / long-running operation helpers | Platform capabilities still evolving; contracting an API against unstable semantics would violate the compatibility promise |

**Handling.** Ships only under explicit experimental labeling, exempt from compatibility
guarantees until promoted. Promotion criteria (platform stability, test coverage, API review)
must be defined before any stable release. KDV-023's long-running-operation patterns should
generalize from KDV-007's job-polling machinery rather than reinvent it.

## Sequencing principles

1. **Freeze before generate.** Nothing typed-codegen (v1.2) starts before the 1.0 API freeze.
2. **Isolate weight.** Heavy dependencies (KDV-015) never enter core packages.
3. **Additive minors.** v1.1/v1.2 features are additive-only; anything breaking waits for 2.0
   (master plan §4 compatibility strategy).
4. **Scope gates.** Every v2.0 feature is checked against the non-goals list in review before
   design; "out of scope" is a maintained decision, not a historical note.
5. **Experimental is honest.** Experimental features are labeled, documented as unstable, and
   never a dependency of stable features.
