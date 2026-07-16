# Recipe: Production Configuration

A hardened, observable production setup in one place: managed identity, tight timeout, tuned
retries, health checks, OpenTelemetry, and quiet logs. Rationale for each choice lives in the
[production configuration guide](../guides/configuration.md).

## Packages

```bash
dotnet add package Koras.Dataverse
dotnet add package Koras.Dataverse.OpenTelemetry
dotnet add package OpenTelemetry.Extensions.Hosting
dotnet add package OpenTelemetry.Exporter.OpenTelemetryProtocol
```

## appsettings.json

No secrets — the identity is managed:

```json
{
  "Dataverse": {
    "EnvironmentUrl": "https://contoso.crm.dynamics.com"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Koras.Dataverse": "Warning",
      "Koras.Dataverse.Http": "Warning"
    }
  }
}
```

## Program.cs

```csharp
using Koras.Dataverse;
using Koras.Dataverse.OpenTelemetry;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// --- Dataverse: interactive traffic -----------------------------------
builder.Services.AddDataverse(options =>
{
    options.EnvironmentUrl = new Uri(builder.Configuration["Dataverse:EnvironmentUrl"]!);

    // No secrets: system-assigned managed identity, registered as an
    // application user with a least-privilege role in the environment.
    options.Authentication.UseManagedIdentity();

    // Tight budget for request-path calls; covers all retries.
    options.Timeout = TimeSpan.FromSeconds(30);

    // Fail fast on the request path; the server's Retry-After is still honored.
    options.Retry.MaxRetries = 2;
    options.Retry.BaseDelay = TimeSpan.FromSeconds(1);
    options.Retry.MaxDelay = TimeSpan.FromSeconds(5);
});

// --- Dataverse: long-running ALM/solution work (separate budget) -------
builder.Services.AddDataverse("solutions", options =>
{
    options.EnvironmentUrl = new Uri(builder.Configuration["Dataverse:EnvironmentUrl"]!);
    options.Authentication.UseManagedIdentity();
    options.Timeout = TimeSpan.FromMinutes(15);
    options.Retry.MaxRetries = 5;
    options.Retry.MaxDelay = TimeSpan.FromMinutes(1);
});

// --- Health checks ------------------------------------------------------
builder.Services.AddHealthChecks()
    .AddDataverseHealthCheck(tags: new[] { "ready" });

// --- OpenTelemetry ------------------------------------------------------
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(
        serviceName: "contoso-integration",
        serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString()))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddKorasDataverseInstrumentation()
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddKorasDataverseInstrumentation()
        .AddOtlpExporter());

var app = builder.Build();

// Liveness: process only. Readiness: includes the Dataverse WhoAmI probe.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
});

// …endpoints…

app.Run();
```

## The checklist

| Concern | Setting | Why |
|---|---|---|
| Credentials | `UseManagedIdentity()` | Nothing to store or rotate; falls back to `UseCertificate` off-Azure |
| Startup safety | (automatic) `ValidateOnStart` | Bad URL/credential config fails deployment, not the first user request |
| Request-path timeout | `Timeout = 30s` | Degrade visibly instead of pinning threads for 100 s |
| Solution operations | named client, `Timeout = 15m` | Long ALM calls don't force a long budget on everything |
| Throttling | defaults + `RespectRetryAfter` | Dataverse's own hint always wins |
| Readiness | `AddDataverseHealthCheck(tags: ["ready"])` | Drain traffic during a Dataverse outage; never in liveness |
| Traces/metrics | `AddKorasDataverseInstrumentation()` | Spans per operation; alert on `koras.dataverse.client.throttles` and non-success outcomes |
| Logs | both SDK categories at `Warning` | Retries and failures visible, zero steady-state noise |
| Secrets hygiene | (automatic) | The SDK never logs tokens or row data |

## Alerting suggestions

- `koras.dataverse.client.throttles` rate > 0 sustained for minutes → approaching
  service-protection limits; review batch sizes and parallelism.
- `koras.dataverse.client.operations` with `outcome="error"` ratio above your SLO → inspect
  `dataverse.error.category` on failed spans.
- Readiness probe failing with `Dataverse probe failed: Authentication` right after a deploy →
  identity/role misconfiguration, not an outage.
