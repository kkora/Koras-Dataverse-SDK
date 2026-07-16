# Cancellation

## Every call takes a token

Every I/O method on `IDataverseClient` (and the `Metadata`/`Solutions` sub-clients) accepts a
`CancellationToken`, defaulted so quick scripts can omit it. In real applications, always pass
one — the request token in ASP.NET Core, the stopping token in a `BackgroundService`:

```csharp
app.MapGet("/accounts/{id:guid}", (IDataverseClient dataverse, Guid id, CancellationToken ct) =>
    dataverse.RetrieveAsync("account", id, ColumnSet.Of("name"), ct));
```

Streaming enumerations are cancellable too, either via the method parameter or
`WithCancellation`:

```csharp
await foreach (Entity row in dataverse.QueryAllAsync(query, ct)) { /* … */ }
await foreach (Entity row in dataverse.QueryAllAsync(query).WithCancellation(ct)) { /* … */ }
```

Cancellation is cooperative and honored everywhere: token acquisition, retry backoff delays,
the HTTP request, and response reading.

## Timeout vs. caller token: linked, distinguished

Each operation runs under a linked `CancellationTokenSource` combining:

1. **your token** — "the caller no longer wants the result", and
2. **`options.Timeout`** (default 100 seconds) — "the operation ran out of its time budget",
   covering the initial attempt *and all retries*.

The SDK distinguishes which one fired:

| What fired | What you observe |
|---|---|
| Your token | `OperationCanceledException` — the original, unwrapped |
| The timeout | `DataverseException` with `Category == Timeout`, `IsTransient == true`, and the underlying `OperationCanceledException` as `InnerException` |

This split is deliberate: cancellation is *your intent* and flows as the standard .NET signal;
a timeout is a *Dataverse operation failure* and joins the normal error taxonomy so generic
`DataverseException` handling sees it.

## OperationCanceledException is never wrapped

The SDK's contract: cooperative cancellation always surfaces as `OperationCanceledException`
(or its subclass `TaskCanceledException`) — never swallowed, never wrapped in
`DataverseException`. That keeps the idiomatic patterns working:

```csharp
try
{
    await dataverse.CreateAsync(entity, stoppingToken);
}
catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
{
    // shutdown in progress — not an error; do not log as a failure
}
catch (DataverseException exception)
{
    logger.LogError(exception, "Create failed: {Category}.", exception.Category);
    throw;
}
```

Order matters as shown: catch `OperationCanceledException` first (it does not derive from
`DataverseException`), and use the `when` filter so an unexpected cancellation from elsewhere
still propagates.

## Practical guidance

- **Long operations**: solution export/import can legitimately run for minutes. Raise
  `options.Timeout` for clients used for solution work (see the
  [production configuration guide](../guides/configuration.md)) rather than suppressing
  timeouts globally.
- **Per-call budgets**: for a tighter budget on one call, link your own source —
  `CancellationTokenSource.CreateLinkedTokenSource(ct)` plus `CancelAfter(…)`. Note this
  surfaces as cancellation (your token), not as a `Timeout`-category exception.
- **Retries never outlive cancellation**: once a token fires, the retry loop stops
  immediately — no further attempts, no lingering backoff sleeps.
