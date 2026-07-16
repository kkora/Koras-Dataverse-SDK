# Koras Dataverse SDK Documentation

The Koras Dataverse SDK is a modern .NET client for the Microsoft Dataverse Web API. It removes
the repetitive plumbing of Dataverse integration — authentication, retries, throttling, paging,
batching, query generation, metadata access, and error handling — behind a small, testable,
DI-native API surface.

> **Preview status:** the SDK is currently in preview (`0.1.0-preview`). The packages are not
> yet published to NuGet.org; install commands throughout these docs are written for the final
> package names. During 0.x the public API may still change between minor versions — see the
> [versioning policy](migration/versioning-policy.md).

## Packages

| Package | Purpose | Target frameworks |
|---|---|---|
| `Koras.Dataverse` | The Web API client: CRUD, queries, batches, paging, metadata, solutions, auth, retry, DI, health checks, telemetry | net8.0, net9.0, net10.0 |
| `Koras.Dataverse.Abstractions` | Interfaces and models only (`IDataverseClient`, `Entity`, error model, query and batch models) — reference this from libraries and test projects | net8.0, net9.0, net10.0 |
| `Koras.Dataverse.FetchXml` | Standalone fluent FetchXML builder with zero dependencies | netstandard2.0, net8.0, net9.0, net10.0 |
| `Koras.Dataverse.OpenTelemetry` | One-line OpenTelemetry wiring for the SDK's traces and metrics | net8.0, net9.0, net10.0 |

## Quick start

```csharp
using Koras.Dataverse;
using Koras.Dataverse.Queries;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDataverse(options =>
{
    options.EnvironmentUrl = new Uri("https://contoso.crm.dynamics.com");
    options.Authentication.UseDefault(); // DefaultAzureCredential: env vars, managed identity, az login, …
});

var app = builder.Build();

app.MapGet("/accounts", (IDataverseClient dataverse, CancellationToken ct) =>
    dataverse.QueryAllAsync(
        ODataQuery.For("account").Select("name", "revenue").Where(f => f.Eq("statecode", 0)),
        ct));

app.Run();
```

Continue with the [5-minute quick start](getting-started/quick-start.md).

## Documentation map

### Getting started

- [Installation](getting-started/installation.md) — packages, target frameworks, what to reference where
- [Quick start](getting-started/quick-start.md) — from install to first query in five minutes
- [Your first application](getting-started/first-application.md) — a complete console app without dependency injection
- [Dependency injection](getting-started/dependency-injection.md) — `AddDataverse`, named clients, the client factory
- [Configuration](getting-started/configuration.md) — `DataverseClientOptions`, appsettings binding, startup validation

### Concepts

- [Overview](concepts/overview.md) — the SDK's mental model
- [Architecture](concepts/architecture.md) — layers, pipeline, package dependency direction
- [Core abstractions](concepts/core-abstractions.md) — every core type with purpose and a snippet
- [Operation lifecycle](concepts/lifecycle.md) — what happens during one call
- [Error handling](concepts/error-handling.md) — `DataverseException`, categories, transient failures
- [Cancellation](concepts/cancellation.md) — tokens, timeouts, and their interaction
- [Thread safety](concepts/thread-safety.md) — what is safe to share, and what is not

### Guides

- [Console applications](guides/console-app.md)
- [ASP.NET Core (controllers)](guides/aspnet-core.md)
- [Minimal APIs](guides/minimal-api.md)
- [Worker services](guides/worker-service.md)
- [Dependency injection patterns](guides/dependency-injection.md)
- [Production configuration](guides/configuration.md)
- [Logging](guides/logging.md)
- [Telemetry and OpenTelemetry](guides/telemetry.md)
- [Health checks](guides/health-checks.md)
- [Testing consumer code](guides/testing.md)

### Recipes

- [Common scenarios](recipes/common-scenarios.md) — copy-paste snippets for everyday operations
- [Advanced scenarios](recipes/advanced-scenarios.md) — custom token providers, handlers, solutions, metadata
- [Production configuration](recipes/production-configuration.md) — a hardened setup in one place
- [Testing recipes](recipes/testing-recipes.md) — fakes and integration-style test patterns

### Configuration reference

- [All options](configuration/all-options.md) — every option with type, default, and effect
- [Environment variables](configuration/environment-variables.md) — recommended names; what the SDK does *not* read
- [appsettings.json](configuration/appsettings.md) — binding patterns and secret placement
- [Validation](configuration/validation.md) — what is enforced, and when

### Troubleshooting

- [Common errors](troubleshooting/common-errors.md) — symptom → cause → fix
- [Diagnostics](troubleshooting/diagnostics.md) — debug logging, request ids, working with Microsoft support
- [Log filtering](troubleshooting/logging.md)
- [Provider errors](troubleshooting/provider-errors.md) — Entra ID (AADSTS) and service-protection limits
- [FAQ](troubleshooting/faq.md)

### Migration

- [Versioning policy](migration/versioning-policy.md)
- [Upgrading](migration/upgrading.md)
- [Breaking changes](migration/breaking-changes.md)
- [Migrating from ServiceClient](migration/from-serviceclient.md) — `Microsoft.PowerPlatform.Dataverse.Client` mapping

### API reference

- [API reference overview](api-reference/overview.md) — namespaces and types by package

### Design documentation

Deeper design material lives alongside this documentation: [architecture](architecture/overview.md),
[public API design](api/public-api-design.md), [testing strategy](testing/test-strategy.md),
[security](security/threat-model.md), [release and versioning](release/versioning.md), and the
canonical [master plan](planning/master-plan.md).
