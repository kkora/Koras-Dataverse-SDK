# Koras.Dataverse.OpenTelemetry

OpenTelemetry wiring for the [Koras Dataverse SDK](https://github.com/kkora/Koras-Dataverse-SDK).

```csharp
services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddKorasDataverseInstrumentation())
    .WithMetrics(metrics => metrics.AddKorasDataverseInstrumentation());
```

Subscribes your providers to the SDK's `Koras.Dataverse` `ActivitySource` (spans per operation
with operation/table/status tags — never row data) and `Meter`
(`koras.dataverse.client.operations`, `…operation.duration`, `…retries`, `…throttles`).

- **License:** MIT · **Publisher:** Koras Technologies
