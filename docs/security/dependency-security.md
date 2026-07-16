# Dependency & Supply Chain Security

> Policy for the Koras Dataverse SDK's own dependency tree and release supply chain.
> Expands [master plan §2 and §7](../planning/master-plan.md). A compromised or bloated
> dependency tree in an SDK is inherited by every consumer, so this policy is
> deliberately restrictive.

## 1. Dependency policy: what is allowed and why

### 1.1 Current allowed set (MVP)

| Package | Dependencies | Rationale |
|---|---|---|
| `Koras.Dataverse.Abstractions` | **none** | Contracts must be adoptable with zero supply-chain cost; nothing to vet, nothing to conflict |
| `Koras.Dataverse.FetchXml` | **none** | Must run inside Dataverse plugin sandboxes (netstandard2.0) where extra deps are a liability; zero third-party keeps it universally embeddable |
| `Koras.Dataverse` | `Azure.Identity`, `Microsoft.Extensions.{Http, Options, Options.DataAnnotations, Logging.Abstractions, DependencyInjection.Abstractions, Diagnostics.HealthChecks.Abstractions}` + the two packages above | See below |
| `Koras.Dataverse.OpenTelemetry` | `Koras.Dataverse` (ids only), `OpenTelemetry.Api` | The API package only — never the full OpenTelemetry SDK/exporters |

**Why Azure.Identity + Microsoft.Extensions.\* only in core:**

- Both are Microsoft-maintained, security-serviced on a fast cadence, signed,
  ubiquitous in the target audience's dependency graphs already (so we add almost zero
  *new* surface to a typical ASP.NET Core app), and license-compatible (MIT).
- `Azure.Identity` is the positioning choice itself (master plan §1: `TokenCredential`
  auth) — reimplementing OAuth/MSAL flows in-house would be a far larger security risk
  than the dependency.
- `Microsoft.Extensions.*` packages are the abstractions layer of the platform
  (`IHttpClientFactory`, options, logging, DI, health checks). We depend on
  **abstractions** packages wherever they exist (`Logging.Abstractions`, not `Logging`;
  `DependencyInjection.Abstractions` for registration types) to keep the closure small.
- Versions are pinned per-TFM (8.0.x/9.0.x/10.0.x) via Central Package Management so
  net8.0 consumers are not forced onto newer runtimes (ADR-0002).

**Deliberately excluded from core:** Polly (we own a small, testable retry handler
purpose-built for Dataverse service protection semantics), Newtonsoft.Json
(System.Text.Json only), OpenTelemetry SDK (core emits via `ActivitySource`/`Meter`
from the BCL — the helper package exists precisely so core stays clean), any HTTP
convenience libraries, any mapper libraries.

**Zero third-party in `Abstractions`/`FetchXml`** is enforced by architecture tests
(NetArchTest) and by pack-time dependency assertions in the package-consumption suite —
not just by convention.

### 1.2 Rules for proposing a NEW dependency (lock)

New dependencies are closed by default. A PR introducing one must include a written
assessment covering all of the following, and requires maintainer sign-off plus an ADR:

1. **License:** must be MIT/Apache-2.0/BSD-class, compatible with MIT distribution.
   Copyleft, source-available-with-restrictions, or paid-commercial licenses (see the
   FluentAssertions v8 case in
   [test-strategy.md §5.1](../testing/test-strategy.md)) are rejected outright — for
   test dependencies too.
2. **Maintenance:** active releases within the last 12 months, responsive security
   process, more than one maintainer or a corporate steward; single-maintainer hobby
   packages are rejected for shipped code.
3. **Size/closure:** the full transitive closure it drags in, per TFM; anything that
   meaningfully grows consumer dependency graphs needs a strong justification.
4. **Public-API leakage assessment:** does any type from the dependency appear in our
   public API surface? Leakage into `Abstractions` is banned absolutely (master plan
   §4: no third-party types in Abstractions). Leakage elsewhere couples our semver to
   theirs and is presumptively rejected (`Azure.Core.TokenCredential` is the single
   sanctioned exception, by design).
5. **Alternatives considered:** why not the BCL, why not ~50 lines of owned code.
6. **Placement:** could it live in an optional add-on package instead of core?

The same gate applies to *build-time* components (analyzers, SDK targets) at slightly
relaxed size criteria, because build-time compromise is still supply-chain compromise.

## 2. Automated dependency hygiene (CI)

All of the following are part of the repository CI from milestone 0:

- **Dependabot:** weekly update PRs for NuGet dependencies and GitHub Actions versions;
  security updates immediate. Grouped minor/patch updates to keep noise manageable.
  Dependabot PRs run the full test + package-consumption matrix like any PR.
- **Dependency review action:** on every PR, `actions/dependency-review-action` flags
  newly introduced dependencies with known vulnerabilities or disallowed licenses,
  failing the PR — this is the automated half of the §1.2 lock.
- **CodeQL:** C# analysis on every PR and on a weekly schedule against the default
  branch; alerts triaged within one week; no release with open high/critical alerts.
- **Vulnerability scan:** CI runs
  `dotnet list package --vulnerable --include-transitive` against the restored solution
  and fails on any known-vulnerable package, including transitive ones. This catches
  advisories between Dependabot cycles and covers packages Dependabot cannot bump
  (transitives pinned by others).
- **Pinned actions:** GitHub Actions workflows pin third-party actions by commit SHA,
  not floating tags.
- **NuGet lock/CPM discipline:** Central Package Management
  (`Directory.Packages.props`) gives one reviewable file for every version in the
  repository; version changes cannot hide inside csproj diffs.

## 3. Release supply chain

- **SBOM at release:** every release build generates a CycloneDX (or SPDX) SBOM per
  package from the actual restored graph and attaches it to the GitHub Release
  ([release-process.md](../release/release-process.md)). Consumers get a machine-
  readable inventory; we get an audit trail per version.
- **Deterministic, source-linked builds:** deterministic build + SourceLink + embedded
  untracked sources from milestone 0, so shipped binaries are reproducibly traceable to
  a commit.
- **Protected publish path:** NuGet publishing runs only in the `nuget-release` GitHub
  environment with required reviewers; the API key is scoped to push-only for the
  `Koras.*` prefix ([nuget-publishing.md](../release/nuget-publishing.md)).
- **NuGet package signing plan:** NuGet.org repository-signs all packages on upload
  (baseline integrity for consumers today). **Author signing** with a
  Koras Technologies code-signing certificate is planned at **1.0**: certificate
  acquisition, signing in the release workflow via `dotnet nuget sign` before push, the
  certificate registered on the NuGet.org profile, and a documented
  timestamping + rotation procedure. Pre-1.0 packages are honest about relying on
  repository signing only. The signing step is wired as sign-when-configured so the
  workflow is ready before the certificate exists.
- **Prefix reservation:** `Koras.*` reserved on NuGet.org (release checklist) so
  squatters cannot publish look-alike packages.
- **2FA:** required on the NuGet.org organization and GitHub organization accounts that
  can touch the release path.

## 4. Consuming advisories / responding to vulnerable dependencies

1. Advisory lands (Dependabot alert, CI `--vulnerable` failure, or private report via
   SECURITY.md).
2. Assess exploitability in our usage within 2 business days; document the assessment
   on the tracking issue (private security advisory if the impact is on consumers).
3. If exploitable through the SDK: patch release on the current minor (and the previous
   major once the LTS policy is active — see
   [versioning.md](../release/versioning.md)) bumping the minimum dependency version;
   release notes mark it clearly as a security release.
4. If not exploitable through the SDK: still bump in the next scheduled release; note
   the assessment so consumers scanning transitives can suppress with evidence.
5. Never silently downgrade or pin around an advisory.

## 5. What consumers inherit (transparency)

A consumer installing `Koras.Dataverse` gets: our four packages' assemblies,
`Azure.Identity` (and its Azure.Core/MSAL closure), and Microsoft.Extensions
abstractions they almost certainly already have. Consumers installing only
`Koras.Dataverse.Abstractions` or `Koras.Dataverse.FetchXml` get **zero** third-party
packages. Keeping that sentence true across every release is the primary acceptance
test of this policy.
