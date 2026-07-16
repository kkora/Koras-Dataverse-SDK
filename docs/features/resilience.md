# Feature Planning — KDV-008 Resilience

> Planning-level design document (pre-implementation). Catalog entry:
> [`feature-catalog.md`](feature-catalog.md#kdv-008--resilience).
> Source of truth: [`docs/planning/master-plan.md`](../planning/master-plan.md) §1, §3
> (KDV-008), §4, §5, §7, §8. Release classification: **MVP**.

## Overview

Dataverse enforces service protection limits, signaling pressure with HTTP 429/503/504 and a
`Retry-After` header. KDV-008 makes correct behavior the default: a `RetryHandler` in the HTTP
pipeline that always honors `Retry-After`, applies jittered exponential backoff otherwise,
bounds retry counts, and combines per-request timeouts with caller cancellation. Resilience
by default is a headline differentiator (master plan §1) and the top reliability mitigation in
the risk table (§8).

## Requirements

**Functional**

1. Pipeline position: `AuthenticationHandler` → **`RetryHandler`** → (user handlers) → network
   (master plan §5). Telemetry activities are emitted above the handler so they wrap retries.
2. Retry triggers: HTTP 429, 503, 504 (service protection, master plan §3), plus transient
   transport failures (connection-level `HttpRequestException`, timeouts) — final trigger list
   documented; classification consistent with KDV-009's transient flag.
3. `Retry-After` is honored **always**, in both delay-seconds and HTTP-date forms; it takes
   precedence over computed backoff (master plan §8: "Retry-After always honored").
4. Without `Retry-After`: exponential backoff with jitter, bounded by a max delay.
5. Bounded retry count; on exhaustion the final response flows to KDV-009 mapping — retries
   happen below error mapping; only post-retry failures surface (master plan §5).
6. Per-request timeout combined with the caller's `CancellationToken` via linked CTS (master
   plan §5).
7. Non-idempotency: the MVP retries the request set above uniformly because 429/503/504 for
   Dataverse indicate the operation was not processed; any deviation discovered during
   implementation is documented and tested (subject to implementation).

**Nonfunctional.** All delays via injected `TimeProvider` — fully deterministic tests (master
plan §5); `OperationCanceledException` never swallowed or wrapped; async, non-blocking waits.

## Proposed public API

Fixed by master plan §4: `DataverseRetryOptions` in `Koras.Dataverse`, configured within
`DataverseClientOptions` via `AddDataverse`. The handler itself is internal; behavior is the
contract, options are the surface.

Conservative option set, subject to implementation:

```csharp
public sealed class DataverseRetryOptions
{
    public int MaxRetries { get; set; }            // default: small, service-limit-tuned
    public TimeSpan BaseDelay { get; set; }        // backoff seed
    public TimeSpan MaxDelay { get; set; }         // backoff ceiling
    public TimeSpan RequestTimeout { get; set; }   // per-attempt or per-operation: decided
                                                   // and documented in implementation
}
```

Defaults are chosen so that **zero configuration is production-safe**; exact default values
are set during implementation against service-protection guidance and documented (no numbers
are promised here).

## Configuration

- `DataverseClientOptions.Retry` (`DataverseRetryOptions`), validated at startup (KDV-010):
  non-negative retries, positive delays, ceiling ≥ seed.
- Disabling retries (`MaxRetries = 0`) is supported for callers running their own outer
  policy — documented with a warning.
- Per-request policy overrides are **not** MVP (knob restraint; see
  [`../product/problem-statement.md`](../product/problem-statement.md)).

## Error conditions

| Condition | Behavior |
|---|---|
| Retries exhausted | Final response mapped by KDV-009; `DataverseError.Transient` remains true so outer layers can decide |
| Non-transient failure (400, 401, 403, 404 …) | Never retried; mapped immediately |
| Per-request timeout | Distinguishable timeout outcome (not conflated with caller cancellation); classified via KDV-009 |
| Caller cancellation during backoff wait or attempt | `OperationCanceledException` immediately, unwrapped; no further attempts |
| Malformed `Retry-After` | Fallback to computed backoff (documented) |

## Security

- Bounded retries prevent amplification of load or of auth failures (master plan §7).
- 401/403 are never retried (avoids hammering identity providers and masking
  misconfiguration).
- Retry logging excludes request bodies and tokens.

## Performance

- Backoff waits are async (`TimeProvider`-based delays) — no thread pool starvation under
  throttling storms.
- Jitter decorrelates retry timing across instances, preventing synchronized retry waves
  against the same environment.
- Handler adds no measurable overhead on the success path (no allocation beyond bookkeeping,
  design goal verified in benchmarks during hardening, master plan §8 milestone 8).

## Observability

- Log per retry attempt: attempt number, status/reason, chosen delay, `Retry-After` presence —
  at warning for throttling, debug otherwise (levels subject to implementation).
- Metric: retry counter tagged by trigger status (KDV-011); throttling encounters visible on
  dashboards before they become incidents.
- Activities: the operation activity (emitted above the handler) spans all attempts; attempt
  count recorded as a tag on completion (master plan §5).

## Test plan

**Unit** (fake `HttpMessageHandler` scripting response sequences + fake `TimeProvider`):
- `Retry-After` numeric and HTTP-date forms honored exactly; precedence over backoff.
- Backoff progression: exponential growth, jitter within bounds, ceiling respected.
- Retry ceiling: N failures then success succeeds; N+1 failures surfaces final error.
- Trigger matrix: 429/503/504 retried; 400/401/403/404 not; transport-exception handling.
- Linked CTS: per-request timeout fires independently of caller token; caller cancellation
  wins instantly during a backoff wait; `OperationCanceledException` unwrapped in all cases.
- Malformed `Retry-After` fallback.

**Integration** (env-var gated): sustained parallel operation burst against a real
environment completes without surfacing throttling errors to the caller (test asserts
completion, not fabricated rate numbers).

## Acceptance criteria

1. A scripted 429-with-`Retry-After` sequence delays exactly as instructed (fake-time
   verified) and then succeeds.
2. Non-transient statuses are never retried (asserted on handler invocation counts).
3. Retries are bounded; exhaustion surfaces the final failure through KDV-009 with the
   transient flag set.
4. Cancellation is immediate from any state (waiting or in-flight) and never wrapped.
5. Default configuration requires no tuning to survive an integration-level burst test.
6. All timing behavior is deterministic under fake time.
