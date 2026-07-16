# Guide: Console Applications

Console tools — admin utilities, data-fix scripts, environment checks — are where
`DataverseClient.Create` shines: no host, no DI container, interactive sign-in for the human
running it.

## The pattern

```csharp
using Koras.Dataverse;

var options = new DataverseClientOptions
{
    EnvironmentUrl = new Uri(args.Length > 0 ? args[0] : "https://contoso.crm.dynamics.com"),
};
options.Authentication.UseInteractive(); // browser sign-in as the person running the tool

using var dataverse = DataverseClient.Create(options);
```

`UseInteractive()` opens the system browser once; the token is cached in memory and refreshed
automatically for the process lifetime. The tool acts with the *user's* Dataverse privileges —
exactly right for admin tooling, wrong for unattended jobs (use `UseClientSecret`,
`UseCertificate`, or `UseManagedIdentity` there).

## A complete admin tool

A small utility that reports on an environment and deactivates stale accounts:

```csharp
using Koras.Dataverse;
using Koras.Dataverse.Batches;
using Koras.Dataverse.Errors;
using Koras.Dataverse.Queries;
using Microsoft.Extensions.Logging;

using ILoggerFactory loggerFactory = LoggerFactory.Create(b => b
    .AddSimpleConsole(o => o.SingleLine = true)
    .SetMinimumLevel(LogLevel.Information));

var options = new DataverseClientOptions
{
    EnvironmentUrl = new Uri("https://contoso.crm.dynamics.com"),
};
options.Authentication.UseInteractive();

using var dataverse = DataverseClient.Create(options, loggerFactory);

// Ctrl+C cancels cleanly instead of killing the process mid-request.
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
CancellationToken ct = cts.Token;

try
{
    WhoAmIResponse who = await dataverse.WhoAmIAsync(ct);
    Console.WriteLine($"Signed in as {who.UserId} (org {who.OrganizationId}).");

    // Find active accounts not modified in two years.
    DateTimeOffset cutoff = DateTimeOffset.UtcNow.AddYears(-2);
    var stale = ODataQuery.For("account")
        .Select("name", "modifiedon")
        .Where(f => f.Eq("statecode", 0).And(a => a.Lt("modifiedon", cutoff)))
        .OrderBy("modifiedon");

    var batch = new BatchRequest(); // atomic: deactivate all or none
    int found = 0;

    await foreach (Entity account in dataverse.QueryAllAsync(stale, ct))
    {
        found++;
        Console.WriteLine($"  stale: {account.GetValue<string>("name")} " +
                          $"(last modified {account.GetValue<DateTimeOffset?>("modifiedon"):yyyy-MM-dd})");

        batch.AddUpdate(new Entity("account", account.Id)
        {
            ["statecode"] = 1,  // inactive
            ["statuscode"] = 2,
        });

        if (batch.Operations.Count == BatchRequest.MaxOperations)
        {
            await dataverse.ExecuteBatchAsync(batch, ct);
            batch = new BatchRequest();
        }
    }

    if (batch.Operations.Count > 0)
    {
        await dataverse.ExecuteBatchAsync(batch, ct);
    }

    Console.WriteLine($"Deactivated {found} stale account(s).");
    return 0;
}
catch (OperationCanceledException) when (ct.IsCancellationRequested)
{
    Console.Error.WriteLine("Canceled.");
    return 130;
}
catch (DataverseException exception)
{
    Console.Error.WriteLine($"Failed: {exception.Error}");
    Console.Error.WriteLine($"Request id for support: {exception.Error.RequestId ?? "-"}");
    return 1;
}
```

## Console-specific advice

- **Dispose the client.** `Create` builds and owns its own HTTP stack; `using` releases it.
- **Pass a logger factory** (as above) to see the SDK's retry warnings
  (`Koras.Dataverse.Http`) and failure errors (`Koras.Dataverse`) in the console — invaluable
  when a script stalls because the environment is throttling.
- **One client per environment, for the whole run.** Do not create a client per operation; you
  would lose the token cache and connection pool.
- **Exit codes**: map cancellation and `DataverseException` to distinct exit codes so shell
  scripts can react.
- **Unattended variants**: swap `UseInteractive()` for `UseClientSecret(...)` (secret from an
  environment variable your script reads) or `UseManagedIdentity()` on an Azure VM/container —
  the rest of the code is unchanged.

## Related

- [Your first application](../getting-started/first-application.md) — the minimal version
- [Common scenarios](../recipes/common-scenarios.md) — batching, paging, upserts
- [Worker service guide](worker-service.md) — the hosted, long-running cousin
