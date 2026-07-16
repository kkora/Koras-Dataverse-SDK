# Security checklist dry run — 2026-07-16

First execution of [security-checklist.md](../security-checklist.md) against the implemented
code (milestone 8 exit item). Run on main at the commit introducing this file; mechanical
checks were executed for real, not assumed. Status legend: ✅ pass · ⚠️ open finding ·
➖ not applicable yet.

## Results

### §1 Static analysis and scanning

- ✅ CodeQL: 0 open alerts (default setup; csharp + actions).
- ✅ `dotnet list package --vulnerable --include-transitive`: clean across all projects.
- ✅ Dependency review action passing (enabled with the dependency graph).
- ✅ Dependabot alert queue: 0 open alerts.
- ✅ Suppressions: exactly one `[SuppressMessage]` in the repo (benchmarks, CA1001) and it
  carries a justification; no `#pragma warning disable` anywhere.

### §2 Secrets and credential handling

- ⚠️ **Secret scanning and push protection are disabled** (they did not auto-enable when the
  repository became public). Owner toggle: Settings → Security analysis. Free for public repos.
- ⚠️ Dependabot *security updates* also disabled (alerts work; automatic fix PRs don't).
- ✅ Release-diff grep for secrets/tenant ids/real URLs: nothing found; samples read secrets
  from user-secrets/env vars and contain placeholders only.
- ⚠️ Redaction coverage is *partial*: token-provider tests exist, but there is no dedicated
  test asserting no `Authorization`/token material in any log output path. Recommend an
  explicit redaction test pass (tracked in the follow-up issue).

### §3 Injection and input handling

- ✅ Encoding-corpus tests present for OData literals and FetchXML escaping
  (`ODataFilterBuilderTests`, FetchXml builder tests with escaping cases).
- ✅ `ODataQuery.WhereRaw` XML docs warn against interpolating untrusted input and point to
  `Where`.
- ✅ No polymorphic deserialization; `JsonDocument`/explicit converters only.

### §4 Transport and configuration safety

- ✅ HTTPS-only enforcement covered by options-validation tests (KDV-010 suite).
- ✅ No option exists that weakens TLS validation or redirects.

### §5 Resilience / DoS posture

- ✅ Retry budget bounded (`DataverseRetryOptions.MaxRetries`), `Retry-After` honored, jitter
  present — RetryHandler suite green.
- ✅ Batch 1000-operation guard tested.
- ✅ **Error-body size cap: was missing — fixed in this change.** `DataverseErrorParser` now
  reads at most 64 K chars; larger payloads skip parsing and classify from the status code
  (regression tests added). Note: the HTTP pipeline still buffers responses before the parser
  sees them; bounding the transport read itself would require `ResponseHeadersRead` semantics
  in the client — assessed as acceptable for now and left as a future hardening option.
- ✅ `nextLink` following restricted to the configured host; paging/polling cancelable.

### §6 Threat model and docs

- ✅ No feature since the last release crosses a new trust boundary (icon/infra changes only).
- ✅ SECURITY.md present with private-reporting channel; supported-versions table is
  pre-release ("latest preview only") — revisit at 1.0.

### §7 Supply chain and release integrity

- ⚠️ **No SBOM generation** in the release workflow.
- ⚠️ **GitHub Actions referenced by tag, not SHA** (`actions/checkout@v4`, `setup-dotnet@v4`,
  `NuGet/login@v1`, `softprops/action-gh-release@v2`).
- ⚠️ **`nuget-release` environment has no required reviewers** — a pushed tag publishes
  without a human gate.
- ⚠️ Checklist §7 references `NUGET_API_KEY` scope/expiry — **outdated**: publishing now uses
  NuGet Trusted Publishing (OIDC, short-lived keys). The checklist itself needs updating.
- ⚠️ `Koras.*` prefix reservation not yet filed (owner action, nuget.org).
- ➖ Author signing is a 1.0+ item.
- ⚠️ 2FA on the NuGet.org and GitHub accounts: requires owner attestation (not verifiable
  from the repository).
- ⚠️ Deterministic-build rebuild verification not yet performed for any release.

## Disposition

The ⚠️ items are tracked in the follow-up GitHub issue (release-integrity hardening) with
owners; the error-body cap was closed in the same PR as this record. Re-run this checklist
at every release per the release process.
