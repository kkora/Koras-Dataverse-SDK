# Error Model

> Elaborates KDV-009 and §5 of [`docs/planning/master-plan.md`](../planning/master-plan.md).
> If this document and the master plan disagree, the master plan wins. The decision to use
> exceptions rather than result types is recorded in
> [ADR-0006](decision-records/ADR-0006-exception-based-error-model.md).

All types described here live in `Koras.Dataverse.Abstractions`, namespace
`Koras.Dataverse.Errors`, so consumers can catch and inspect failures without referencing the
implementation package.

## 1. Design

A failed Dataverse operation surfaces as a single exception type, `DataverseException`,
carrying a structured `DataverseError`:

```csharp
namespace Koras.Dataverse.Errors;

public enum DataverseErrorCategory
{
    Unknown = 0,
    Authentication,
    Authorization,
    NotFound,
    Concurrency,
    Throttling,
    Validation,
    Timeout,
    Network,
    Server,
}

public sealed record DataverseError
{
    public DataverseErrorCategory Category { get; init; }
    public string? DataverseErrorCode { get; init; }   // e.g. "0x80040333", hex code from the OData payload
    public int? HttpStatusCode { get; init; }          // null when no HTTP response was received
    public string Message { get; init; }               // normalized, safe-to-log message
    public string? RequestId { get; init; }            // Dataverse x-ms-service-request-id / req_id
    public TimeSpan? RetryAfter { get; init; }         // parsed Retry-After, when the service sent one
    public bool IsTransient { get; init; }             // true → retrying the same call may succeed
}

public sealed class DataverseException : Exception
{
    public DataverseError Error { get; }
    // Convenience pass-throughs: Category, IsTransient, RequestId.
}
```

Notes:

- `HttpStatusCode` is an `int?`, not `System.Net.HttpStatusCode`, keeping `Abstractions` free
  of any pull toward HTTP types on its surface (the enum is BCL, but the plain int also covers
  non-standard codes verbatim).
- `Message` is normalized from the OData error payload and never contains row data or secrets;
  it is safe to log.
- Exact constructor shapes and any additional convenience members are subject to
  implementation review; the members above are the contract implied by the master plan
  (category, Dataverse code, HTTP status, request id, transient flag) plus `RetryAfter`,
  which the plan's resilience feature (KDV-008) requires surfacing.

## 2. Category mapping

The mapping runs in the core client after the retry handler has given up (or for non-retryable
statuses, immediately). Dataverse error codes, when present in the OData payload, refine the
HTTP-status-based category; the HTTP status is the fallback.

| Signal | Category | IsTransient |
|---|---|---|
| HTTP 401 | Authentication | No |
| Token acquisition failure from the provider (no HTTP response) | Authentication | No¹ |
| HTTP 403 (except service-protection code below) | Authorization | No |
| HTTP 403 + Dataverse code `0x80072322` (number-of-requests limit) | Throttling | Yes |
| HTTP 404 | NotFound | No |
| HTTP 412 (`If-Match`/`If-None-Match` failed, e.g. code `0x80060882` / `ConcurrencyVersionMismatch`) | Concurrency | No |
| HTTP 429 | Throttling | Yes |
| HTTP 400 (malformed query, invalid attribute, bad payload) | Validation | No |
| HTTP 405, 413, 501 | Validation | No |
| HTTP 408 from service | Timeout | Yes |
| Per-request timeout elapsed (linked CTS fired; caller's token **not** canceled) | Timeout | Yes |
| `HttpRequestException` / DNS / TLS / connection reset (no response) | Network | Yes |
| HTTP 503, 504 | Server² | Yes |
| HTTP 500, 502, other 5xx | Server | 500: No³ · others: Yes |
| Anything unmapped | Unknown | No |

¹ Except where the underlying credential signals a transient condition (e.g., IMDS
availability); the default provider maps those to `Authentication` with `IsTransient = true`.
² 503/504 are also part of Dataverse service-protection signaling; the retry handler treats
them like throttling for backoff purposes (KDV-008), but the surfaced category reflects the
server-side nature of the status. When the payload carries an explicit service-protection
error code (`0x80072326` execution-time limit, `0x80072321` concurrency limit), the category
is `Throttling`.
³ HTTP 500 defaults to non-transient because blind retries of generic server faults can
duplicate side effects; a payload code known to be transient can override this.

Rules of precedence:

1. Caller cancellation is never mapped: if the caller's `CancellationToken` is canceled, the
   SDK lets `OperationCanceledException` propagate untouched (master plan §5).
2. Known Dataverse error code beats HTTP status.
3. HTTP status beats transport heuristics.
4. Unmapped → `Unknown`, `IsTransient = false` (fail safe: do not encourage retry loops on
   unclassified failures).

The specific set of recognized Dataverse error codes is expected to grow during
implementation against the live Web API; additions refine categories within this table's rules
and are documented in the changelog. The category *semantics* are frozen contract; the
code-level refinements are subject to implementation review.

## 3. Interaction with retry (KDV-008 / ADR-0007)

- The retry handler sits **below** error mapping: it retries 429/503/504 responses and
  transient transport failures with `Retry-After` honored and jittered exponential backoff.
  Only after retries are exhausted (or for non-retryable failures) does the response reach the
  mapping layer and become a `DataverseException`.
- Consequently, a thrown `Throttling`/`Server` exception means "already retried per policy" —
  callers should not immediately retry in a tight loop. `Error.RetryAfter` carries the
  service's last hint for callers that schedule their own deferred retry.
- `IsTransient` is advice for *outer* resilience layers (queues, jobs): re-running the whole
  operation later may succeed. It is not an instruction that the SDK will retry again.
- Retries are attempt-scoped; the single `dataverse.execute` activity spans all attempts
  (see [`observability.md`](observability.md)), and the retry counter metric increments per
  retry attempt.

## 4. What callers should catch

```csharp
try
{
    var id = await dataverse.CreateAsync(account, ct);
}
catch (DataverseException ex) when (ex.Error.Category == DataverseErrorCategory.Validation)
{
    // Bad input — fix the request; do not retry.
}
catch (DataverseException ex) when (ex.Error.IsTransient)
{
    // Optionally reschedule; honor ex.Error.RetryAfter if present.
}
catch (OperationCanceledException)
{
    // Cooperative shutdown — not a Dataverse failure. Let it flow in most code.
}
```

Guidance:

- Catch `DataverseException` — it is the only exception type the SDK throws for Dataverse
  operation failures. Filter on `Error.Category` / `IsTransient`, not on message text.
- Do **not** catch `OperationCanceledException` except at top-level orchestration; the SDK
  never wraps it.
- Argument misuse (`ArgumentNullException`, `ArgumentException` from builders and guard
  clauses) and `ObjectDisposedException` remain ordinary BCL exceptions — they indicate bugs,
  not runtime Dataverse failures, and are not wrapped.
- Batch specifics (KDV-005): in continue-on-error mode, per-item failures are reported as
  `BatchItemResult` entries carrying a `DataverseError` without throwing; the batch call
  itself throws only if the `$batch` request as a whole fails. An atomic change set failure
  throws with the error of the failing item.

## 5. Why exceptions over Result types (ADR-0006 summary)

- The SDK's callers are .NET applications where the platform idiom — `HttpClient`,
  `Azure.Identity`, EF Core — is exceptions; a `Result<T>` surface would force adapter code at
  every boundary and double the API shape (`CreateAsync` vs `TryCreateAsync`).
- `IAsyncEnumerable` paging cannot express per-`MoveNextAsync` result types without abandoning
  `await foreach`.
- The rich, structured `DataverseError` payload delivers the real benefit result types promise
  (typed, inspectable failure data) while keeping the happy path clean.
- Expected-absence lookups are handled by API design instead: dedicated `TryRetrieve`-style
  members returning null/`bool` are considered where absence is a normal outcome (subject to
  implementation review), rather than making every failure a value.

Full context and alternatives:
[ADR-0006](decision-records/ADR-0006-exception-based-error-model.md).
