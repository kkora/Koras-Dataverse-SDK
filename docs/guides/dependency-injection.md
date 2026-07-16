# Guide: Dependency Injection Patterns

Beyond the [getting-started DI page](../getting-started/dependency-injection.md): patterns for
multiple environments and for keeping consumer code testable.

## Multi-environment: named clients

One `AddDataverse(name, …)` call per environment. Names are free-form strings; pick stable ones
because they appear in factory lookups and error messages.

```csharp
string[] environments = ["crm-prod", "crm-uat", "crm-dev"];

foreach (string name in environments)
{
    IConfigurationSection section = builder.Configuration.GetSection($"Dataverse:{name}");
    builder.Services.AddDataverse(name, options =>
    {
        options.EnvironmentUrl = new Uri(section["EnvironmentUrl"]!);
        options.Authentication.UseManagedIdentity();
    });
}
```

```json
{
  "Dataverse": {
    "crm-prod": { "EnvironmentUrl": "https://contoso.crm.dynamics.com" },
    "crm-uat":  { "EnvironmentUrl": "https://contoso-uat.crm.dynamics.com" },
    "crm-dev":  { "EnvironmentUrl": "https://contoso-dev.crm.dynamics.com" }
  }
}
```

Consume through the factory:

```csharp
public sealed class EnvironmentComparer(IDataverseClientFactory factory)
{
    public async Task<bool> SolutionMatchesAsync(string solution, CancellationToken ct)
    {
        var prodTask = factory.GetClient("crm-prod").Solutions.FindAsync(solution, ct);
        var uatTask = factory.GetClient("crm-uat").Solutions.FindAsync(solution, ct);
        return (await prodTask)?.Version == (await uatTask)?.Version;
    }
}
```

Reminders:

- Each named client is its own singleton with its own options, token cache, and HTTP pipeline.
- With several named clients and no unnamed registration, injecting `IDataverseClient`
  directly throws by design — the ambiguity must be resolved explicitly via the factory.
- `GetClient` with an unknown name throws `InvalidOperationException` listing the registered
  names — typos fail loudly.

## Pinning a default while keeping named access

If one environment is "the" environment and others are auxiliary, register the main one
unnamed and the rest named:

```csharp
builder.Services.AddDataverse(options => { /* primary environment */ });
builder.Services.AddDataverse("archive", options => { /* secondary environment */ });
```

Now `IDataverseClient` injects the primary, and `factory.GetClient("archive")` reaches the
secondary.

## Keeping consumers testable

Depend on `IDataverseClient` (from `Koras.Dataverse.Abstractions`), never on the concrete
`DataverseClient`. Your domain services then substitute cleanly:

```csharp
public sealed class RevenueReporter(IDataverseClient dataverse)
{
    public async Task<decimal> TotalActiveRevenueAsync(CancellationToken ct)
    {
        decimal total = 0;
        var query = ODataQuery.For("account").Select("revenue").Where(f => f.Eq("statecode", 0));
        await foreach (Entity account in dataverse.QueryAllAsync(query, ct))
        {
            total += account.GetValue<decimal?>("revenue") ?? 0;
        }

        return total;
    }
}
```

Test with a substitute (NSubstitute shown; Moq works identically in spirit):

```csharp
using NSubstitute;

[Fact]
public async Task Sums_revenue_of_streamed_accounts()
{
    var dataverse = Substitute.For<IDataverseClient>();
    dataverse.QueryAllAsync(Arg.Any<ODataQuery>(), Arg.Any<CancellationToken>())
        .Returns(Rows(
            new Entity("account", Guid.NewGuid()) { ["revenue"] = 100m },
            new Entity("account", Guid.NewGuid()) { ["revenue"] = 250m },
            new Entity("account", Guid.NewGuid()) { /* revenue missing */ }));

    var reporter = new RevenueReporter(dataverse);

    Assert.Equal(350m, await reporter.TotalActiveRevenueAsync(CancellationToken.None));

    static async IAsyncEnumerable<Entity> Rows(params Entity[] entities)
    {
        foreach (Entity entity in entities)
        {
            yield return entity;
        }

        await Task.CompletedTask;
    }
}
```

For multi-environment code, substitute `IDataverseClientFactory` the same way and return a
substitute client per name. More patterns — error paths, fake data helpers, integration-style
tests — in the [testing guide](testing.md) and [testing recipes](../recipes/testing-recipes.md).

## Custom handlers per client

Each `AddDataverse` call returns a `DataverseBuilder` scoped to that one client, so different
environments can have different pipeline extras:

```csharp
builder.Services.AddDataverse("crm-prod", options => { /* … */ })
    .AddHttpMessageHandler(_ => new AuditHeaderHandler("prod"));

builder.Services.AddDataverse("crm-dev", options => { /* … */ })
    .AddHttpMessageHandler(_ => new AuditHeaderHandler("dev"));
```
