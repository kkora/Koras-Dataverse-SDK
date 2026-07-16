# MVP Scope — Koras Dataverse SDK

> Defines the MVP (release trains `0.1.0-preview.1` → `0.1.0`) per §3 and §8 of
> [`docs/planning/master-plan.md`](../planning/master-plan.md). Per-feature detail lives in
> [`feature-catalog.md`](feature-catalog.md) and the linked planning docs.

## MVP feature list

| ID | Feature | Detail doc |
|---|---|---|
| KDV-001 | Authentication (client secret, certificate, managed identity, interactive/dev, `DefaultAzureCredential`, custom `TokenCredential`/`IDataverseTokenProvider`) | [`authentication.md`](authentication.md) |
| KDV-002 | CRUD + upsert + alternate keys, late-bound `Entity`, typed POCO mapping | [`crud-operations.md`](crud-operations.md) |
| KDV-003 | OData query builder + execution + `IAsyncEnumerable` auto-paging | [`odata-queries.md`](odata-queries.md) |
| KDV-004 | FetchXML builder + execution + paging-cookie paging | [`fetchxml.md`](fetchxml.md) |
| KDV-005 | Batch (`$batch`), atomic change sets, continue-on-error | [`batch-operations.md`](batch-operations.md) |
| KDV-006 | Metadata: tables, columns, choices, relationships (read-only) | [`metadata.md`](metadata.md) |
| KDV-007 | Solutions: export, import (async job polling), publish-all, query installed | [`solutions.md`](solutions.md) |
| KDV-008 | Resilience: Retry-After, throttling awareness, timeout, jittered backoff | [`resilience.md`](resilience.md) |
| KDV-009 | Strong error model: `DataverseError`, categories, transient flag, request id | [`error-model.md`](error-model.md) |
| KDV-010 | DI + options: `AddDataverse`, named clients, factory, startup validation | [`dependency-injection.md`](dependency-injection.md) |
| KDV-011 | Observability: logging, `ActivitySource`, `Meter`, OTel helper package | [`observability.md`](observability.md) |
| KDV-012 | Health checks (`WhoAmI` probe) | [`health-checks.md`](health-checks.md) |

**MVP packages.** `Koras.Dataverse.Abstractions`, `Koras.Dataverse.FetchXml`,
`Koras.Dataverse`, `Koras.Dataverse.OpenTelemetry` (master plan §2).

## Why exactly this set

The MVP is the smallest set that makes the SDK *production-usable*, not merely demo-usable. A
Dataverse client without resilience (KDV-008) or a real error model (KDV-009) fails its first
production load test; one without DI/options (KDV-010) or observability (KDV-011) fails
enterprise review. CRUD, queries, FetchXML, batch, metadata, and solutions (KDV-002…KDV-007)
cover the six primary use cases in
[`../product/use-cases.md`](../product/use-cases.md); health checks (KDV-012) complete the
operable-service story at low cost.

## Explicit exclusions and why

**Deferred features** (planned, but not MVP):

- **KDV-013 Impersonation (v1.1).** Valuable but additive; not required for any primary use
  case to function. Deferring keeps the MVP auth surface small during the API feedback window.
- **KDV-014 File & image columns (v1.1).** Chunked streaming is a substantial sub-protocol;
  cutting it protects MVP schedule without blocking core scenarios.
- **KDV-015 Organization Service transport (v1.1).** The core promise is Web API first; the
  adapter exists for conservative estates and must not delay or contaminate the core (heavy
  dependencies stay out of MVP packages).
- **KDV-016/017/018 Typed models, typed queries, snapshots (v1.2).** The late-bound model must
  stabilize first; generators built on an unfrozen surface would churn.
- **KDV-019/020/021/022 (v2.0)** and **KDV-023 (Experimental).** Breadth features gated to
  protect focus — the top scope-creep risk in master plan §8.

**Permanent exclusions** (out of scope by design, master plan §1): UI components, data
migration engines, plugin execution runtime, XRM tooling replacements (pac CLI), Power Automate
connectors, on-premises pre-9.x support, WCF Organization Service in the core package. These
are excluded because they change the product category (tool/engine/runtime rather than SDK) or
anchor the SDK to legacy stacks.

## MVP acceptance criteria

1. **Feature completeness.** All twelve MVP features meet the acceptance criteria in their
   catalog entries and detail docs.
2. **Per-feature quality bar** (master plan §3): unit tests, error-path tests, cancellation
   tests, docs page, runnable sample usage, XML IntelliSense docs — for every MVP feature.
3. **Test strategy satisfied** (master plan §6): unit, architecture, integration (skipped
   without credentials, CI-safe), and package/compat test suites in place; coverage ratchet at
   ≥ 80 % line / ≥ 70 % branch on `Koras.Dataverse` + `FetchXml`.
4. **Architecture rules hold.** `Abstractions` and `FetchXml` have zero third-party
   dependencies; dependency direction enforced by architecture tests; public types sealed or
   abstract; async suffix conventions (master plan §2, §6).
5. **Security checklist.** HTTPS-only environment URLs enforced; no tokens in logs; builders
   encode all values; CI security scanning (CodeQL, dependency review, vulnerable-package
   check) enabled (master plan §7).
6. **Packaging.** `dotnet pack` output validated (README, icon, symbols, deps, TFMs); consumer
   project compiles against each TFM from a local feed; TFMs per master plan §2 (net8.0/9.0/
   10.0; FetchXml additionally netstandard2.0).
7. **Samples.** Console, minimal API, and worker samples build and run the primary flows.
8. **Integration proof.** Integration suite passes against a real Dataverse environment
   covering CRUD round-trip, paging, batch, metadata, and WhoAmI (master plan §6).

## Definition of MVP done

The MVP is **done** when:

- `0.1.0-preview.1` is published to NuGet.org for all four MVP packages, with `Koras.*` prefix
  reservation completed;
- all eight acceptance criteria above are verifiably met (CI green on the release commit);
- the quick start takes a new user from `dotnet add package` to a successful query in five
  minutes using only published docs;
- the API feedback window is open: feedback issue template live, public API surface tracked in
  `PublicAPI.Unshipped.txt`, and the 0.1.0 stabilization criteria published;
- known deviations, if any, are documented in the release notes — not silently shipped.

`0.1.0` (stabilized MVP) additionally requires: feedback-window API changes resolved, no open
API-shape issues, and the comparison guide vs ServiceClient published (see
[`../product/product-roadmap.md`](../product/product-roadmap.md)).
