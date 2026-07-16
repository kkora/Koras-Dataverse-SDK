# Guide: ASP.NET Core (Controllers)

Using the SDK from a controller-based Web API: registration in `Program.cs`, injection into
controllers, and a health endpoint.

## Program.cs

```csharp
using Koras.Dataverse;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddDataverse(options =>
{
    options.EnvironmentUrl = new Uri(builder.Configuration["Dataverse:EnvironmentUrl"]!);
    options.Authentication.UseManagedIdentity(); // or UseClientSecret from configuration in dev
});

builder.Services.AddHealthChecks()
    .AddDataverseHealthCheck(tags: new[] { "ready" });

var app = builder.Build();

app.MapControllers();

// Liveness: process is up — no dependency probes.
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false,
});

// Readiness: includes the Dataverse WhoAmI probe.
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
});

app.Run();
```

## A controller

Inject `IDataverseClient` (a singleton — safe from any number of concurrent requests) and pass
the request's `CancellationToken` to every call:

```csharp
using Koras.Dataverse;
using Koras.Dataverse.Errors;
using Koras.Dataverse.Queries;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/accounts")]
public sealed class AccountsController(IDataverseClient dataverse) : ControllerBase
{
    public sealed record AccountDto(Guid Id, string? Name, decimal? Revenue);
    public sealed record CreateAccountRequest(string Name, decimal? Revenue);

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AccountDto>>> List(CancellationToken ct)
    {
        var query = ODataQuery.For("account")
            .Select("name", "revenue")
            .Where(f => f.Eq("statecode", 0))
            .OrderBy("name")
            .Top(50);

        DataverseQueryResult page = await dataverse.QueryAsync(query, ct);
        return Ok(page.Entities
            .Select(e => new AccountDto(e.Id, e.GetValue<string>("name"), e.GetValue<decimal?>("revenue")))
            .ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AccountDto>> Get(Guid id, CancellationToken ct)
    {
        try
        {
            Entity account = await dataverse.RetrieveAsync("account", id, ColumnSet.Of("name", "revenue"), ct);
            return Ok(new AccountDto(account.Id, account.GetValue<string>("name"), account.GetValue<decimal?>("revenue")));
        }
        catch (DataverseException exception) when (exception.Category == DataverseErrorCategory.NotFound)
        {
            return NotFound();
        }
    }

    [HttpPost]
    public async Task<ActionResult<AccountDto>> Create(CreateAccountRequest request, CancellationToken ct)
    {
        var entity = new Entity("account")
        {
            ["name"] = request.Name,
            ["revenue"] = request.Revenue,
        };

        Guid id = await dataverse.CreateAsync(entity, ct);
        return CreatedAtAction(nameof(Get), new { id }, new AccountDto(id, request.Name, request.Revenue));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try
        {
            await dataverse.DeleteAsync("account", id, ct);
            return NoContent();
        }
        catch (DataverseException exception) when (exception.Category == DataverseErrorCategory.NotFound)
        {
            return NotFound();
        }
    }
}
```

## Mapping Dataverse errors to HTTP responses

A small exception filter keeps controllers clean:

```csharp
using Koras.Dataverse.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

public sealed class DataverseExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not DataverseException exception)
        {
            return;
        }

        int status = exception.Category switch
        {
            DataverseErrorCategory.NotFound => StatusCodes.Status404NotFound,
            DataverseErrorCategory.Validation => StatusCodes.Status400BadRequest,
            DataverseErrorCategory.Concurrency => StatusCodes.Status409Conflict,
            DataverseErrorCategory.Throttling => StatusCodes.Status503ServiceUnavailable,
            DataverseErrorCategory.Timeout or DataverseErrorCategory.Network => StatusCodes.Status504GatewayTimeout,
            _ => StatusCodes.Status502BadGateway,
        };

        context.Result = new ObjectResult(new ProblemDetails
        {
            Status = status,
            Title = $"Dataverse: {exception.Category}",
            Extensions = { ["dataverseRequestId"] = exception.Error.RequestId },
        })
        { StatusCode = status };
        context.ExceptionHandled = true;
    }
}
```

Register it with `builder.Services.AddControllers(o => o.Filters.Add<DataverseExceptionFilter>());`.
Never echo `exception.Message` verbatim to external callers — it can contain table and column
names; the request id is safe and enough for correlation.

## Notes

- Cancellation: ASP.NET Core cancels the request token when the client disconnects; the SDK
  call stops promptly and surfaces `OperationCanceledException` — do not log those as errors.
- Timeouts: the default 100-second operation budget usually exceeds a sensible web request
  budget; consider a lower `options.Timeout` for interactive APIs.
- Health endpoints: see the [health checks guide](health-checks.md) for readiness vs. liveness
  reasoning.

## Related

- [Minimal API guide](minimal-api.md) — the same app in minimal-API style
- [Telemetry guide](telemetry.md) — traces for each Dataverse call inside your request spans
