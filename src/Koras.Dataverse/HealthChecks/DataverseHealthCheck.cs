using Koras.Dataverse.Errors;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Koras.Dataverse.HealthChecks;

/// <summary>
/// A health check that probes Dataverse connectivity and authentication with a <c>WhoAmI</c>
/// call. Register with
/// <c>services.AddHealthChecks().AddCheck&lt;DataverseHealthCheck&gt;("dataverse")</c>, or use
/// <c>AddDataverseHealthCheck()</c> from the DI extensions.
/// </summary>
public sealed class DataverseHealthCheck : IHealthCheck
{
    private readonly IDataverseClient _client;

    /// <summary>Creates the health check for the given client.</summary>
    public DataverseHealthCheck(IDataverseClient client) => _client = client ?? throw new ArgumentNullException(nameof(client));

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            WhoAmIResponse who = await _client.WhoAmIAsync(cancellationToken).ConfigureAwait(false);
            return HealthCheckResult.Healthy(
                "Dataverse is reachable.",
                new Dictionary<string, object> { ["userId"] = who.UserId, ["organizationId"] = who.OrganizationId });
        }
        catch (DataverseException exception)
        {
            return new HealthCheckResult(
                context?.Registration?.FailureStatus ?? HealthStatus.Unhealthy,
                $"Dataverse probe failed: {exception.Category}.",
                exception);
        }
    }
}
