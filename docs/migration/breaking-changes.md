# Breaking Changes

## None yet

The SDK has not shipped a stable release, so no breaking changes have been recorded. This page
is the permanent, cumulative registry; the process below applies from the first published
package.

## How breaking changes are recorded

Every breaking change gets an entry here, in the release that introduces it, with:

- **What changed** — the exact API or documented behavior, before and after.
- **Why** — the motivating problem; significant ones link to an architecture decision record
  in [docs/architecture/decision-records/](../architecture/decision-records/).
- **Migration** — the mechanical fix, with a code before/after where applicable.
- **Detection** — how the change surfaces (compile error, `[Obsolete]` warning, behavioral
  difference), so you know whether the compiler will find it for you.

Entries are grouped by release, newest first, and mirrored as a `### Breaking changes` section
in the changelog for that release.

## What counts as breaking

Anything that invalidates code or expectations built on the *documented* public API:

- binary- or source-breaking API changes (removals, signature changes, behavioral narrowing),
- changes to documented behavioral contracts: the error taxonomy's category semantics, retry
  semantics, thread-safety guarantees, or the cancellation contract
  (`OperationCanceledException` never wrapped),
- dropping a target framework or raising a minimum dependency major.

Not breaking: changes to internal types, undocumented behavior, or log message text.

## When they may happen

Per the [versioning policy](versioning-policy.md):

- **0.x previews**: breaking changes may land in minor bumps — flagged here and in the
  changelog, no deprecation window required.
- **1.0 and later**: majors only, preceded where possible by an `[Obsolete]` deprecation
  window of at least one minor release. The public API surface is guarded by
  `PublicAPI.Shipped.txt` tracking and package validation in CI, so unintended breaks fail the
  build rather than reaching a release.
