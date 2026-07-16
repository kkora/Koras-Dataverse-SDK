using Koras.Dataverse;
using Koras.Dataverse.Batches;
using Koras.Dataverse.Errors;
using Koras.Dataverse.Queries;

namespace Koras.Dataverse.Samples.WorkerService;

/// <summary>
/// Streams all active accounts missing a description and back-fills them in batches of 100.
/// Demonstrates IAsyncEnumerable paging, batching, throttling-friendly patterns, and shutdown
/// cooperation via the stopping token.
/// </summary>
public sealed class AccountSweepWorker(IDataverseClient dataverse, ILogger<AccountSweepWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var query = ODataQuery.For("account")
                .Select("name")
                .Where(f => f.Eq("statecode", 0).IsNull("description"))
                .PageSize(500);

            var batch = new BatchRequest();
            int updated = 0;

            await foreach (Entity account in dataverse.QueryAllAsync(query, stoppingToken))
            {
                batch.AddUpdate(new Entity("account", account.Id)
                {
                    ["description"] = $"Reviewed by AccountSweepWorker on {DateTimeOffset.UtcNow:yyyy-MM-dd}.",
                });

                if (batch.Operations.Count == 100)
                {
                    await dataverse.ExecuteBatchAsync(batch, stoppingToken);
                    updated += batch.Operations.Count;
                    logger.LogInformation("Updated {Count} accounts so far.", updated);
                    batch = new BatchRequest();
                }
            }

            if (batch.Operations.Count > 0)
            {
                await dataverse.ExecuteBatchAsync(batch, stoppingToken);
                updated += batch.Operations.Count;
            }

            logger.LogInformation("Sweep complete: {Count} accounts updated.", updated);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Sweep canceled during shutdown.");
        }
        catch (DataverseException exception)
        {
            logger.LogError(exception,
                "Sweep failed: {Category} (HTTP {Status}, request {RequestId}).",
                exception.Category, exception.Error.HttpStatusCode, exception.Error.RequestId);
        }
    }
}
