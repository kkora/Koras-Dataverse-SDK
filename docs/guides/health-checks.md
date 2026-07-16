# Guide: Health Checks

The SDK ships a health check that probes Dataverse connectivity **and** authentication with a
`WhoAmI` call against the default client.

## Registration

```csharp
builder.Services.AddHealthChecks()
    .AddDataverseHealthCheck();
```

Optional parameters:

```csharp
builder.Services.AddHealthChecks()
    .AddDataverseHealthCheck(
        name: "dataverse",                                      // default "dataverse"
        failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded,
        tags: new[] { "ready", "external" });
```

On success the check reports `Healthy` with the caller's `userId` and `organizationId` in the
check data. On a `DataverseException` it reports the configured failure status with a
description like `Dataverse probe failed: Authentication.` — the category tells you at a
glance whether the problem is credentials, permissions, throttling, or the service itself.

The check targets the **default** `IDataverseClient`. For a named client in a
multi-environment app, register the check type directly against a factory lookup:

```csharp
using Koras.Dataverse.HealthChecks;

builder.Services.AddHealthChecks().Add(new Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckRegistration(
    "dataverse-prod",
    provider => new DataverseHealthCheck(
        provider.GetRequiredService<IDataverseClientFactory>().GetClient("crm-prod")),
    failureStatus: null,
    tags: new[] { "ready" }));
```

## What the probe costs

`WhoAmI` is the cheapest authenticated Dataverse call — a small GET with a tiny response, no
table access. Still, it is a real request: it **counts against service-protection limits** and
API entitlement, it reuses the cached token (so it does not hit Entra ID each probe), and it is
subject to the client's retry policy and `Timeout`.

Practical implications:

- Probe intervals of 10–30 seconds are harmless. Sub-second probing is pointless and wasteful.
- During a Dataverse throttling episode the probe may itself be throttled (and retried); with
  aggressive probe timeouts, budget for the SDK's retry delays or lower `Retry.MaxRetries` on
  the probed client.

## Readiness vs. liveness

Wire the Dataverse check into **readiness**, never liveness:

- **Liveness** answers "should the orchestrator restart this process?" A Dataverse outage is
  not fixed by restarting your pod — including this check in liveness turns an upstream
  incident into a restart loop.
- **Readiness** answers "should traffic be routed here?" If your service cannot function
  without Dataverse, failing readiness during an outage is exactly right.

```csharp
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

builder.Services.AddHealthChecks()
    .AddDataverseHealthCheck(tags: new[] { "ready" });

var app = builder.Build();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false, // no dependency probes: process-up only
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
});
```

If Dataverse is a *soft* dependency (the service degrades but still works), keep the check in
readiness with `failureStatus: HealthStatus.Degraded` so dashboards see the problem without
traffic being drained.

## Related

- [ASP.NET Core guide](aspnet-core.md) and [minimal API guide](minimal-api.md) — endpoints in context
- [Common errors](../troubleshooting/common-errors.md) — interpreting the failure categories
