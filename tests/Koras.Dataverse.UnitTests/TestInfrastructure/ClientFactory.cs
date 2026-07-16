namespace Koras.Dataverse.UnitTests.TestInfrastructure;

/// <summary>Builds a <see cref="DataverseClient"/> wired to a fake HTTP handler.</summary>
public static class ClientFactory
{
    public static readonly Uri EnvironmentUrl = new("https://unittest.crm.dynamics.com");

    public static DataverseClient Create(FakeHttpMessageHandler handler, Action<DataverseClientOptions>? configure = null)
    {
        var options = new DataverseClientOptions { EnvironmentUrl = EnvironmentUrl };
        options.Authentication.UseTokenProvider(new FakeTokenProvider());
        configure?.Invoke(options);
        var httpClient = new HttpClient(handler, disposeHandler: false);
        return new DataverseClient(httpClient, options);
    }

    public sealed class FakeTokenProvider : Koras.Dataverse.Authentication.IDataverseTokenProvider
    {
        public ValueTask<string> GetAccessTokenAsync(Uri environmentUrl, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult("fake-token");
    }
}
