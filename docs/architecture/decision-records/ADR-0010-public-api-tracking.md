# ADR-0010: Public API tracking and package validation

## Status

Accepted — 2026-07-16

## Context

The SDK promises a "documented, tested, semver-disciplined public API" (master plan §1) with
1.0 freezing the surface (§5) and breaking changes only in majors (§4). That promise needs
mechanical enforcement: accidental public members, signature changes, and removed members
must be impossible to merge unnoticed, and packed packages must stay binary-compatible with
their baselines across TFMs.

Two first-party tools cover this: `Microsoft.CodeAnalysis.PublicApiAnalyzers` (tracks every
public symbol in `PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt` per project, failing
analysis when the code and the files disagree) and NuGet **package validation**
(`EnablePackageValidation`, with a baseline version) which validates packed output for
compatibility against the previous release and across TFMs.

During 0.x the API is *supposed* to change with feedback (the preview window is the point —
master plan §8), so enforcement strictness must be phased.

## Decision

We will adopt **Microsoft.CodeAnalysis.PublicApiAnalyzers** for every shipped package from
the first public preview:

- Each shipped project carries `PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt`
  (per-TFM files where the surface differs by TFM, e.g. `Koras.Dataverse.FetchXml`'s
  netstandard2.0 target).
- Every PR that changes the public surface must update the Unshipped file — making API
  changes an explicit, reviewable diff line, reviewed against
  [`../../api/public-api-review-checklist.md`](../../api/public-api-review-checklist.md).
- **Before 1.0:** the analyzer diagnostics are treated as **warnings** (still surfaced in CI
  logs and review, but not blocking) so the preview feedback loop stays fast.
- **At 1.0:** the Unshipped surface is promoted to Shipped, and the diagnostics are elevated
  to **errors**; from then on, an API change cannot compile without a deliberate file edit.
- Removal entries (`*REMOVED*`) after 1.0 are only accepted in major-version branches, per
  the SemVer policy in
  [`../../api/backward-compatibility.md`](../../api/backward-compatibility.md).

We will enable **`EnablePackageValidation`** on all shipped packages **once 1.0 ships**, with
`PackageValidationBaselineVersion` set to the previous stable release, so every pack
verifies (a) no breaking change against the baseline and (b) TFM-to-TFM compatibility within
the package. During 0.x, package validation runs without a baseline (TFM consistency checks
only), since 0.x-to-0.x breaks are permitted.

## Consequences

- The public surface becomes a reviewed artifact: `git diff` on PublicAPI files is the
  canonical answer to "what API changed in this PR?", and the changelog can be assembled
  from it.
- Accidental exposure (a forgotten `public`, a leaked implementation type) fails analysis
  instead of shipping.
- Post-1.0, binary breaks are caught twice — at compile time (analyzers) and at pack time
  (package validation) — before any release workflow can publish.
- Contributors bear a small ongoing tax (updating text files for every API change); this is
  intended friction.
- Per-TFM surface differences must be conscious: divergent files make any TFM-conditional
  API visible and reviewable (FetchXml netstandard2.0 is the expected case).
- The warning-first phase means 0.x previews rely on review discipline rather than hard
  gates — accepted, because hard-freezing a preview API would defeat the feedback window
  (master plan §8 risk: "API frozen too early").

## Alternatives considered

- **No tracking until 1.0.** Rejected: the 1.0 freeze needs an accurate inventory to freeze;
  building the habit and files from day one costs little and produces the inventory for
  free.
- **Errors from day one.** Rejected: makes every preview-phase API iteration a two-step
  chore and trains contributors to bulk-regenerate the files without reading them.
- **ApiCompat/Microsoft.DotNet.ApiCompat.Tool standalone instead of package validation.**
  Rejected as the primary gate: package validation integrates ApiCompat into `dotnet pack`
  with baseline support and no extra CI plumbing; the standalone tool remains available for
  ad-hoc investigations.
- **Approval-test snapshots of the surface (e.g., PublicApiGenerator + verified files).**
  Rejected: similar effect but runs at test time rather than compile time, without IDE
  feedback and code-fix support that PublicApiAnalyzers provides.
