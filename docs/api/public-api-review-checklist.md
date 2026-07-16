# Public API Review Checklist

> For PR reviewers. Use this checklist on every PR whose diff touches a
> `PublicAPI.Unshipped.txt` file or otherwise changes the public surface of a shipped
> package. Canonical sources: master plan §4,
> [`public-api-design.md`](public-api-design.md),
> [`naming-guidelines.md`](naming-guidelines.md),
> [`backward-compatibility.md`](backward-compatibility.md),
> [`../architecture/package-boundaries.md`](../architecture/package-boundaries.md).

Copy the relevant sections into the PR review or link this file and note exceptions
explicitly. "N/A" is an acceptable answer; silence is not.

## 1. Scope and intent

- [ ] The PR description states *why* the surface changes and names the feature ID
      (KDV-xxx) or ADR that motivates it.
- [ ] The change does not invent API beyond master plan §4 / the approved design doc; if it
      extends the plan, the plan/design doc is updated in the same PR or an ADR is included.
- [ ] Every new symbol appears in `PublicAPI.Unshipped.txt` (never hand-edited into
      `Shipped` pre-release), and nothing appears there that the PR does not intend to ship.
- [ ] No accidental exposure: no `public` type/member that exists only for internal use
      (would `internal` do?).

## 2. Placement and boundaries

- [ ] The type lives in the correct package per
      [`package-boundaries.md`](../architecture/package-boundaries.md) and the correct
      namespace per [`naming-guidelines.md`](naming-guidelines.md) §7.
- [ ] Nothing added to `Koras.Dataverse.Abstractions` references HTTP types, Azure types,
      Microsoft.Extensions types, or any third-party type.
- [ ] Nothing added to `Koras.Dataverse.FetchXml` adds a dependency or breaks the
      netstandard2.0 build; its public surface stays identical across its TFMs.
- [ ] New dependency? The seven-point assessment from
      [`dependency-rules.md`](../architecture/dependency-rules.md) §2 is in the PR
      description. (In Abstractions/FetchXml: reject; requires an ADR.)
- [ ] No third-party type leaks into public signatures beyond the documented allowances
      (`TokenCredential` in core auth options only).

## 3. Naming

- [ ] Async return type ⇔ `Async` suffix, both directions.
- [ ] Vocabulary: `tableName` for table logical names in data APIs; `logicalName` in
      metadata APIs; columns vs attributes per naming guidelines §3.
- [ ] Options types end in `Options`; configuration selectors use `Use*`; no `Get` prefix
      on properties; `Try*` members never throw on the miss case.
- [ ] Builder members use the fixed verb set (`For`/`Select`/`Where`/`OrderBy`/`Top`/
      `Link`/`Expand`/`Build`); chainables return the builder.
- [ ] US English; acronym casing per guidelines §1.

## 4. Shape and modifiers

- [ ] Type is `sealed` (or `abstract`/`static` deliberately); an unsealed concrete type has
      a written justification.
- [ ] Immutable models are records (or otherwise immutable); mutable types are mutable for
      a reason (builder/options/Entity-under-construction).
- [ ] Constructor visibility is intentional: no public constructors on types consumers
      should obtain via DI or factories.
- [ ] No public statics except pure builder entry points and DI extension classes.
- [ ] No positional `bool` parameter that switches behavior (enum/options/second method
      instead); optional named-flag exceptions are documented in the design doc.
- [ ] No new overload set that creates ambiguity traps (same arity, convertible parameter
      types).
- [ ] Interfaces: no members added to an already-shipped interface (breaking — majors
      only); new capability goes on a new interface or the implementing class.

## 5. Async, cancellation, threading

- [ ] Every I/O member is async-only; no sync counterpart added; no sync-over-async inside.
- [ ] `CancellationToken cancellationToken = default` is the last parameter of every I/O
      member; `IAsyncEnumerable<T>` members use `[EnumeratorCancellation]`.
- [ ] `OperationCanceledException` is never caught-and-wrapped by the new code path.
- [ ] Thread-safety class of the new type is stated in XML docs (service / immutable /
      builder) and matches the model in
      [`overview.md`](../architecture/overview.md) §3.

## 6. Nullability and validation

- [ ] Nullable reference annotations are complete and honest (no `!` suppressions on the
      public surface; `?` only where null is a real, documented state).
- [ ] Arguments are validated eagerly (before any I/O or deferred enumeration) with the
      standard BCL exceptions; async methods validate before the first await (or in a
      wrapper so the throw is synchronous).
- [ ] New options properties carry DataAnnotations where startup validation applies, and
      defaults are stated in XML docs.

## 7. Errors and behavior contract

- [ ] Dataverse failures surface as `DataverseException` with a correctly categorized
      `DataverseError`; any new mapping follows the table in
      [`error-model.md`](../architecture/error-model.md) (new Dataverse codes refine, never
      re-shuffle, categories).
- [ ] XML docs list every exception type the member throws and under what condition.
- [ ] Expected-absence cases use the `Try*` pattern instead of throwing `NotFound`.
- [ ] Behavior that becomes contract (defaults, category outcomes) is added to the
      documentation in the same PR.

## 8. Compatibility

- [ ] Post-1.0: the change is additive (minor) or scheduled for a major; removals follow
      the obsoletion policy ([`backward-compatibility.md`](backward-compatibility.md) §4).
- [ ] No enum value reordering/renumbering; new enum members are appended.
- [ ] No default-parameter-value change on an existing member (add an overload instead).
- [ ] Parameter names unchanged on existing members (named-argument compatibility).
- [ ] Package validation and PublicAPI analyzers pass; per-TFM surface differences are
      intentional and reviewed.

## 9. Observability and security

- [ ] New telemetry uses the existing source/meter and documented tag names; no new
      identifier without an observability-doc update; no row data, attribute values, query
      literals, or tokens in any log/span/metric the new code emits.
- [ ] Secrets never appear in exception messages, `ToString()` output, or debugger
      displays of new types.
- [ ] Any user-influenced value placed into a URL, OData literal, or XML document goes
      through the SDK's encoders.

## 10. Documentation and tests

- [ ] XML IntelliSense docs on every new public symbol (summary, params, returns,
      exceptions, thread-safety note).
- [ ] The relevant docs page(s) under `docs/` are updated in the same PR
      (design doc for new members; error model for new mappings; observability for new
      telemetry).
- [ ] Tests per master plan §3 gate: unit tests, error-path tests, cancellation tests for
      every new I/O member; builder/encoding tests for new builder members.
- [ ] A runnable sample or snippet exists for any new user-facing capability.
- [ ] Changelog entry drafted, tagged with the feature ID.
