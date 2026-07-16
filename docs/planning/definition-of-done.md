# Definition of Done

> Binding quality bar at three levels: **task** (backlog item, KDV-xxx-Tn), **feature**
> (KDV-xxx), and **release**. Consistent with
> [master plan §3/§6/§8](master-plan.md) ("Every MVP feature ships with: unit tests,
> error-path tests, cancellation tests, docs page, runnable sample usage, and XML
> IntelliSense docs"). Nothing is "done" by declaration — every criterion is verifiable
> in CI output, a review record, or a checklist artifact.

## 1. Task-level DoD (every backlog task / PR)

A task ([backlog.md](backlog.md)) is done when its PR merges, and a PR may merge only
when:

- [ ] **Build green** on the full CI matrix (Linux + Windows × net8.0/net9.0/net10.0;
      FetchXml additionally netstandard2.0), warnings-as-errors, zero analyzer
      warnings.
- [ ] **No analyzer suppressions without justification:** every new
      `#pragma warning disable`, `[SuppressMessage]`, `.editorconfig` severity
      downgrade, or nullable-suppression (`!`) beyond the trivial carries a comment
      explaining why, and the reviewer explicitly accepts it.
- [ ] **Tests land with the code:** the test cases the
      [test matrix](../testing/test-matrix.md) assigns to the touched behavior exist
      and pass — for I/O paths that always includes invalid-input, failure-path, and
      cancellation tests, not just happy path. No real network, no real-clock sleeps
      in unit/component tests.
- [ ] **Coverage ratchet respected:** coverage does not drop below the high-water mark
      (tolerance and waiver rules per [test-strategy §8](../testing/test-strategy.md)).
- [ ] **Public API discipline:** any surface change appears in
      `PublicAPI.Unshipped.txt`, matches master plan §4 (or an approved API-doc
      update), and received API-review sign-off in the PR. New public members have
      complete XML docs (params, returns, exceptions, thread-safety/cancellation notes
      where relevant).
- [ ] **Architecture rules hold:** arch tests green — dependency direction,
      zero third-party in Abstractions/FetchXml, sealed/abstract, Async +
      `CancellationToken` conventions.
- [ ] **Security rules hold** where touched: encoding for any value reaching a
      query/URL/payload (with corpus tests), no secrets/tokens in logs or exceptions,
      options validation for new options ([threat model](../security/threat-model.md)).
- [ ] **No secrets in the diff:** no credentials, real tenant/org identifiers, or
      captured payloads containing real data — placeholder values only (secret
      scanning is the backstop, not the check).
- [ ] **Docs impact handled:** XML docs always; affected concept/recipe pages updated
      in the same PR once those pages exist (M7+); changelog entry under `Unreleased`
      for consumer-visible changes.
- [ ] **Task hygiene:** backlog task's own "Done when" criteria met; commit/PR
      references the task id (KDV-xxx-Tn).

## 2. Feature-level DoD (every KDV-xxx before its milestone closes)

A feature is done when all its backlog tasks are done **and**:

- [ ] **Test matrix row complete:** every dimension in
      [test-matrix.md](../testing/test-matrix.md) for the feature (happy path,
      invalid input, boundary, cancellation, failure path, dependency failure,
      configuration, thread safety) has passing tests.
- [ ] **Error paths mapped:** the feature's failures surface as documented
      `DataverseError` categories — no raw `HttpRequestException`/`JsonException`
      leaks to consumers (except the never-wrapped `OperationCanceledException`).
- [ ] **Cancellation proven:** pre-canceled and mid-flight cancellation tests pass;
      no swallowing or wrapping.
- [ ] **Thread-safety statement:** the feature's types are covered by the documented
      threading contract (thread-safe singletons vs. not-thread-safe builders) and
      tests/docs agree.
- [ ] **Docs page** for the feature (from M7: exists and reviewed; before M7: content
      captured in the feature's tracking issue for M7 assembly).
- [ ] **Runnable sample usage** demonstrating the feature (in `samples/` from M7).
- [ ] **XML IntelliSense docs** complete across the feature's public surface.
- [ ] **Observability wired** (from M6): the feature's operations emit the documented
      activities/metrics/logs within the
      [data-protection tag policy](../security/data-protection.md).
- [ ] **Live integration coverage** (from M8) for features with a live-testable
      surface, per [integration-testing.md](../testing/integration-testing.md).
- [ ] Feature listed correctly in the changelog and the feature catalog status.

## 3. Release-level DoD (every published version)

A release is done when:

- [ ] **[Release checklist](../release/release-checklist.md) fully executed** — scope
      vs. release train, CI matrix green, coverage targets met (≥ 80 % line / ≥ 70 %
      branch on core packages), live suite green, package-consumption suite green,
      benchmarks archived with no unexplained hot-path regressions.
- [ ] **[Security checklist](../security/security-checklist.md) fully executed** —
      scans clean, redaction/injection suites green, threat model reviewed, supply-
      chain items (SBOM, pinned actions, protected publish) verified.
- [ ] **Versioning correct:** version matches the diff per
      [versioning.md](../release/versioning.md); deprecations and (0.x) breaking
      changes explicitly called out; migration notes written where required.
- [ ] **Changelog complete** for the version, every entry traceable to a KDV id or PR.
- [ ] **API review closed:** `PublicAPI.Unshipped.txt` reviewed (and promoted for
      stable releases); package validation green (baseline respected post-1.0).
- [ ] **No unjustified suppressions** anywhere in the shipped source (audited, not
      assumed).
- [ ] **No secrets** in the repository, packages, or workflow logs (checked per the
      security checklist).
- [ ] **Published and verified:** packages + symbols on NuGet.org, listing renders,
      clean-machine install test passes, GitHub Release with notes/SBOMs/benchmark
      artifacts, checklists archived in the release issue.

## 4. Explicit non-negotiables (summary)

The items that are never waived, at any level: green build, cancellation correctness,
injection-encoding tests for new value paths, secret/token redaction, public API
tracked via PublicAPI files, and no secrets in the repo. Everything else can, in
principle, be waived by a maintainer **in writing** (PR or release issue) with a
reason — silent waivers do not exist.
