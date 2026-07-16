# Risk Register

> Expands the [master plan §8](master-plan.md#8-delivery-plan) risk table with
> likelihood, impact, mitigation, owner, and trigger (the observable event that
> activates the contingency). Reviewed at every milestone exit
> ([implementation-plan.md](implementation-plan.md)) and before every release.
> Owner roles: **Maintainers** (project leads at Koras Technologies), **Release
> manager** (maintainer running a given release), **Security contact** (SECURITY.md
> responder). Likelihood/impact scale: Low / Medium / High.

## R1 — Microsoft ships a modern first-party Web API SDK

- **Category:** Market / adoption. **Likelihood:** Medium. **Impact:** High (adoption).
- **Description:** A first-party HttpClientFactory-era Dataverse SDK would absorb most
  of the demand this SDK targets.
- **Mitigation (proactive):** Differentiate on DX and observability (master plan §1
  differentiators); keep the architecture adapter-friendly so Koras value-adds
  (builders, error taxonomy, telemetry) could sit over an official transport, as
  already planned for the OrganizationService adapter (KDV-015).
- **Contingency:** Re-scope toward the differentiated layers (FetchXml builder,
  observability, resilience contracts) as companions rather than a competing transport.
- **Owner:** Maintainers.
- **Trigger:** Public Microsoft announcement/preview of a modern Web API .NET SDK →
  positioning review within 30 days, documented as an ADR.

## R2 — Web API contract drift across versions

- **Category:** Technical / correctness. **Likelihood:** Medium. **Impact:** Medium-High
  (silent breakage for consumers).
- **Description:** Payload shapes, headers, or error formats change server-side
  (Dataverse is continuously deployed) and diverge from our pinned assumptions.
- **Mitigation:** Pin API version `v9.2` in requests; error-tolerant parsing (unknown
  members ignored, fallback error mapping); contract fixtures kept alongside tests;
  nightly live integration suite as the drift alarm
  ([integration-testing.md](../testing/integration-testing.md)).
- **Contingency:** Patch release updating parsers/fixtures; if a breaking service
  change is announced, tracked issue + migration note ahead of enforcement dates.
- **Owner:** Maintainers.
- **Trigger:** Nightly live suite failure not attributable to our changes, or a
  Microsoft release-wave announcement touching the Web API → triage within 2 business
  days.

## R3 — Auth complexity (sovereign clouds, ADFS, unusual tenants)

- **Category:** Support load. **Likelihood:** High (auth is where integration projects
  bleed). **Impact:** Medium.
- **Description:** Credential flows differ across clouds (GCC/DoD/China), proxies, and
  policy-heavy tenants; failures land as SDK issues regardless of root cause.
- **Mitigation:** Standard `TokenCredential` escape hatch (`UseTokenCredential`) so any
  Azure.Identity-supported topology works; clouds documented via `EnvironmentUrl`
  (scope derived from it, no hardcoded cloud endpoints); actionable error mapping for
  auth failures; troubleshooting page in the M7 docs.
- **Contingency:** Grow a triage FAQ from real issues; add credential-specific docs
  rather than credential-specific code where possible.
- **Owner:** Maintainers.
- **Trigger:** ≥ 3 distinct auth-topology issues filed in a month → dedicate a docs/
  diagnostics iteration in the next minor.

## R4 — Throttling semantics change

- **Category:** Reliability. **Likelihood:** Low-Medium. **Impact:** Medium.
- **Description:** Service protection limit behavior (status codes, headers, budgets)
  evolves; a wrong retry policy amplifies outages instead of absorbing them.
- **Mitigation:** Central retry policy (single `RetryHandler`), `Retry-After` always
  honored verbatim, `DataverseRetryOptions` tunable without code changes, bounded
  budgets, jitter; unit tests pin current semantics so a deliberate change is loud.
- **Contingency:** Patch release adjusting the central policy; options let affected
  consumers mitigate before upgrading.
- **Owner:** Maintainers.
- **Trigger:** Live-suite observations or credible reports of new throttling
  status/header behavior → policy review within 1 week.

## R5 — API frozen too early

- **Category:** Maintenance. **Likelihood:** Medium. **Impact:** High (breaking-change
  cost compounds for an SDK).
- **Description:** Freezing at 1.0 with design mistakes bakes them in under the
  [compatibility policy](../release/versioning.md).
- **Mitigation:** 0.x preview window with an explicit feedback period after
  0.1.0-preview.1 (master plan §8); PublicAPI analyzer tracking makes every surface
  change reviewable from day one; API review sign-off per PR touching surface; 0.x
  minors may break with changelog callouts.
- **Contingency:** Delay 1.0 rather than freeze known warts; deprecation process for
  anything discovered post-freeze.
- **Owner:** Maintainers (API review).
- **Trigger:** Unresolved API-design issues open when the 1.0 train is proposed → 1.0
  does not proceed until they close.

## R6 — Scope creep (ALM tooling, generators, connectors)

- **Category:** Focus / delivery. **Likelihood:** High. **Impact:** Medium (MVP slips).
- **Description:** Dataverse's surface invites endless features; master plan §1 lists
  the non-goals, §3 assigns release trains for a reason.
- **Mitigation:** Feature gates by release train (KDV table); out-of-scope list
  enforced in PR review; backlog tasks reference KDV ids so unplanned work is visible;
  scope changes require a master-plan update first
  ([implementation-plan.md](implementation-plan.md) ground rules).
- **Contingency:** Park out-of-train contributions as draft PRs/issues tagged for
  their train.
- **Owner:** Maintainers.
- **Trigger:** Any PR implementing an unassigned or later-train feature → close or
  re-train it, never quietly merge.

## R7 — Injection or credential-handling defect ships (security)

- **Category:** Security. **Likelihood:** Low (with controls) / consequence-driven
  entry. **Impact:** High (consumer data exposure, trust loss).
- **Description:** An encoding gap in builders, a token in logs, or an SSRF path via
  `EnvironmentUrl`/redirects reaches a released package.
- **Mitigation:** [Threat model](../security/threat-model.md) with enforced test
  suites (hostile-input corpus, redaction, HTTPS/redirect rules); security checklist
  on every release; CodeQL + dependency scanning; private reporting via SECURITY.md.
- **Contingency:** Security response path in
  [dependency-security.md §4](../security/dependency-security.md): assess ≤ 2 business
  days, patch release, advisory, unlist affected versions.
- **Owner:** Security contact + Release manager.
- **Trigger:** Any security report or scanner finding rated ≥ medium → response clock
  starts immediately.

## R8 — Dependency shock (Azure.Identity/Extensions major or advisory)

- **Category:** Supply chain / compatibility. **Likelihood:** Medium. **Impact:**
  Medium.
- **Description:** A required dependency ships a breaking major, an incompatible
  security fix, or (worst case) a license change — cf. the FluentAssertions v8 episode
  that already shaped test tooling ([test-strategy §5.1](../testing/test-strategy.md)).
- **Mitigation:** Minimal dependency set with per-TFM pins; `TokenCredential` is the
  only third-party type on the public surface (contains the blast radius);
  dependency-update policy in [versioning.md §6](../release/versioning.md); Dependabot
  + review gates.
- **Contingency:** Security-forced floor raise in a minor with changelog callout;
  dependency major treated as SDK major unless proven compatible; license change →
  pin last good version short-term, replace via ADR long-term.
- **Owner:** Maintainers.
- **Trigger:** Dependency major announced, advisory affecting our floor, or license
  change detected by dependency review → assessment issue within 1 week.

## R9 — Live test environment fragility

- **Category:** Delivery / quality signal. **Likelihood:** Medium. **Impact:** Low-
  Medium (blocked releases, noisy signal — release gating depends on the live suite).
- **Description:** The dedicated Dataverse test environment expires (trial resets),
  credentials rotate unexpectedly, or shared-tenant policy changes break nightly runs.
- **Mitigation:** Environment + app registration documented as infrastructure
  (recreate runbook kept with CI docs); suite designed for any vanilla environment
  (standard tables only, self-isolating prefixes); skip-clean behavior keeps PRs
  unaffected.
- **Contingency:** Recreate environment from the runbook; releases hold on the live
  gate rather than waiving it.
- **Owner:** Release manager.
- **Trigger:** Two consecutive nightly failures with environment/auth signatures →
  runbook execution, not test patching.

## R10 — Single-maintainer bus factor (project sustainability)

- **Category:** Sustainability. **Likelihood:** Medium (early OSS reality). **Impact:**
  Medium-High over time.
- **Description:** Adoption strategy (master plan §1) depends on responsive
  maintenance; a thin maintainer bench stalls reviews, security response, and releases.
- **Mitigation:** Everything runbook'd (this docs tree); CI does the remembering
  (gates, checklists); protected release path works with any authorized maintainer;
  CONTRIBUTING lowers the entry bar; org-owned (not personal) NuGet/GitHub accounts.
- **Contingency:** Widen the maintainer group from active contributors; if capacity
  drops durably, narrow supported scope explicitly rather than rot silently.
- **Owner:** Maintainers (Koras Technologies).
- **Trigger:** Review latency > 2 weeks or a missed security-response clock →
  sustainability review.
