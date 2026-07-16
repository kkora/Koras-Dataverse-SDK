# Guide: Minimal APIs

A complete minimal-API application over Dataverse: registration, endpoints, error mapping, and
health checks in one `Program.cs`.

## The application

```bash
dotnet new web -n Contoso.AccountsApi
cd Contoso.AccountsApi
dotnet add package Koras.Dataverse
dotnet user-secrets init
dotnet user-secrets set "Dataverse:ClientSecret" "<secret>"
```

`appsettings.json`:

```json
{
  "Dataverse": {
    "EnvironmentUrl": "https://contoso.crm.dynamics.com",
    "TenantId": "11111111-1111-1111-1111-111111111111",
    "ClientId": "22222222-2222-2222-2222-222222222222"
  }
}
```

`Program.cs`:

```csharp
using Koras.Dataverse;
using Koras.Dataverse.Errors;
using Koras.Dataverse.Queries;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

IConfigurationSection config = builder.Configuration.GetSection("Dataverse");

builder.Services.AddDataverse(options =>
{
    options.EnvironmentUrl = new Uri(config["EnvironmentUrl"]!);
    options.Timeout = TimeSpan.FromSeconds(30); // interactive API: keep the budget tight

    if (builder.Environment.IsDevelopment())
    {
        options.Authentication.UseClientSecret(
            config["TenantId"]!, config["ClientId"]!, config["ClientSecret"]!);
    }
    else
    {
        options.Authentication.UseManagedIdentity();
    }
});

builder.Services.AddHealthChecks()
    .AddDataverseHealthCheck(tags: new[] { "ready" });

var app = builder.Build();

RouteGroupBuilder accounts = app.MapGroup("/accounts");

accounts.MapGet("/", async (IDataverseClient dataverse, CancellationToken ct) =>
{
    var query = ODataQuery.For("account")
        .Select("name", "revenue")
        .Where(f => f.Eq("statecode", 0))
        .OrderBy("name")
        .Top(50);

    DataverseQueryResult page = await dataverse.QueryAsync(query, ct);
    return Results.Ok(page.Entities.Select(e => new
    {
        e.Id,
        Name = e.GetValue<string>("name"),
        Revenue = e.GetValue<decimal?>("revenue"),
    }));
});

accounts.MapGet("/{id:guid}", async (Guid id, IDataverseClient dataverse, CancellationToken ct) =>
{
    Entity account = await dataverse.RetrieveAsync("account", id, ColumnSet.Of("name", "revenue"), ct);
    return Results.Ok(new
    {
        account.Id,
        Name = account.GetValue<string>("name"),
        Revenue = account.GetValue<decimal?>("revenue"),
    });
});

accounts.MapPost("/", async (CreateAccount request, IDataverseClient dataverse, CancellationToken ct) =>
{
    var entity = new Entity("account")
    {
        ["name"] = request.Name,
        ["revenue"] = request.Revenue,
    };
    Guid id = await dataverse.CreateAsync(entity, ct);
    return Results.Created($"/accounts/{id}", new { id });
});

accounts.MapDelete("/{id:guid}", async (Guid id, IDataverseClient dataverse, CancellationToken ct) =>
{
    await dataverse.DeleteAsync("account", id, ct);
    return Results.NoContent();
});

// Map Dataverse failures onto HTTP problem responses in one place.
app.UseExceptionHandler(handler => handler.Run(async context =>
{
    Exception? exception = context.Features
        .Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;

    if (exception is DataverseException dataverseException)
    {
        int status = dataverseException.Category switch
        {
            DataverseErrorCategory.NotFound => StatusCodes.Status404NotFound,
            DataverseErrorCategory.Validation => StatusCodes.Status400BadRequest,
            DataverseErrorCategory.Concurrency => StatusCodes.Status409Conflict,
            DataverseErrorCategory.Throttling => StatusCodes.Status503ServiceUnavailable,
            DataverseErrorCategory.Timeout or DataverseErrorCategory.Network => StatusCodes.Status504GatewayTimeout,
            _ => StatusCodes.Status502BadGateway,
        };

        await Results.Problem(
            title: $"Dataverse: {dataverseException.Category}",
            statusCode: status,
            extensions: new Dictionary<string, object?>
            {
                ["dataverseRequestId"] = dataverseException.Error.RequestId,
            }).ExecuteAsync(context);
        return;
    }

    await Results.Problem(statusCode: StatusCodes.Status500InternalServerError).ExecuteAsync(context);
}));

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
});

app.Run();

public sealed record CreateAccount(string Name, decimal? Revenue);
```

## Try it

```bash
dotnet run
curl -X POST http://localhost:5000/accounts \
     -H "Content-Type: application/json" \
     -d '{"name":"Contoso Ltd","revenue":25000}'
curl http://localhost:5000/accounts
curl http://localhost:5000/health/ready
```

## Points worth noticing

- The lambda parameters `IDataverseClient dataverse, CancellationToken ct` are resolved by
  minimal-API binding — no extra code; the token is the request's abort token.
- The environment switch (`UseClientSecret` in dev, `UseManagedIdentity` in prod) lives in one
  place. Only the credential changes; nothing downstream cares.
- The `UseExceptionHandler` block is the minimal-API twin of the controller filter in the
  [ASP.NET Core guide](aspnet-core.md): stable HTTP mapping, request id exposed for
  correlation, raw Dataverse messages kept out of responses.

## Related

- [Health checks guide](health-checks.md)
- [Telemetry guide](telemetry.md) — add OpenTelemetry to this Program.cs
- [Production configuration](../recipes/production-configuration.md)
