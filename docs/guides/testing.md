# Guide: Testing Consumer Code

How to unit-test code that uses the SDK. Everything injectable is an interface in
`Koras.Dataverse.Abstractions` — reference that package (not the implementation) from test
projects and substitute freely. Copy-paste variants live in
[testing recipes](../recipes/testing-recipes.md).

## The system under test

```csharp
using Koras.Dataverse;
using Koras.Dataverse.Errors;
using Koras.Dataverse.Queries;

public sealed class CustomerDirectory(IDataverseClient dataverse)
{
    public async Task<string?> FindNameAsync(Guid accountId, CancellationToken ct)
    {
        try
        {
            Entity account = await dataverse.RetrieveAsync("account", accountId, ColumnSet.Of("name"), ct);
            return account.GetValue<string>("name");
        }
        catch (DataverseException exception) when (exception.Category == DataverseErrorCategory.NotFound)
        {
            return null;
        }
    }

    public async Task<int> CountActiveAsync(CancellationToken ct)
    {
        int count = 0;
        var query = ODataQuery.For("account").Select("accountid").Where(f => f.Eq("statecode", 0));
        await foreach (Entity _ in dataverse.QueryAllAsync(query, ct))
        {
            count++;
        }

        return count;
    }
}
```

## Substituting IDataverseClient — NSubstitute

```csharp
using Koras.Dataverse;
using NSubstitute;
using Xunit;

public class CustomerDirectoryTests
{
    private readonly IDataverseClient _dataverse = Substitute.For<IDataverseClient>();

    [Fact]
    public async Task FindNameAsync_returns_the_name()
    {
        var id = Guid.NewGuid();
        _dataverse.RetrieveAsync("account", id, Arg.Any<ColumnSet>(), Arg.Any<CancellationToken>())
            .Returns(new Entity("account", id) { ["name"] = "Contoso" });

        var directory = new CustomerDirectory(_dataverse);

        Assert.Equal("Contoso", await directory.FindNameAsync(id, CancellationToken.None));
    }

    [Fact]
    public async Task CountActiveAsync_counts_streamed_rows()
    {
        _dataverse.QueryAllAsync(Arg.Any<Koras.Dataverse.Queries.ODataQuery>(), Arg.Any<CancellationToken>())
            .Returns(TestEntities.Stream(
                new Entity("account", Guid.NewGuid()),
                new Entity("account", Guid.NewGuid())));

        var directory = new CustomerDirectory(_dataverse);

        Assert.Equal(2, await directory.CountActiveAsync(CancellationToken.None));
    }
}
```

`QueryAllAsync` returns `IAsyncEnumerable<Entity>`; a tiny helper turns arrays into streams:

```csharp
public static class TestEntities
{
    public static async IAsyncEnumerable<Entity> Stream(params Entity[] entities)
    {
        foreach (Entity entity in entities)
        {
            yield return entity;
        }

        await Task.CompletedTask;
    }
}
```

## The same with Moq

```csharp
using Moq;

var dataverse = new Mock<IDataverseClient>();
dataverse
    .Setup(c => c.RetrieveAsync("account", id, It.IsAny<ColumnSet>(), It.IsAny<CancellationToken>()))
    .ReturnsAsync(new Entity("account", id) { ["name"] = "Contoso" });

var directory = new CustomerDirectory(dataverse.Object);
```

## In-memory fake Entity data

`Entity` is a plain class — construct realistic test rows directly, including formatted values
and lookups:

```csharp
var account = new Entity("account", Guid.NewGuid())
{
    ["name"] = "Contoso",
    ["revenue"] = 25_000m,
    ["accountcategorycode"] = 1,
    ["primarycontactid"] = new EntityReference("contact", Guid.NewGuid()) { Name = "Ada Lovelace" },
};
account.FormattedValues["accountcategorycode"] = "Preferred Customer";
account.FormattedValues["revenue"] = "$25,000.00";
```

For code driven off `DataverseQueryResult` (single-page `QueryAsync`), build pages directly:

```csharp
var page = new DataverseQueryResult(new[] { account }, nextLink: null, totalCount: 1);
```

## Testing error paths

`DataverseException` and `DataverseError` are constructible — throw exactly the failure you
want to exercise:

```csharp
using Koras.Dataverse.Errors;

_dataverse.RetrieveAsync("account", Arg.Any<Guid>(), Arg.Any<ColumnSet>(), Arg.Any<CancellationToken>())
    .Returns<Task<Entity>>(_ => throw new DataverseException(new DataverseError
    {
        Category = DataverseErrorCategory.NotFound,
        Message = "account does not exist",
        HttpStatusCode = 404,
        ErrorCode = "0x80040217",
    }));

Assert.Null(await directory.FindNameAsync(Guid.NewGuid(), CancellationToken.None));
```

Useful variants:

```csharp
// Throttling with a server hint
new DataverseError
{
    Category = DataverseErrorCategory.Throttling,
    Message = "Rate limit exceeded",
    HttpStatusCode = 429,
    RetryAfter = TimeSpan.FromSeconds(30),
    IsTransient = true,
}

// Cancellation: never a DataverseException — test with the real signal
_dataverse.WhoAmIAsync(Arg.Any<CancellationToken>())
    .Returns<Task<WhoAmIResponse>>(_ => throw new OperationCanceledException());
```

## What to test at which level

- **Unit (substitute `IDataverseClient`)** — your domain logic, error-category branching,
  paging consumption. Fast, no HTTP.
- **Integration-style (fake `HttpMessageHandler` + real `DataverseClient`)** — your custom
  `DelegatingHandler`s, or asserting exact requests your code produces. See
  [testing recipes](../recipes/testing-recipes.md); this mirrors how the SDK tests itself.
- **Live integration** — a real dev environment, gated on environment variables so CI stays
  green without secrets (the SDK's own suite keys off `KORAS_DATAVERSE_*`; see
  [environment variables](../configuration/environment-variables.md)).

Don't re-test the SDK from your suite (retry policy, encoding, paging mechanics are covered by
the SDK's own tests) — test *your* behavior around it.
