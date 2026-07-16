using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;

namespace Koras.Dataverse.Diagnostics;

/// <summary>
/// Names of the SDK's diagnostic sources. Wire them to OpenTelemetry with the
/// <c>Koras.Dataverse.OpenTelemetry</c> package or by subscribing to these names directly.
/// </summary>
public static class DataverseDiagnostics
{
    /// <summary>The <see cref="ActivitySource"/> name used for tracing.</summary>
    public const string ActivitySourceName = "Koras.Dataverse";

    /// <summary>The <see cref="Meter"/> name used for metrics.</summary>
    public const string MeterName = "Koras.Dataverse";
}

/// <summary>Internal telemetry instruments. Tag values never contain row data or credentials.</summary>
internal static class DataverseTelemetry
{
    private static readonly string Version =
        typeof(DataverseTelemetry).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";

    public static readonly ActivitySource ActivitySource = new(DataverseDiagnostics.ActivitySourceName, Version);

    public static readonly Meter Meter = new(DataverseDiagnostics.MeterName, Version);

    public static readonly Counter<long> Operations = Meter.CreateCounter<long>(
        "koras.dataverse.client.operations",
        description: "Completed Dataverse operations, tagged by operation, table, and outcome.");

    public static readonly Histogram<double> OperationDuration = Meter.CreateHistogram<double>(
        "koras.dataverse.client.operation.duration",
        unit: "s",
        description: "Duration of Dataverse operations including retries.");

    public static readonly Counter<long> Retries = Meter.CreateCounter<long>(
        "koras.dataverse.client.retries",
        description: "Retry attempts performed by the SDK.");

    public static readonly Counter<long> Throttles = Meter.CreateCounter<long>(
        "koras.dataverse.client.throttles",
        description: "Responses that hit Dataverse service-protection limits (HTTP 429).");
}
