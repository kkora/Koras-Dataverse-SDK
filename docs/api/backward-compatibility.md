# Backward Compatibility Policy

> Elaborates the compatibility strategy of §4 and the versioning line of §5 in
> [`docs/planning/master-plan.md`](../planning/master-plan.md). If this document and the
> master plan disagree, the master plan wins. Enforcement mechanics are decided in
> [ADR-0010](../architecture/decision-records/ADR-0010-public-api-tracking.md).

## 1. What is public contract

The following are covered by the compatibility promise once shipped in a stable release:

- **Types and members.** Every `public` type and member in the shipped packages — names,
  signatures, parameter names (they are contract for named-argument callers), return types,
  `sealed`/`abstract` modifiers, and enum member *values* (e.g., `DataverseErrorCategory`
  numeric values).
- **Nullability annotations.** Tightening an input to non-nullable or loosening an output to
  nullable is a source-breaking change and treated as breaking.
- **Documented behavior of the error model.** The category semantics and mapping rules in
  [`../architecture/error-model.md`](../architecture/error-model.md): which category a
  documented condition produces, the `IsTransient` promise, `OperationCanceledException`
  never being wrapped. Adding recognition of *new* Dataverse error codes that refines
  `Unknown`/status-based mapping into a more specific category is a compatible,
  changelog-documented improvement; moving a documented condition between categories is
  breaking.
- **Options defaults.** Default values of shipped options (retry attempts, timeout, etc.)
  are contract: changing a default changes runtime behavior of existing apps and is treated
  as at least minor-with-prominent-changelog, breaking if it can affect correctness
  (e.g., disabling retries).
- **Telemetry identifiers.** ActivitySource/Meter name `Koras.Dataverse`, span name
  `dataverse.execute`, instrument names `koras.dataverse.client.*`, and the documented tag
  *names* — dashboards and alerts are built on these. Logger category names
  `Koras.Dataverse` / `Koras.Dataverse.Http` likewise.
- **Package identity and layout.** Package IDs, TFM list, and the dependency-freedom of
  `Abstractions`/`FetchXml` (zero third-party dependencies is itself a promise).
- **Exception contract.** Which exception types a documented failure produces
  (`DataverseException` for Dataverse failures, BCL argument exceptions for misuse).

## 2. What is not contract

May change in any release without notice (do not build on these):

- **Log text.** Message templates, wording, event ids, and level assignments of individual
  log events. Parse nothing from logs; use the structured telemetry.
- **Experimental telemetry tag values.** Tag *names* are contract; tag *value sets* marked
  experimental in [`../architecture/observability.md`](../architecture/observability.md)
  (e.g., the exact `dataverse.operation` value list before it is declared final, any tag
  added after 1.0 and explicitly marked experimental) may be refined.
- **Internal and non-public surface.** `internal` types, private members, implementation
  namespaces without public types, and anything reachable only via reflection.
- **Wire-level details.** Exact HTTP headers, payload formatting, `$batch` boundary
  strings, retry timing jitter — observable but not contractual.
- **Exception messages** and `DataverseError.Message` wording (the *fields* of
  `DataverseError` are contract; the human-readable text is not).
- **0.x preview surface.** Anything shipped only in `0.x` versions (see §3).

## 3. SemVer rules

SemVer 2.0 (master plan §5):

- **0.x (preview).** The API may change in any release; `0.1.0-preview.*` exists precisely
  to gather feedback. Breaking changes are still documented in the changelog and flagged in
  release notes.
- **1.0.0.** Freezes the public API. `PublicAPI.Unshipped.txt` is promoted to
  `Shipped`, analyzers escalate to errors, and package validation gains a baseline
  (ADR-0010).
- **Patch (`x.y.Z`).** Bug fixes only; no public surface change, no default changes, no new
  dependencies.
- **Minor (`x.Y.0`).** Additive only: new types, new members, new optional parameters are
  *not* added to interfaces or existing method signatures (both are breaking — see §5); new
  functionality arrives as new members/overloads on classes, new interfaces, or extension
  methods. Default-value changes only when behaviorally safe and prominently documented.
- **Major (`X.0.0`).** The only place for breaking changes: removals (after obsoletion, §4),
  signature changes, behavior changes to documented contracts, TFM drops, dependency-floor
  raises that are breaking in practice.
- **Interfaces.** Because `IDataverseClient` etc. are implemented by consumer mocks, adding
  members to an existing shipped interface is a breaking change and happens only in majors.
  Growth points are designed in from the start (sub-client properties, options) to keep
  minors additive. (Default interface members are not used: they complicate the
  netstandard2.0-adjacent story and mock behavior.)

## 4. Obsoletion policy

- A member slated for removal is marked `[Obsolete]` with a message naming the replacement
  and the planned removal major, **at least one minor release** before the major that
  removes it (master plan §4).
- Obsoletions start as warnings; a major may escalate to `error: true` one release before
  removal when feasible.
- The changelog lists every new obsoletion under a dedicated heading; the XML docs of the
  obsolete member link the migration note.
- Obsolete members keep working (behavior frozen) until removed.

## 5. Binary vs source compatibility stance

- **Within a major: binary compatibility is the hard promise.** An assembly compiled
  against `x.y` must load and run against `x.(y+n)` without recompilation. Package
  validation enforces this against the baseline (ADR-0010). Consequences we honor: no
  changing defaults *of parameters* (default parameter values are baked into callers —
  changing them silently is misleading; we add overloads instead), no reordering enum
  values, no adding abstract members, no moving types between assemblies without
  type-forwarders.
- **Source compatibility is a strong goal, not an absolute promise, within a major.** Rare
  acceptable source breaks: adding overloads that create ambiguity for lambda-typed
  arguments, new members that collide with a consumer's extension methods, analyzer/nullable
  annotation refinements. These are documented in release notes.
- **Behavioral compatibility.** Documented behavior (§1) follows the same major-only rule;
  undocumented observable behavior (§2) may change with changelog notice.

## 6. Enforcement

- **PublicAPI files.** `PublicAPI.Shipped.txt`/`PublicAPI.Unshipped.txt` per shipped
  project (per-TFM where surfaces differ). Every surface change is a visible diff in review;
  warnings pre-1.0, errors post-1.0 (ADR-0010).
- **Package validation.** `EnablePackageValidation` + `PackageValidationBaselineVersion`
  from 1.0: every `dotnet pack` verifies baseline and cross-TFM compatibility; the release
  workflow cannot publish a package that fails it.
- **Architecture tests** guard structural promises (dependency-freedom of
  Abstractions/FetchXml, sealed-by-default, namespace placement).
- **Review checklist.** Every public-surface PR walks
  [`public-api-review-checklist.md`](public-api-review-checklist.md).
- **Changelog discipline.** Feature IDs (KDV-xxx) and PublicAPI diffs drive the changelog;
  breaking changes and obsoletions get dedicated sections.

## 7. Support window

Per master plan §8: after 1.0, the latest major receives features and fixes; the previous
major receives security fixes. TFM support tracks the .NET support lifecycle; dropping a TFM
happens only in a major.
