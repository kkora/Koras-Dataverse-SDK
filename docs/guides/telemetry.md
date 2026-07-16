# Guide: Telemetry

The SDK is instrumented with the standard .NET diagnostics primitives — an `ActivitySource`
for tracing and a `Meter` for metrics — with **no OpenTelemetry dependency in the core
package**. The `Koras.Dataverse.OpenTelemetry` package wires both into OTel in one line each.

## Names

Both sources are named **`Koras.Dataverse`** (constants:
`DataverseDiagnostics.ActivitySourceName` and `DataverseDiagnostics.MeterName` in
`Koras.Dataverse.Diagnostics`).

## Traces

One client span per operation, named `dataverse.<operation>`, covering the whole call
*including retries*:

`dataverse.create` · `dataverse.retrieve` · `dataverse.update` · `dataverse.upsert` ·
`dataverse.delete` · `dataverse.associate` · `dataverse.disassociate` · `dataverse.query` ·
`dataverse.fetch` · `dataverse.batch` · `dataverse.whoami`

Span tags:

| Tag | Example | When |
|---|---|---|
| `dataverse.operation` | `create` | always |
| `dataverse.table` | `account` | when the operation targets a table |
| `http.response.status_code` | `429` | when an HTTP response was received |
| `dataverse.error.category` | `Throttling` | on failure |
| `dataverse.request_id` | `4c8d…` | on failure, when Dataverse sent one |

Failed, timed-out, and canceled operations set the span status to error. Tag values never
contain row data or credentials.

## Metrics

| Instrument | Type | Unit | Description |
|---|---|---|---|
| `koras.dataverse.client.operations` | Counter | — | Completed operations |
| `koras.dataverse.client.operation.duration` | Histogram | s | Operation duration including retries |
| `koras.dataverse.client.retries` | Counter | — | Retry attempts performed by the SDK |
| `koras.dataverse.client.throttles` | Counter | — | HTTP 429 responses (service-protection hits) |

`operations` and `operation.duration` are tagged with `dataverse.operation`,
`dataverse.table`, and `outcome` (`success`, `error`, `timeout`, `canceled`, `network`).

Alerts worth having: a rising `throttles` rate (you are pressing service-protection limits),
and `operations` with `outcome != "success"` as an error-rate signal per table/operation.

## Wiring with OpenTelemetry

```bash
dotnet add package Koras.Dataverse.OpenTelemetry
dotnet add package OpenTelemetry.Extensions.Hosting
dotnet add package OpenTelemetry.Exporter.OpenTelemetryProtocol
```

Full `Program.cs` for a minimal API with traces and metrics exported over OTLP:

```csharp
using Koras.Dataverse;
using Koras.Dataverse.OpenTelemetry;
using Koras.Dataverse.Queries;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDataverse(options =>
{
    options.EnvironmentUrl = new Uri(builder.Configuration["Dataverse:EnvironmentUrl"]!);
    options.Authentication.UseManagedIdentity();
});

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("contoso-accounts-api"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddKorasDataverseInstrumentation()   // subscribes to the "Koras.Dataverse" source
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddKorasDataverseInstrumentation()   // subscribes to the "Koras.Dataverse" meter
        .AddOtlpExporter());

var app = builder.Build();

app.MapGet("/accounts", async (IDataverseClient dataverse, CancellationToken ct) =>
{
    DataverseQueryResult page = await dataverse.QueryAsync(
        ODataQuery.For("account").Select("name").Top(10), ct);
    return page.Entities.Select(e => e.GetValue<string>("name"));
});

app.Run();
```

Each `/accounts` request now produces an ASP.NET Core server span with a
`dataverse.query` child span, and the Dataverse metrics flow to your OTLP endpoint.

`AddKorasDataverseInstrumentation()` is intentionally thin — `AddSource("Koras.Dataverse")` /
`AddMeter("Koras.Dataverse")` under the hood — so it composes with any exporter, sampler, or
view configuration you already have.

## Without OpenTelemetry

The names are public constants, so anything that understands .NET diagnostics works:

- `dotnet-counters monitor --counters Koras.Dataverse` for live metrics,
- an `ActivityListener` subscribed to the `Koras.Dataverse` source,
- `AddSource`/`AddMeter` string literals if you prefer not to reference the helper package.

## Related

- [Operation lifecycle](../concepts/lifecycle.md) — where in a call telemetry is emitted
- [Logging guide](logging.md) — the complementary `ILogger` signals
