# Error Handling

Every Dataverse failure surfaces as a single exception type — `DataverseException` — carrying a
normalized `DataverseError`. You branch on a stable **category**, not on HTTP minutiae or
provider-specific codes. Cancellation is the one exception to the rule: it always surfaces as
`OperationCanceledException`, never wrapped (see [cancellation](cancellation.md)).

## The shape

```csharp
public class DataverseException : Exception
{
    public DataverseError Error { get; }            // full normalized details
    public DataverseErrorCategory Category { get; } // shortcut for Error.Category
    public bool IsTransient { get; }                // shortcut for Error.IsTransient
}

public sealed record DataverseError
{
    public required DataverseErrorCategory Category { get; init; }
    public required string Message { get; init; }   // never contains credentials or tokens
    public int? HttpStatusCode { get; init; }
    public string? ErrorCode { get; init; }         // Dataverse hex code, e.g. "0x80040217"
    public string? RequestId { get; init; }         // x-ms-service-request-id
    public TimeSpan? RetryAfter { get; init; }      // server's retry hint, when sent
    public bool IsTransient { get; init; }
}
```

## Catch and switch on Category

```csharp
using Koras.Dataverse.Errors;

try
{
    Entity account = await dataverse.RetrieveAsync("account", id, ColumnSet.Of("name"), ct);
}
catch (DataverseException exception)
{
    switch (exception.Category)
    {
        case DataverseErrorCategory.NotFound:
            // the row (or table/resource) does not exist — often a normal business case
            return null;

        case DataverseErrorCategory.Concurrency:
            // optimistic-concurrency or duplicate conflict: re-read and reconcile
            throw;

        case DataverseErrorCategory.Throttling:
            // service protection limits; the SDK already retried MaxRetries times
            logger.LogWarning("Throttled; server asked for {RetryAfter}.", exception.Error.RetryAfter);
            throw;

        case DataverseErrorCategory.Authentication:
        case DataverseErrorCategory.Authorization:
            // configuration/permission problem — retrying will not help
            logger.LogError(exception, "Dataverse access problem ({Code}).", exception.Error.ErrorCode);
            throw;

        default:
            logger.LogError(exception,
                "Dataverse failure {Category}, request id {RequestId}.",
                exception.Category, exception.Error.RequestId);
            throw;
    }
}
```

## Category reference

| Category | Typical HTTP | Meaning | Transient? |
|---|---|---|---|
| `Authentication` | 401 | Token acquisition or use failed | No |
| `Authorization` | 403 | Authenticated but lacking privileges | No |
| `NotFound` | 404 | Row, table, or resource does not exist | No |
| `Concurrency` | 409, 412 | Optimistic-concurrency or duplicate conflict | No |
| `Throttling` | 429 | Service-protection limits hit | **Yes** |
| `Validation` | 400 | Request rejected as invalid (includes business-rule errors) | No |
| `Timeout` | 408, or client-side timeout | The operation exceeded its time budget | **Yes** |
| `Network` | — (no response) | DNS, TLS, or socket failure | **Yes** |
| `Server` | 5xx | Dataverse server-side failure | 502/503/504: yes; 500: no |
| `Unknown` | anything else | Could not be classified | No |

Categories are part of the SDK's public contract and stable across versions.

## IsTransient guidance

`IsTransient == true` means retrying the *identical* request may succeed. Keep in mind:

- The SDK has **already retried** transient HTTP failures (429/502/503/504 and network errors)
  up to `Retry.MaxRetries` before the exception reached you. A transient exception in your code
  means the failure outlasted that policy.
- Sensible app-level responses: fail the message/job and let your queue redeliver later; back
  off at a coarser granularity (minutes, not milliseconds); honor `Error.RetryAfter` when
  present.
- Do **not** wrap SDK calls in your own tight retry loop for transient errors — that multiplies
  pressure on an already-throttled environment.
- `IsTransient == false` means fix the cause: credentials, permissions, payload, or data.

## RequestId for support

`Error.RequestId` is Dataverse's `x-ms-service-request-id`. Log it on every failure — it is the
key Microsoft support uses to locate the server-side trace of the exact request. It is also
emitted as the `dataverse.request_id` tag on the failed operation's telemetry span. See
[diagnostics](../troubleshooting/diagnostics.md).

## Batch errors

Batches have split semantics (details in [core abstractions](core-abstractions.md)):

- **Atomic batch** (default): any operation failing rolls back the change set, and
  `ExecuteBatchAsync` throws a `DataverseException` carrying the first failed operation's error.
- **Non-atomic batch** (`Atomic = false`): `ExecuteBatchAsync` returns normally; inspect
  `BatchResponse.Results[i].Error` (a `DataverseError`) per item. Only a failure of the batch
  request *itself* throws.

## What is not a DataverseException

Programming errors throw standard .NET exceptions immediately, before any I/O:
`ArgumentNullException`/`ArgumentException` (null entity, missing id, invalid column name),
`InvalidOperationException` (unregistered client name, more than 1,000 batch operations),
`InvalidCastException` (`Entity.GetValue<T>` type mismatch). Don't catch `DataverseException`
expecting these.
