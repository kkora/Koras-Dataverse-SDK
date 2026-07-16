using Koras.Dataverse.Errors;
using Koras.Dataverse.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Koras.Dataverse.UnitTests.HealthChecks;

public class DataverseHealthCheckTests
{
    private static HealthCheckContext Context() => new()
    {
        Registration = new HealthCheckRegistration("dataverse", Substitute.For<IHealthCheck>(), HealthStatus.Unhealthy, null),
    };

    [Fact]
    public async Task Healthy_when_whoami_succeeds()
    {
        var client = Substitute.For<IDataverseClient>();
        client.WhoAmIAsync(Arg.Any<CancellationToken>())
            .Returns(new WhoAmIResponse(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));

        HealthCheckResult result = await new DataverseHealthCheck(client).CheckHealthAsync(Context());

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.True(result.Data.ContainsKey("userId"));
    }

    [Fact]
    public async Task Unhealthy_when_dataverse_fails()
    {
        var client = Substitute.For<IDataverseClient>();
        client.WhoAmIAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new DataverseException(new DataverseError
        {
            Category = DataverseErrorCategory.Authentication,
            Message = "401",
        }));

        HealthCheckResult result = await new DataverseHealthCheck(client).CheckHealthAsync(Context());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("Authentication", result.Description, StringComparison.Ordinal);
    }
}
