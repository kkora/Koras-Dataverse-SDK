# Quick Start

From zero to created-and-queried rows in about five minutes, using dependency injection.

## 1. Install

```bash
dotnet new web -n DataverseQuickStart
cd DataverseQuickStart
dotnet add package Koras.Dataverse
```

## 2. Choose credentials

The simplest development setup is `UseDefault()`, which authenticates with
`DefaultAzureCredential` — it picks up environment variables, a managed identity, or your local
`az login` session automatically. Your user (or the app registration) must exist as an
application user in the Dataverse environment with a security role.

If you have an Entra ID app registration with a client secret instead, store the secret with
user-secrets — never in `appsettings.json`:

```bash
dotnet user-secrets init
dotnet user-secrets set "Dataverse:TenantId" "<tenant-guid>"
dotnet user-secrets set "Dataverse:ClientId" "<app-client-guid>"
dotnet user-secrets set "Dataverse:ClientSecret" "<secret>"
```

## 3. Register the client

`Program.cs`:

```csharp
using Koras.Dataverse;
using Koras.Dataverse.Queries;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDataverse(options =>
{
    options.EnvironmentUrl = new Uri("https://contoso.crm.dynamics.com");

    // Option A: local dev / Azure hosting without secrets
    options.Authentication.UseDefault();

    // Option B: client secret from user-secrets (comment out Option A)
    // options.Authentication.UseClientSecret(
    //     builder.Configuration["Dataverse:TenantId"]!,
    //     builder.Configuration["Dataverse:ClientId"]!,
    //     builder.Configuration["Dataverse:ClientSecret"]!);
});

var app = builder.Build();

app.MapPost("/accounts", async (IDataverseClient dataverse, CancellationToken ct) =>
{
    var account = new Entity("account")
    {
        ["name"] = "Contoso Ltd",
        ["revenue"] = 25_000m,
    };

    Guid id = await dataverse.CreateAsync(account, ct);
    return Results.Created($"/accounts/{id}", new { id });
});

app.MapGet("/accounts", async (IDataverseClient dataverse, CancellationToken ct) =>
{
    var query = ODataQuery.For("account")
        .Select("name", "revenue")
        .Where(f => f.Eq("statecode", 0))
        .OrderBy("name")
        .Top(10);

    DataverseQueryResult page = await dataverse.QueryAsync(query, ct);
    return page.Entities.Select(e => new
    {
        Id = e.Id,
        Name = e.GetValue<string>("name"),
        Revenue = e.GetValue<decimal?>("revenue"),
    });
});

app.Run();
```

Notes:

- `AddDataverse` registers `IDataverseClient` as a thread-safe **singleton** and validates the
  options at startup — a bad URL or incomplete credentials fails fast when the host starts.
- Queries address tables by **logical name** (`account`); the client resolves the Web API
  entity set name (`accounts`) for you.
- Every call takes a `CancellationToken`; ASP.NET Core supplies one per request.

## 4. Run

```bash
dotnet run
```

```bash
curl -X POST http://localhost:5000/accounts
curl http://localhost:5000/accounts
```

The first request acquires an access token (cached and refreshed automatically afterwards),
creates a row, and the second streams a page of active accounts back as JSON.

## Where next

- [Your first application](first-application.md) — the same flow without DI, as a console app
- [Dependency injection](dependency-injection.md) — named clients and multiple environments
- [Configuration](configuration.md) — binding options from `appsettings.json`
- [Common scenarios](../recipes/common-scenarios.md) — copy-paste recipes
