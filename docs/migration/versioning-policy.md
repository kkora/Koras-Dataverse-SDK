# Versioning Policy (Summary)

The full, binding policy is [docs/release/versioning.md](../release/versioning.md). The short
version:

- **SemVer 2.0**, applied to the documented public API of each package (the surface tracked in
  `PublicAPI.Shipped.txt` plus documented behavioral contracts: error taxonomy, retry
  semantics, thread-safety and cancellation guarantees).
- **All `Koras.Dataverse*` packages version together** — one version number, one changelog,
  one tag per release.
- **0.x is the preview window**: minor bumps (0.1 → 0.2) may contain breaking changes; they
  are always called out explicitly in the changelog. Patch releases within 0.x are
  non-breaking. Prerelease labels use `-preview.N`.
- **From 1.0.0**: breaking changes only in major versions; deprecations get an `[Obsolete]`
  marking at least one minor release before removal; adding a TFM is a minor, removing one is
  a major.
- Internal types and undocumented behavior carry no compatibility promise.

How breaking changes are recorded and communicated is described in
[breaking changes](breaking-changes.md); the practical upgrade steps per release live in
[upgrading](upgrading.md).
