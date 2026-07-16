# Security Checklist (Pre-Release)

> Actionable checklist executed before **every** release (preview and stable), as part of
> the overall [release checklist](../release/release-checklist.md). Items marked
> *(1.0+)* apply from the 1.0 release onward. Every unchecked box blocks the release or
> requires a written maintainer waiver recorded in the release issue.

## 1. Static analysis and scanning

- [ ] CodeQL: no open high/critical alerts on the release commit; medium alerts triaged
      with dispositions recorded.
- [ ] `dotnet list package --vulnerable --include-transitive` clean on the release
      commit (all TFMs restored).
- [ ] Dependency review action passing; no new dependencies since last release without
      an approved assessment + ADR
      ([dependency-security.md §1.2](dependency-security.md)).
- [ ] Dependabot alert queue empty or every open alert explicitly assessed as
      not exploitable via the SDK, with the assessment linked.
- [ ] .NET analyzers + security-relevant rules (CAxxxx security categories) at the
      configured severity with **zero suppressions lacking a justification comment**
      (grep for `#pragma warning disable` / `[SuppressMessage]` and verify each).

## 2. Secrets and credential handling

- [ ] Repository secret scanning (with push protection) enabled and no open secret
      alerts.
- [ ] Manual grep of the release diff for accidental secrets, tokens, tenant ids of
      real customers, or internal URLs in code, tests, samples, and docs.
- [ ] Redaction tests green: no `Authorization` header/token/secret in any log path
      ([data-protection.md §2.3](data-protection.md)).
- [ ] Sample code and docs contain **placeholder** credentials only; committed
      `appsettings` samples match the placeholder shape in
      [secure-configuration.md §5](secure-configuration.md).
- [ ] Integration-test credentials confined to CI environment secrets; not present in
      any workflow log (check a recent nightly run's logs).

## 3. Injection and input handling

- [ ] Hostile-input encoding corpus tests green for every builder value position
      (OData literals, FetchXML escaping) — see
      [test-strategy.md §4.14](../testing/test-strategy.md).
- [ ] Any *new* public parameter that reaches a query, URL, header, or payload since
      the last release has corresponding encoding/validation tests (review the
      `PublicAPI.Unshipped.txt` diff with this lens).
- [ ] Raw-string escape hatches (raw FetchXML execution, raw OData fragments) carry the
      injection warning in XML docs and feature docs
      ([threat-model.md §2.2](threat-model.md)).
- [ ] No polymorphic deserialization introduced (architecture test green; manual review
      of any new `JsonSerializerOptions`/converters).

## 4. Transport and configuration safety

- [ ] HTTPS-only enforcement tests green (non-HTTPS `EnvironmentUrl` rejected at
      startup validation).
- [ ] No cross-host redirect following with credentials (test green).
- [ ] No new option weakens TLS validation, redirects, or redaction (review options
      diff).
- [ ] Startup options validation covers every new option added this release.

## 5. Resilience / DoS posture

- [ ] Retry budget bounded; `Retry-After` honored; jitter present (KDV-008 suites
      green).
- [ ] Batch 1000-operation guard and error-body size cap in place (tests green).
- [ ] No unbounded loops added on server-controlled inputs (`nextLink`, paging cookies,
      job polling all bounded/cancelable — review + tests).

## 6. Threat model and docs

- [ ] [threat-model.md](threat-model.md) reviewed against the release's feature diff;
      updated if any feature crosses a trust boundary in a new way (mandatory for
      KDV-013 impersonation and KDV-014 file columns when they land).
- [ ] SECURITY.md current: supported-versions table matches the LTS policy in
      [versioning.md](../release/versioning.md); private reporting channel verified
      working.
- [ ] Data-protection rules hold for any new logs/tags added this release (no attribute
      values at Information+, no row data in traces —
      [data-protection.md](data-protection.md)).

## 7. Supply chain and release integrity

- [ ] SBOM generated for every package and attached to the release artifacts.
- [ ] Deterministic build verified (rebuild produces identical package contents) and
      SourceLink metadata present in packages.
- [ ] GitHub Actions in release workflow pinned by SHA; no new unpinned third-party
      actions.
- [ ] Publish restricted to the `nuget-release` environment with required reviewers.
      Publishing uses NuGet.org **Trusted Publishing** (OIDC): verify the trusted-publishing
      policy on nuget.org still targets exactly this repository, `release.yml`, and the
      `nuget-release` environment, and that no long-lived NuGet API key exists as a
      repository/environment secret ([nuget-publishing.md](../release/nuget-publishing.md)).
- [ ] *(1.0+)* Packages author-signed with the Koras certificate; signature and
      timestamp verified on the packed artifacts before push.
- [ ] `Koras.*` NuGet prefix reservation active.
- [ ] 2FA confirmed on NuGet.org and GitHub accounts in the release path.

## 8. Post-release

- [ ] Verify published packages on NuGet.org match built artifacts (version, hash where
      obtainable, repository-signature present).
- [ ] Archive this completed checklist in the release issue for the audit trail.
