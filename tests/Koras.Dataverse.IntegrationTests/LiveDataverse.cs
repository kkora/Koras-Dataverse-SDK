namespace Koras.Dataverse.IntegrationTests;

/// <summary>
/// A fact that runs only when live Dataverse credentials are present in the environment:
/// <c>KORAS_DATAVERSE_URL</c>, <c>KORAS_DATAVERSE_TENANT_ID</c>, <c>KORAS_DATAVERSE_CLIENT_ID</c>,
/// <c>KORAS_DATAVERSE_CLIENT_SECRET</c>. Otherwise the test is skipped, keeping CI green without
/// secrets. See docs/testing/integration-testing.md.
/// </summary>
public sealed class LiveDataverseFactAttribute : FactAttribute
{
    public LiveDataverseFactAttribute()
    {
        if (LiveDataverse.Url is null || LiveDataverse.TenantId is null || LiveDataverse.ClientId is null || LiveDataverse.ClientSecret is null)
        {
            Skip = "Live Dataverse environment variables (KORAS_DATAVERSE_*) are not configured.";
        }
    }
}

/// <summary>Reads the live-environment settings and builds clients for integration tests.</summary>
public static class LiveDataverse
{
    public static string? Url => Environment.GetEnvironmentVariable("KORAS_DATAVERSE_URL");

    public static string? TenantId => Environment.GetEnvironmentVariable("KORAS_DATAVERSE_TENANT_ID");

    public static string? ClientId => Environment.GetEnvironmentVariable("KORAS_DATAVERSE_CLIENT_ID");

    public static string? ClientSecret => Environment.GetEnvironmentVariable("KORAS_DATAVERSE_CLIENT_SECRET");

    public static DataverseClient CreateClient() => DataverseClient.Create(new DataverseClientOptions
    {
        EnvironmentUrl = new Uri(Url!),
    }.Also(o => o.Authentication.UseClientSecret(TenantId!, ClientId!, ClientSecret!)));

    private static DataverseClientOptions Also(this DataverseClientOptions options, Action<DataverseClientOptions> configure)
    {
        configure(options);
        return options;
    }
}
