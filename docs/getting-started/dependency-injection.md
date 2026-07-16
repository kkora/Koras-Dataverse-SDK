# Dependency Injection

The SDK is designed for `Microsoft.Extensions.DependencyInjection`. Registration lives in the
main `Koras.Dataverse` package; the extension methods sit in the
`Microsoft.Extensions.DependencyInjection` namespace so they are visible wherever you configure
services.

## Default client

```csharp
using Koras.Dataverse;

builder.Services.AddDataverse(options =>
{
    options.EnvironmentUrl = new Uri("https://contoso.crm.dynamics.com");
    options.Authentication.UseManagedIdentity();
});
```

This registers:

- `IDataverseClient` — the client itself, resolvable directly,
- `IDataverseClientFactory` — for named-client scenarios,
- the options (validated at startup — see [validation](../configuration/validation.md)),
- a named `HttpClient` (via `IHttpClientFactory`) carrying the SDK's retry and authentication
  handlers.

Inject and use:

```csharp
public sealed class InvoiceService(IDataverseClient dataverse)
{
    public async Task<Guid> CreateAccountAsync(string name, CancellationToken ct)
    {
        var account = new Entity("account") { ["name"] = name };
        return await dataverse.CreateAsync(account, ct);
    }
}
```

## Named clients (multiple environments)

Register one client per environment, each with its own options:

```csharp
builder.Services.AddDataverse("crm-prod", options =>
{
    options.EnvironmentUrl = new Uri("https://contoso.crm.dynamics.com");
    options.Authentication.UseManagedIdentity();
});

builder.Services.AddDataverse("crm-test", options =>
{
    options.EnvironmentUrl = new Uri("https://contoso-test.crm.dynamics.com");
    options.Authentication.UseManagedIdentity();
});
```

Resolve them through the factory:

```csharp
public sealed class SyncService(IDataverseClientFactory factory)
{
    public async Task CompareAsync(CancellationToken ct)
    {
        IDataverseClient prod = factory.GetClient("crm-prod");
        IDataverseClient test = factory.GetClient("crm-test");

        WhoAmIResponse prodWho = await prod.WhoAmIAsync(ct);
        WhoAmIResponse testWho = await test.WhoAmIAsync(ct);
    }
}
```

`GetClient` throws `InvalidOperationException` for unregistered names, listing the names that
*are* registered.

How the unnamed `IDataverseClient` resolves alongside named registrations:

- If a client was registered without a name (the internal name `"Default"`), that one is the
  `IDataverseClient`.
- If exactly one named client exists, it doubles as the default.
- If multiple named clients exist and none is the default, resolving `IDataverseClient`
  throws — inject `IDataverseClientFactory` instead. This is deliberate: an ambiguous default
  would silently target the wrong environment.

## Why singletons

Each Dataverse client is a **thread-safe singleton** per registered name. This is the right
lifetime because the client holds:

- a **token cache** — tokens are reused until five minutes before expiry; a transient or scoped
  client would re-authenticate constantly,
- pooled HTTP connections via `IHttpClientFactory`,
- cached entity-set-name resolutions.

Never register your own transient wrapper that creates clients per request. Inject the
singleton and pass a `CancellationToken` per call.

## Extending the HTTP pipeline

`AddDataverse` returns a `DataverseBuilder` for advanced customization of that one named
client:

```csharp
builder.Services.AddDataverse(options => { /* … */ })
    .AddHttpMessageHandler(provider => new CorrelationIdHandler());
```

```csharp
public sealed class CorrelationIdHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.TryAddWithoutValidation("x-correlation-id", Guid.NewGuid().ToString("D"));
        return base.SendAsync(request, cancellationToken);
    }
}
```

Custom handlers run **inside** the SDK's retry and authentication handlers — your handler sees
each individual attempt, already authenticated. The builder also exposes `Services`, `Name`,
and the raw `HttpClientBuilder` when you need full `IHttpClientFactory` control.

## Health checks

```csharp
builder.Services.AddHealthChecks().AddDataverseHealthCheck();
```

See the [health checks guide](../guides/health-checks.md).

## Where next

- [Configuration](configuration.md) — binding options from `appsettings.json`
- [DI patterns guide](../guides/dependency-injection.md) — multi-environment and testing patterns
