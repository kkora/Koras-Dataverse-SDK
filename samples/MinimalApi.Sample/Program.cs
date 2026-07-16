// Minimal API sample: Dataverse-backed HTTP endpoints with DI, options binding, error mapping
// to ProblemDetails-style responses, and a health check. See README.md for setup.
using Koras.Dataverse;
using Koras.Dataverse.Errors;
using Koras.Dataverse.Queries;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDataverse(options =>
{
    // Configuration lives in appsettings.json / user-secrets, e.g.:
    //   "Dataverse": { "EnvironmentUrl": "https://contoso.crm.dynamics.com" }
    // Secrets (TenantId/ClientId/ClientSecret) belong in user-secrets or Key Vault.
    IConfiguration config = builder.Configuration;
    options.EnvironmentUrl = new Uri(config["Dataverse:EnvironmentUrl"]
        ?? throw new InvalidOperationException("Configure Dataverse:EnvironmentUrl."));

    string? tenantId = config["Dataverse:TenantId"];
    string? clientId = config["Dataverse:ClientId"];
    string? clientSecret = config["Dataverse:ClientSecret"];
    if (tenantId is not null && clientId is not null && clientSecret is not null)
    {
        options.Authentication.UseClientSecret(tenantId, clientId, clientSecret);
    }
    else
    {
        options.Authentication.UseDefault(); // az login / managed identity
    }
});

builder.Services.AddHealthChecks().AddDataverseHealthCheck(tags: ["ready"]);

var app = builder.Build();

app.MapHealthChecks("/health/ready");

app.MapGet("/accounts", async (IDataverseClient dataverse, int top, CancellationToken ct) =>
{
    var query = ODataQuery.For("account")
        .Select("name", "revenue", "statecode")
        .OrderBy("name")
        .Top(top is > 0 and <= 100 ? top : 10);

    DataverseQueryResult page = await dataverse.QueryAsync(query, ct);
    return Results.Ok(page.Entities.Select(a => new
    {
        Id = a.Id,
        Name = a.GetValue<string>("name"),
        Revenue = a.GetValue<decimal?>("revenue"),
    }));
});

app.MapGet("/accounts/{id:guid}", async (IDataverseClient dataverse, Guid id, CancellationToken ct) =>
{
    Entity account = await dataverse.RetrieveAsync("account", id, ColumnSet.Of("name", "revenue"), ct);
    return Results.Ok(new { account.Id, Name = account.GetValue<string>("name") });
});

app.MapPost("/accounts", async (IDataverseClient dataverse, NewAccount request, CancellationToken ct) =>
{
    var entity = new Entity("account") { ["name"] = request.Name };
    if (request.Revenue is decimal revenue)
    {
        entity["revenue"] = revenue;
    }

    Guid id = await dataverse.CreateAsync(entity, ct);
    return Results.Created($"/accounts/{id}", new { Id = id });
});

// Normalize SDK failures into HTTP problem responses.
app.UseExceptionHandler(handler => handler.Run(async context =>
{
    Exception? exception = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
    if (exception is DataverseException dataverseException)
    {
        context.Response.StatusCode = dataverseException.Error.HttpStatusCode ?? StatusCodes.Status502BadGateway;
        await context.Response.WriteAsJsonAsync(new
        {
            title = $"Dataverse error: {dataverseException.Category}",
            status = context.Response.StatusCode,
            detail = dataverseException.Message,
            requestId = dataverseException.Error.RequestId,
        });
    }
}));

app.Run();

internal sealed record NewAccount(string Name, decimal? Revenue);
