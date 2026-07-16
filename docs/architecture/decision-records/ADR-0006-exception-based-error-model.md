# ADR-0006: Exception-based error model

## Status

Accepted — 2026-07-16

## Context

The SDK promises a strong error taxonomy (KDV-009): every failure normalized into a category
(Authentication, Authorization, NotFound, Concurrency, Throttling, Validation, Timeout,
Network, Server, Unknown) with the Dataverse error code, HTTP status, request id,
`Retry-After`, and a transient flag. The open question is the delivery mechanism: thrown
exceptions carrying that structure, or functional result types (`Result<T>` /
discriminated-union style returns).

Result types have gained popularity for making failure explicit in signatures. But the SDK's
environment is the mainstream .NET service stack: `HttpClient`, `Azure.Identity`, EF Core,
ASP.NET Core — all exception-based. The SDK also leans heavily on `IAsyncEnumerable<T>`
streaming (KDV-003/KDV-004), and cancellation must surface as `OperationCanceledException`
(master plan §5).

## Decision

We will report Dataverse operation failures by **throwing `DataverseException`**, which
carries a structured, immutable **`DataverseError`** (category, Dataverse code, HTTP status,
request id, `Retry-After`, transient flag) — full design in
[`../error-model.md`](../error-model.md). We will not offer a parallel `Result<T>`-returning
API surface.

Boundary rules that make this workable:

- One exception type for Dataverse failures; callers filter with `catch … when` on
  `Error.Category` / `Error.IsTransient`, never on message text.
- `OperationCanceledException` propagates untouched; argument misuse throws standard BCL
  exceptions; neither is ever wrapped in `DataverseException`.
- Where absence is an *expected* outcome rather than a failure, the API expresses it in the
  signature (e.g., a `TryRetrieve`-style member returning null/`bool` — exact members subject
  to implementation review) instead of forcing exception-based control flow.
- Batch continue-on-error (KDV-005) returns per-item `DataverseError` values inside
  `BatchItemResult` without throwing — the result-shaped design is used exactly where partial
  failure is a normal, data-like outcome.

## Consequences

- Idiomatic integration: the SDK composes naturally with ASP.NET Core exception handling,
  Polly-style outer policies, and existing logging middleware; no adapter layer at every
  boundary.
- `IAsyncEnumerable` paging works with plain `await foreach`; a mid-stream failure throws
  from `MoveNextAsync`, which is the established platform behavior.
- The happy path stays clean (`Guid id = await client.CreateAsync(...)`) with no obligatory
  `.Value`/match ceremony, and failures cannot be silently ignored the way an unchecked
  result can.
- Callers who want railway-style flow must wrap calls themselves; the rich `DataverseError`
  makes such wrappers one-liners.
- We take on the documentation duty exceptions imply: every public member documents what it
  throws ([`../../api/public-api-design.md`](../../api/public-api-design.md)), and error-path
  tests are mandatory for every MVP feature (master plan §3).
- Exception construction cost on failure paths is accepted; failures are dominated by network
  latency, not allocation.

## Alternatives considered

- **`Result<T>` / OneOf-style returns everywhere.** Rejected: fights the platform idiom,
  doubles the surface or forces wrappers at each framework boundary, cannot express
  `IAsyncEnumerable` streaming failures cleanly, and (without language-level checking) is as
  ignorable as it is explicit.
- **Dual surface (`CreateAsync` throws + `TryCreateAsync` returns result).** Rejected: doubles
  every member, splits documentation and tests, and creates a permanent "which one is
  blessed?" question. The narrow `Try…` pattern is reserved for expected-absence cases only.
- **Status-code-first (return response objects, never throw — raw HTTP style).** Rejected:
  pushes taxonomy work onto every consumer, exactly the hand-rolled-HttpClient pain the SDK
  replaces (master plan §1).
- **Exception hierarchy (subclass per category).** Rejected: a flat type with a category enum
  keeps `catch` sites stable as the taxonomy is refined, avoids a breaking change every time
  a category is added, and serializes/logs more uniformly. `catch … when` filters give the
  same selectivity.
