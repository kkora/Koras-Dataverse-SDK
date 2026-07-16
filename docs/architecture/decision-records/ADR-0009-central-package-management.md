# ADR-0009: Central Package Management

## Status

Accepted — 2026-07-16 · **Amended — 2026-07-16** (see "Amendment: single Microsoft.Extensions
version line" below)

## Context

The repository will contain multiple shipping packages, several test projects, sample apps
(console, minimal API, worker), and packaging/compat test projects (master plan §6, §8). Each
references overlapping sets of NuGet packages. Managing `Version=` attributes per project
invites drift: two projects silently on different versions of the same package, upgrade PRs
touching a dozen files, and review noise that hides real changes.

Additionally, ADR-0002 requires Microsoft.Extensions.* versions to differ **per target
framework** (8.0.x on net8.0, 9.0.x on net9.0, 10.0.x on net10.0) — a policy that must be
expressed once, not per project.

NuGet's Central Package Management (CPM) with `Directory.Packages.props` and
`ManagePackageVersionsCentrally` is the standard solution and works with conditional
`ItemGroup`s for per-TFM versions.

## Decision

We will manage all package versions centrally via a repository-root
**`Directory.Packages.props`** with `ManagePackageVersionsCentrally=true`:

- Project files declare `PackageReference` items **without** `Version` attributes; all
  versions live in `Directory.Packages.props` as `PackageVersion` items.
- Microsoft.Extensions.* (and any other runtime-band-aligned packages) use **conditional
  `PackageVersion` items keyed on `$(TargetFramework)`**: 8.0.x for net8.0, 9.0.x for
  net9.0, 10.0.x for net10.0.
- Floating versions and ranges are prohibited; exact versions only.
- `VersionOverride` on individual projects is prohibited except with an explicit review
  justification recorded in the PR (expected use: none).
- Build/analyzer/test tooling versions are centralized in the same file.

Dependabot and CI checks (`dotnet list package --vulnerable`, master plan §7) operate against
this single file.

## Consequences

- One-file dependency review: any PR that changes a version touches
  `Directory.Packages.props`, making the dependency-policy checklist
  ([`../dependency-rules.md`](../dependency-rules.md)) easy to trigger and audit.
- Per-TFM pinning is expressed exactly once and applies uniformly to every project that
  multi-targets.
- Version drift between shipped packages, tests, and samples becomes structurally
  impossible.
- Contributors must learn the CPM convention (no `Version` in project files); a build error
  results otherwise, which is self-correcting.
- Conditional `PackageVersion` blocks add some props-file complexity; kept manageable by
  grouping per TFM with comments.
- Transitive-pinning (`CentralPackageTransitivePinningEnabled`) is **not** enabled initially
  — it can create surprising graphs for package consumers; revisit only with a superseding
  ADR if a vulnerable-transitive situation demands it.

## Alternatives considered

- **Per-project `Version` attributes.** Rejected: drift-prone across the project count this
  repo will reach; upgrade PRs become multi-file noise.
- **MSBuild version variables in `Directory.Build.props` (pre-CPM pattern).** Rejected:
  reinvents CPM without tooling support (Dependabot, NuGet audit integration understand CPM
  natively).
- **Paket.** Rejected: additional toolchain for contributors and CI; CPM is first-party and
  sufficient.
- **Single unconditioned version per package (no per-TFM split).** Originally rejected as
  conflicting with ADR-0002's consumer-friendliness requirement for net8.0 users — but see
  the amendment below.

## Amendment: single Microsoft.Extensions version line (2026-07-16)

Implementation surfaced a hard constraint: `Azure.Core` 1.60.0 (required by `Azure.Identity`)
transitively floors the dependency graph at Microsoft.Extensions **10.0.x** on every TFM,
including net8.0. Per-TFM downpinning (8.0.x on net8.0) therefore produces NU1605 package
downgrades and cannot be honored. Since the 10.0.x Microsoft.Extensions packages themselves
target net8.0+, net8.0 consumers are unaffected at runtime.

**Amended decision:** `Directory.Packages.props` pins Microsoft.Extensions.* to a single
10.0.x version line for all TFMs. The per-TFM conditional mechanism described above remains
the documented pattern should a future dependency graph allow (or require) split versions.
CPM itself, exact-version pinning, and all other rules are unchanged.
