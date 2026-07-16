using Koras.Dataverse.Diagnostics;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Koras.Dataverse.OpenTelemetry;

/// <summary>One-line OpenTelemetry wiring for the Koras Dataverse SDK.</summary>
public static class DataverseOpenTelemetryExtensions
{
    /// <summary>Subscribes the tracer provider to the SDK's <c>Koras.Dataverse</c> activity source.</summary>
    public static TracerProviderBuilder AddKorasDataverseInstrumentation(this TracerProviderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddSource(DataverseDiagnostics.ActivitySourceName);
    }

    /// <summary>Subscribes the meter provider to the SDK's <c>Koras.Dataverse</c> meter.</summary>
    public static MeterProviderBuilder AddKorasDataverseInstrumentation(this MeterProviderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddMeter(DataverseDiagnostics.MeterName);
    }
}
