<p align="center">
  <img src="assets/icon.png" alt="Koras Dataverse SDK" width="96" />
</p>

# Koras Dataverse SDK

**A modern, fluent, resilient .NET SDK for Microsoft Dataverse** — authentication, CRUD, OData
and FetchXML queries with automatic paging, batching, metadata, solutions, retries, dependency
injection, health checks, and OpenTelemetry-ready diagnostics. Web API first, mockable
everywhere, secure by default.

[![Build](https://github.com/korastechnologies/koras-dataverse/actions/workflows/build.yml/badge.svg)](https://github.com/korastechnologies/koras-dataverse/actions/workflows/build.yml)
[![Tests](https://github.com/korastechnologies/koras-dataverse/actions/workflows/test.yml/badge.svg)](https://github.com/korastechnologies/koras-dataverse/actions/workflows/test.yml)
[![NuGet](https://img.shields.io/nuget/v/Koras.Dataverse.svg)](https://www.nuget.org/packages/Koras.Dataverse)
[![Downloads](https://img.shields.io/nuget/dt/Koras.Dataverse.svg)](https://www.nuget.org/packages/Koras.Dataverse)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

> **Status: pre-release (0.1.0-preview).** The API surface is open for feedback until 1.0.

**Supported .NET:** net8.0 · net9.0 · net10.0 (the FetchXML builder additionally targets
netstandard2.0 for plug-in assemblies).

## Why

Dataverse teams keep rebuilding the same plumbing: token handling, retries against service
protection limits, paging, `$batch` bodies, metadata calls, and ad-hoc error handling — either
over raw `HttpClient` or through the legacy `IOrganizationService` model. This SDK packages that
plumbing behind a small, strongly typed, DI-native API. See the
[competitive analysis](docs/product/competitive-analysis.md) for an honest comparison.

## Key features

- 🔐 **Auth that fits 2026**: `Azure.Identity` end to end — client secret, certificate, managed identity, interactive, `DefaultAzureCredential`, or bring your own token provider.
- ✍️ **CRUD + upsert + alternate keys** on a plain-CLR `Entity` model (no `OptionSetValue`/`Money` wrappers), plus attribute-mapped POCOs.
- 🔎 **Injection-safe query builders** for OData *and* FetchXML, with `IAsyncEnumerable` auto-paging.
- 📦 **Batching**: atomic change sets or continue-on-error, per-item results.
- 🧱 **Metadata & solutions**: tables, columns, relationships, choices; export/import/publish.
- 🔁 **Resilience by default**: 429/5xx retries honoring `Retry-After`, jittered backoff, per-operation timeouts.
- 🩺 **Operations-ready**: strong error taxonomy, health checks, structured logging, `ActivitySource`/`Meter` telemetry with a one-line OpenTelemetry hookup.

## Installation

```bash
dotnet add package Koras.Dataverse                 # client (brings Abstractions + FetchXml)
dotnet add package Koras.Dataverse.OpenTelemetry   # optional OTel wiring
```

## Five-minute quick start

```csharp
using Koras.Dataverse;
using Koras.Dataverse.Queries;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDataverse(o =>
{
    o.EnvironmentUrl = new Uri("https://contoso.crm.dynamics.com");
    o.Authentication.UseDefault(); // az login locally, managed identity in Azure
});

var app = builder.Build();

app.MapGet("/accounts", async (IDataverseClient dataverse, CancellationToken ct) =>
{
    var page = await dataverse.QueryAsync(
        ODataQuery.For("account").Select("name", "revenue")
            .Where(f => f.Eq("statecode", 0)).OrderBy("name").Top(10), ct);

    return page.Entities.Select(a => new { a.Id, Name = a.GetValue<string>("name") });
});

app.Run();
```

Create, update, batch:

```csharp
var account = new Entity("account")
{
    ["name"] = "Contoso",
    ["revenue"] = 250_000m,
    ["primarycontactid"] = new EntityReference("contact", contactId), // @odata.bind handled for you
};
Guid id = await dataverse.CreateAsync(account, ct);

var batch = new BatchRequest()                    // atomic change set by default
    .AddUpdate(new Entity("account", id) { ["revenue"] = 300_000m })
    .AddCreate(new Entity("task") { ["subject"] = "Follow up" });
await dataverse.ExecuteBatchAsync(batch, ct);
```

FetchXML, streamed across pages:

```csharp
var fetch = FetchXml.For("account")
    .Attributes("name")
    .Where(f => f.Eq("statecode", 0).Like("name", "Contoso%"))
    .Build();

await foreach (Entity row in dataverse.FetchAllAsync(fetch, pageSize: 1000, ct))
{
    Console.WriteLine(row.GetValue<string>("name"));
}
```

Configuration, named clients, health checks, telemetry:

```csharp
services.AddDataverse("prod", o => { /* … */ });          // multi-environment via IDataverseClientFactory
services.AddHealthChecks().AddDataverseHealthCheck();     // WhoAmI probe
services.AddOpenTelemetry()
    .WithTracing(t => t.AddKorasDataverseInstrumentation())
    .WithMetrics(m => m.AddKorasDataverseInstrumentation());
```

Errors are normalized — branch on category, not status codes:

```csharp
try { await dataverse.RetrieveAsync("account", id, ColumnSet.Of("name"), ct); }
catch (DataverseException ex) when (ex.Category == DataverseErrorCategory.NotFound) { /* … */ }
```

## Documentation & samples

- 📚 [Documentation index](docs/index.md) — [quick start](docs/getting-started/quick-start.md) · [configuration](docs/configuration/all-options.md) · [troubleshooting](docs/troubleshooting/common-errors.md)
- 🧪 Samples: [Console](samples/Console.Sample) · [Minimal API](samples/MinimalApi.Sample) · [Worker Service](samples/WorkerService.Sample)
- 🏛 [Architecture](docs/architecture/overview.md) · [ADRs](docs/architecture/decision-records) · [Public API design](docs/api/public-api-design.md)

## Package architecture

| Package | Purpose |
|---|---|
| `Koras.Dataverse` | The client: CRUD, queries, batch, metadata, solutions, auth, resilience, DI, health checks, diagnostics |
| `Koras.Dataverse.Abstractions` | Interfaces + models only — reference from libraries and tests |
| `Koras.Dataverse.FetchXml` | Dependency-free FetchXML builder (netstandard2.0-compatible) |
| `Koras.Dataverse.OpenTelemetry` | One-line OpenTelemetry registration |

## Security & performance

Secure by default: HTTPS-only environment URLs, injection-safe query encoding, no secrets or row
data in logs/traces, secrets via Azure credentials or your own provider —
[threat model](docs/security/threat-model.md). Performance philosophy and benchmarks:
[performance guide](docs/performance/performance-guide.md). Report vulnerabilities privately per
[SECURITY.md](SECURITY.md).

## Versioning

Semantic Versioning; 0.x is preview and may change between minors, 1.0 freezes the public API.
Policy: [docs/release/versioning.md](docs/release/versioning.md).

## Contributing & support

Contributions welcome — see [CONTRIBUTING.md](CONTRIBUTING.md) and the
[roadmap](ROADMAP.md). Questions: [SUPPORT.md](SUPPORT.md).

## License

[MIT](LICENSE) © Koras Technologies
