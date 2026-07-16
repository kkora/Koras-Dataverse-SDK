# ADR-0007: Built-in resilience instead of a resilience dependency

## Status

Accepted — 2026-07-16

## Context

Dataverse enforces service protection limits and signals them with HTTP 429 (and 503/504
under load), including `Retry-After` headers and specific error codes (number of requests,
execution time, concurrency). "Service-protection-limit-aware resilience by default" is a
core differentiator (master plan §1, KDV-008): the SDK must retry correctly out of the box —
honoring `Retry-After`, jittered exponential backoff, bounded attempts, cancellation-aware
waits, `TimeProvider`-testable delays.

The build-vs-buy question: take a dependency on Polly (or
`Microsoft.Extensions.Http.Resilience`, which wraps Polly v8) versus implementing a focused
retry `DelegatingHandler` in the SDK.

The required behavior is narrow and Dataverse-specific: retry exactly 429/503/504 and
transient transport failures, always prefer `Retry-After`, apply service-protection
error-code awareness, count retries/throttles into the SDK's own metrics, and stay tunable
via `DataverseRetryOptions`. General-purpose resilience features (circuit breakers, hedging,
bulkheads, rate limiters) are not part of the SDK's promise.

## Decision

We will implement a **focused, built-in `RetryHandler`** in `Koras.Dataverse` — no Polly and
no `Microsoft.Extensions.Http.Resilience` dependency.

Behavior (KDV-008): retry on 429/503/504 and transient transport exceptions; always honor
`Retry-After` (delta or HTTP-date) when present; otherwise jittered exponential backoff;
bounded attempt count and delay caps from `DataverseRetryOptions`; delays via `TimeProvider`
and abortable by cancellation; retry/throttle counters fed to the SDK `Meter`
([`../observability.md`](../observability.md)).

Configuration over pluggability: the policy is tuned through options, not replaced through
interfaces. The existing extension point for user `DelegatingHandler`s via the `AddDataverse`
builder ([`../extension-model.md`](../extension-model.md)) remains, and users who want a
fully custom policy can disable built-in retries via options and apply their own resilience
around calls or in their own handler.

## Consequences

- Zero additional dependencies for every consumer; no version-conflict exposure to Polly's
  major-version transitions in consumer apps that use Polly themselves (SDK-internal policy
  can never conflict with app-level Polly usage).
- Dataverse-specific semantics (service-protection codes, `Retry-After` primacy, throttle
  metrics) are first-class rather than adapted into a generic pipeline.
- We own the correctness burden: backoff math, jitter, cancellation, and header parsing need
  the dedicated unit tests the master plan already mandates (§6: retry policy with fake
  time).
- We deliberately do not offer circuit breaking or hedging; consumers with such requirements
  add them at the application layer, where they belong for their topology.
- If future requirements genuinely outgrow the focused handler, moving to
  `Microsoft.Extensions.Http.Resilience` is an internal implementation change (the options
  surface is ours), recorded via a superseding ADR.

## Alternatives considered

- **Polly (direct dependency).** Rejected: a large general-purpose dependency for one narrow
  behavior; exposes consumers to Polly version unification issues; Dataverse-specific logic
  would still have to be written as custom strategies on top.
- **`Microsoft.Extensions.Http.Resilience`.** Rejected for the same footprint reason (it
  brings Polly v8 transitively); its standard handler retries a generic status set and would
  need customization anyway; also, its pipeline metrics would not match the SDK's contracted
  instruments.
- **No built-in resilience (document a recipe).** Rejected: abandons a headline
  differentiator; every consumer would rebuild throttling handling, the precise pain the SDK
  removes (master plan §1).
- **Pluggable policy interface in Abstractions.** Rejected: freezes an internal pipeline
  concept into the public contract and complicates the future OrganizationService transport,
  which has different retry semantics; options-based tuning covers the real cases.
