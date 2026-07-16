# Koras.Dataverse

A modern, fluent, resilient .NET SDK for Microsoft Dataverse: authentication via Azure.Identity,
CRUD, OData and FetchXML queries with automatic paging, batch operations, metadata and solution
helpers, retry/throttling handling, dependency injection, health checks, and OpenTelemetry-ready
diagnostics.

```csharp
builder.Services.AddDataverse(o =>
{
    o.EnvironmentUrl = new Uri("https://contoso.crm.dynamics.com");
    o.Authentication.UseClientSecret(tenantId, clientId, clientSecret);
});

public sealed class AccountService(IDataverseClient dataverse)
{
    public async Task<Guid> CreateAsync(string name, CancellationToken ct)
    {
        var account = new Entity("account") { ["name"] = name };
        return await dataverse.CreateAsync(account, ct);
    }
}
```

- **Docs & samples:** https://github.com/kkora/Koras-Dataverse-SDK
- **License:** MIT · **Publisher:** Koras Technologies

Failures surface as `DataverseException` with a normalized `DataverseError` (category, Dataverse
error code, HTTP status, request id, retry hints). Service-protection limits (HTTP 429) are
retried automatically with `Retry-After` support.
