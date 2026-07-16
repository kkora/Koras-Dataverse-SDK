# Recipes: Advanced Scenarios

## Custom IDataverseTokenProvider

Bypass Azure.Identity entirely and supply tokens from any source — an on-behalf-of flow, a
token broker, a test fixture. Implementations must be thread-safe and should cache until close
to expiry:

```csharp
using Koras.Dataverse.Authentication;

public sealed class BrokeredTokenProvider(ITokenBroker broker) : IDataverseTokenProvider
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private string? _token;
    private DateTimeOffset _expiresOn;

    public async ValueTask<string> GetAccessTokenAsync(Uri environmentUrl, CancellationToken cancellationToken = default)
    {
        if (_token is not null && DateTimeOffset.UtcNow < _expiresOn - TimeSpan.FromMinutes(5))
        {
            return _token;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_token is null || DateTimeOffset.UtcNow >= _expiresOn - TimeSpan.FromMinutes(5))
            {
                (string token, DateTimeOffset expiresOn) =
                    await broker.AcquireAsync(environmentUrl.GetLeftPart(UriPartial.Authority) + "/.default", cancellationToken);
                (_token, _expiresOn) = (token, expiresOn);
            }

            return _token;
        }
        finally
        {
            _lock.Release();
        }
    }
}
```

```csharp
builder.Services.AddDataverse(options =>
{
    options.EnvironmentUrl = new Uri("https://contoso.crm.dynamics.com");
    options.Authentication.UseTokenProvider(new BrokeredTokenProvider(broker));
});
```

If you have an `Azure.Core.TokenCredential` rather than raw tokens,
`options.Authentication.UseTokenCredential(credential)` gives you the SDK's built-in caching
and single-flight refresh for free — prefer it over a hand-rolled provider.

## Custom DelegatingHandler via DataverseBuilder

`AddDataverse` returns a `DataverseBuilder`; handlers you add run **inside** the SDK's retry
and authentication handlers, so they observe each authenticated attempt:

```csharp
public sealed class RequestAuditHandler(ILogger<RequestAuditHandler> logger) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        long started = System.Diagnostics.Stopwatch.GetTimestamp();
        HttpResponseMessage response = await base.SendAsync(request, cancellationToken);
        logger.LogDebug("{Method} {Path} -> {Status} in {Elapsed}",
            request.Method,
            request.RequestUri?.AbsolutePath,
            (int)response.StatusCode,
            System.Diagnostics.Stopwatch.GetElapsedTime(started));
        return response;
    }
}
```

```csharp
builder.Services.AddTransient<RequestAuditHandler>();

builder.Services.AddDataverse(options => { /* … */ })
    .AddHttpMessageHandler(provider => provider.GetRequiredService<RequestAuditHandler>());
```

Handlers are per named client — different environments can carry different extras. For
anything beyond message handlers, `DataverseBuilder.HttpClientBuilder` exposes the underlying
`IHttpClientBuilder`.

## EntitySetNameOverrides

The client pluralizes logical names with Dataverse's standard rules (`account` → `accounts`,
`opportunity` → `opportunities`, `bus` → `buses`). A few customized tables have set names that
do not follow the rules; if you see `NotFound` on a table you know exists, check the actual
set name and override:

```csharp
builder.Services.AddDataverse(options =>
{
    options.EnvironmentUrl = new Uri("https://contoso.crm.dynamics.com");
    options.Authentication.UseManagedIdentity();
    options.EntitySetNameOverrides["new_metadata"] = "new_metadataset";
});
```

Discover the correct name at runtime via metadata:

```csharp
string setName = await dataverse.Metadata.GetEntitySetNameAsync("new_metadata", ct);
```

## Solution export/import/publish pipeline

An environment-promotion step — export managed from source, import to target, publish:

```csharp
using Koras.Dataverse;
using Koras.Dataverse.Solutions;

public sealed class SolutionPromoter(IDataverseClientFactory factory, ILogger<SolutionPromoter> logger)
{
    public async Task PromoteAsync(string solutionName, CancellationToken ct)
    {
        IDataverseClient source = factory.GetClient("crm-dev");
        IDataverseClient target = factory.GetClient("crm-uat");

        SolutionInfo? existing = await source.Solutions.FindAsync(solutionName, ct);
        if (existing is null)
        {
            throw new InvalidOperationException($"Solution '{solutionName}' is not installed in the source environment.");
        }

        logger.LogInformation("Exporting {Solution} {Version}…", solutionName, existing.Version);
        byte[] zip = await source.Solutions.ExportAsync(solutionName, managed: true, ct);

        var importJobId = Guid.NewGuid(); // correlate with monitoring queries on importjob
        logger.LogInformation("Importing ({Bytes} bytes, job {JobId})…", zip.Length, importJobId);
        await target.Solutions.ImportAsync(zip, new SolutionImportOptions
        {
            OverwriteUnmanagedCustomizations = false,
            PublishWorkflows = true,
            ImportJobId = importJobId,
        }, ct);

        await target.Solutions.PublishAllAsync(ct);

        SolutionInfo? installed = await target.Solutions.FindAsync(solutionName, ct);
        logger.LogInformation("Target now has {Solution} {Version}.", solutionName, installed?.Version);
    }
}
```

Register the clients involved with a generous `Timeout` (solution operations run for minutes) —
see the [production configuration guide](../guides/configuration.md).

## Metadata-driven dynamic UI

Build form fields from column metadata and populate choice dropdowns from choice options:

```csharp
using Koras.Dataverse.Metadata;

public sealed record FieldDescriptor(string LogicalName, string Label, string Type, bool Required,
    IReadOnlyList<ChoiceOption>? Choices);

public sealed class FormBuilder(IDataverseClient dataverse)
{
    public async Task<IReadOnlyList<FieldDescriptor>> DescribeAsync(string table, CancellationToken ct)
    {
        IReadOnlyList<ColumnMetadata> columns = await dataverse.Metadata.GetColumnsAsync(table, ct);

        var fields = new List<FieldDescriptor>();
        foreach (ColumnMetadata column in columns.Where(c => !c.IsPrimaryId))
        {
            IReadOnlyList<ChoiceOption>? choices = null;
            if (column.AttributeType == "Picklist")
            {
                choices = await dataverse.Metadata.GetChoicesAsync(table, column.LogicalName, ct);
            }

            fields.Add(new FieldDescriptor(
                column.LogicalName,
                column.DisplayName ?? column.LogicalName,
                column.AttributeType ?? "String",
                column.RequiredLevel == "ApplicationRequired",
                choices));
        }

        return fields;
    }
}
```

Each `ChoiceOption` carries `Value`, `Label`, and (when defined) `Color` — enough for a
rendered dropdown that round-trips the underlying `int` back through `Entity` writes. Metadata
calls are ordinary Dataverse requests; cache the results rather than describing tables per
page view.
