# Recipes: Testing

Concrete fakes and patterns for testing code that uses the SDK. These mirror the approach the
SDK's own unit-test suite uses (`tests/Koras.Dataverse.UnitTests/TestInfrastructure`): a fake
token provider plus a scripted `HttpMessageHandler` under a real `DataverseClient`.

For substituting `IDataverseClient` itself (the everyday pattern), see the
[testing guide](../guides/testing.md).

## Fake token provider

`IDataverseTokenProvider` is the seam that keeps tests away from Entra ID. A constant-token
fake is all you need:

```csharp
using Koras.Dataverse.Authentication;

public sealed class FakeTokenProvider : IDataverseTokenProvider
{
    public ValueTask<string> GetAccessTokenAsync(Uri environmentUrl, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult("fake-token");
}
```

Plug it in with `options.Authentication.UseTokenProvider(new FakeTokenProvider())` — no
Azure.Identity code runs at all.

## Fake HttpMessageHandler

A scripted handler: enqueue responses, record requests for assertions.

```csharp
using System.Net;
using System.Text;

public sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new();

    public List<(HttpMethod Method, Uri? Uri, string? Body)> Requests { get; } = new();

    public void Enqueue(HttpResponseMessage response) => _responses.Enqueue(_ => response);

    public void Enqueue(Func<HttpRequestMessage, HttpResponseMessage> factory) => _responses.Enqueue(factory);

    public void EnqueueJson(HttpStatusCode statusCode, string json) =>
        Enqueue(new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        });

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string? body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add((request.Method, request.RequestUri, body));

        if (_responses.Count == 0)
        {
            throw new InvalidOperationException($"No scripted response for {request.Method} {request.RequestUri}.");
        }

        return _responses.Dequeue()(request);
    }
}
```

## A real client over the fakes

```csharp
using Koras.Dataverse;

public static class TestClient
{
    public static DataverseClient Create(FakeHttpMessageHandler handler, Action<DataverseClientOptions>? configure = null)
    {
        var options = new DataverseClientOptions
        {
            EnvironmentUrl = new Uri("https://unittest.crm.dynamics.com"),
        };
        options.Authentication.UseTokenProvider(new FakeTokenProvider());
        configure?.Invoke(options);

        var httpClient = new HttpClient(handler, disposeHandler: false);
        return new DataverseClient(httpClient, options);
    }
}
```

This uses the public `DataverseClient(HttpClient, DataverseClientOptions, …)` constructor: the
client builds real URLs, real payloads, and real error mapping, while your fake plays the
Dataverse side. Note this path bypasses the SDK's retry/authentication handlers (they live in
the HTTP pipeline that `AddDataverse`/`Create` build) — which is exactly what you want when
scripting exact response sequences.

## Integration-style test of consumer code

Drive *your* component through the real client and assert both behavior and the wire traffic:

```csharp
using System.Net;
using Xunit;

public class AccountImporterTests
{
    [Fact]
    public async Task Import_creates_the_account_with_the_expected_payload()
    {
        var fake = new FakeHttpMessageHandler();
        var created = new HttpResponseMessage(HttpStatusCode.NoContent);
        created.Headers.Add(
            "OData-EntityId",
            "https://unittest.crm.dynamics.com/api/data/v9.2/accounts(0f3f3f3f-3f3f-3f3f-3f3f-3f3f3f3f3f3f)");
        fake.Enqueue(created);

        using DataverseClient client = TestClient.Create(fake);
        var importer = new AccountImporter(client); // your component under test

        Guid id = await importer.ImportAsync("Contoso", 25_000m, CancellationToken.None);

        Assert.Equal(Guid.Parse("0f3f3f3f-3f3f-3f3f-3f3f-3f3f3f3f3f3f"), id);
        (HttpMethod method, Uri? uri, string? body) = Assert.Single(fake.Requests);
        Assert.Equal(HttpMethod.Post, method);
        Assert.Equal("/api/data/v9.2/accounts", uri!.AbsolutePath);
        Assert.Contains("\"name\":\"Contoso\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Import_translates_dataverse_validation_errors()
    {
        var fake = new FakeHttpMessageHandler();
        fake.EnqueueJson(HttpStatusCode.BadRequest,
            """{"error":{"code":"0x80040203","message":"Invalid attribute value"}}""");

        using DataverseClient client = TestClient.Create(fake);
        var importer = new AccountImporter(client);

        await Assert.ThrowsAsync<ImportRejectedException>(
            () => importer.ImportAsync("", null, CancellationToken.None));
    }
}
```

Handy response scripts:

```csharp
// A query page with two rows
fake.EnqueueJson(HttpStatusCode.OK, """
    {"value":[
        {"accountid":"11111111-1111-1111-1111-111111111111","name":"A"},
        {"accountid":"22222222-2222-2222-2222-222222222222","name":"B"}
    ]}
    """);

// Simulated network failure / timeout inside the handler
fake.Enqueue(_ => throw new HttpRequestException("dns failure"));

// WhoAmI
fake.EnqueueJson(HttpStatusCode.OK, """
    {"UserId":"11111111-1111-1111-1111-111111111111",
     "BusinessUnitId":"22222222-2222-2222-2222-222222222222",
     "OrganizationId":"33333333-3333-3333-3333-333333333333"}
    """);
```

## Testing your custom DelegatingHandler

Chain your handler in front of the fake so tests exercise it exactly as `AddDataverse` would:

```csharp
var fake = new FakeHttpMessageHandler();
fake.EnqueueJson(HttpStatusCode.OK, """{"value":[]}""");

var subject = new CorrelationIdHandler { InnerHandler = fake };
var httpClient = new HttpClient(subject)
{
    BaseAddress = new Uri("https://unittest.crm.dynamics.com/api/data/v9.2/"),
};

var options = new DataverseClientOptions { EnvironmentUrl = new Uri("https://unittest.crm.dynamics.com") };
options.Authentication.UseTokenProvider(new FakeTokenProvider());
using var client = new DataverseClient(httpClient, options);

await client.QueryAsync(Koras.Dataverse.Queries.ODataQuery.For("account"), CancellationToken.None);
// assert on fake.Requests that the correlation header arrived
```

## Live tests, gated

For tests against a real development environment, follow the SDK's own convention: read
connection settings from environment variables and skip when absent, so CI needs no secrets:

```csharp
public sealed class LiveDataverseFactAttribute : FactAttribute
{
    public LiveDataverseFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("KORAS_DATAVERSE_URL") is null)
        {
            Skip = "Live Dataverse environment variables are not configured.";
        }
    }
}
```

See [environment variables](../configuration/environment-variables.md) for the full
`KORAS_DATAVERSE_*` set the SDK's integration suite uses.
