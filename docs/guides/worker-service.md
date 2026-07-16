# Guide: Worker Services

A hosted `BackgroundService` that periodically syncs Dataverse rows to another store, using
`QueryAllAsync` for paged reads and `BatchRequest` for chunked writes back.

## Program.cs

```csharp
using Koras.Dataverse;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDataverse(options =>
{
    options.EnvironmentUrl = new Uri(builder.Configuration["Dataverse:EnvironmentUrl"]!);
    options.Authentication.UseManagedIdentity();

    // Background work tolerates longer waits; let the SDK absorb more throttling.
    options.Retry.MaxRetries = 5;
    options.Retry.MaxDelay = TimeSpan.FromMinutes(1);
});

builder.Services.AddHostedService<AccountSyncWorker>();

builder.Build().Run();
```

## The worker

```csharp
using Koras.Dataverse;
using Koras.Dataverse.Batches;
using Koras.Dataverse.Errors;
using Koras.Dataverse.Queries;

public sealed class AccountSyncWorker(
    IDataverseClient dataverse,
    ILogger<AccountSyncWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);
    private const int BatchSize = 500;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                await SyncOnceAsync(stoppingToken);
            }
            catch (DataverseException exception) when (exception.IsTransient)
            {
                // The SDK already retried; back off until the next tick.
                logger.LogWarning(exception,
                    "Sync hit a transient Dataverse failure ({Category}); will retry next cycle.",
                    exception.Category);
            }
            catch (DataverseException exception)
            {
                // Non-transient: configuration/permissions/data problem. Log and keep the
                // service alive so operators can fix the cause without a crash loop.
                logger.LogError(exception,
                    "Sync failed ({Category}, request {RequestId}).",
                    exception.Category, exception.Error.RequestId);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task SyncOnceAsync(CancellationToken ct)
    {
        // 1. Stream every active account, page by page. QueryAllAsync follows
        //    @odata.nextLink automatically; PageSize controls rows per round-trip.
        var query = ODataQuery.For("account")
            .Select("name", "revenue", "modifiedon")
            .Where(f => f.Eq("statecode", 0))
            .PageSize(1000);

        int scanned = 0;
        var pending = new BatchRequest { Atomic = false }; // independent ops, continue on error

        await foreach (Entity account in dataverse.QueryAllAsync(query, ct))
        {
            scanned++;

            // …push the row to the downstream system here…

            // 2. Write a sync marker back, batched in chunks.
            pending.AddUpdate(new Entity("account", account.Id)
            {
                ["description"] = $"synced {DateTimeOffset.UtcNow:O}",
            });

            if (pending.Operations.Count == BatchSize)
            {
                await FlushAsync(pending, ct);
                pending = new BatchRequest { Atomic = false };
            }
        }

        if (pending.Operations.Count > 0)
        {
            await FlushAsync(pending, ct);
        }

        logger.LogInformation("Sync cycle complete: {Scanned} account(s) processed.", scanned);
    }

    private async Task FlushAsync(BatchRequest batch, CancellationToken ct)
    {
        BatchResponse response = await dataverse.ExecuteBatchAsync(batch, ct);
        foreach (BatchItemResult item in response.Results.Where(r => !r.Succeeded))
        {
            logger.LogWarning("Batch item {Index} failed: {Error}.", item.Index, item.Error);
        }
    }
}
```

## Design notes

- **`stoppingToken` everywhere.** Host shutdown cancels the token; the in-flight page fetch or
  batch stops promptly and surfaces `OperationCanceledException`, which `BackgroundService`
  handles as normal shutdown.
- **`Atomic = false` for bulk maintenance writes.** One bad row should not roll back 499 good
  ones; failed items are reported per item instead of throwing. Use the default atomic mode
  when the operations genuinely belong together.
- **Chunk below the 1,000-op limit.** `BatchRequest` refuses to grow past
  `BatchRequest.MaxOperations`; 100–500 per batch is a practical sweet spot for payload size
  and per-item diagnostics.
- **Let the SDK absorb throttling.** Background workloads are the classic 429 victims. Higher
  `MaxRetries`/`MaxDelay` (as configured above) plus honoring `Retry-After` (default) is
  usually all you need; if a transient exception still escapes, skip the cycle rather than
  hammering the environment.
- **FetchXML variant**: for join-heavy source queries, swap the OData stream for
  `dataverse.FetchAllAsync(fetchQuery, pageSize: 5000, ct)` — same `IAsyncEnumerable<Entity>`
  consumption, paging cookies handled internally.

## Related

- [Common scenarios](../recipes/common-scenarios.md) — batch upsert recipe
- [Telemetry guide](telemetry.md) — watch `koras.dataverse.client.throttles` for this worker
- [Production configuration](../recipes/production-configuration.md)
