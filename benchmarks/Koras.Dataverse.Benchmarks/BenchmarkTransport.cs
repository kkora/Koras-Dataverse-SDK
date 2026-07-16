using Koras.Dataverse.Authentication;

namespace Koras.Dataverse.Benchmarks;

/// <summary>
/// In-memory transport for pipeline benchmarks: canned responses, no sockets, static token.
/// Everything goes through the public <see cref="DataverseClient"/> constructor so benchmarks
/// measure the real handler chain (per docs/performance/benchmarks.md §1).
/// </summary>
internal sealed class CannedResponseHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(responder(request));
}

internal sealed class StaticTokenProvider : IDataverseTokenProvider
{
    public ValueTask<string> GetAccessTokenAsync(Uri environmentUrl, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult("benchmark-token");
}

internal static class BenchmarkClient
{
    public static readonly Uri EnvironmentUrl = new("https://benchmark.crm.dynamics.com");

    public static DataverseClient Create(
        Func<HttpRequestMessage, HttpResponseMessage> responder,
        Action<DataverseClientOptions>? configure = null)
    {
        var options = new DataverseClientOptions { EnvironmentUrl = EnvironmentUrl };
        options.Authentication.UseTokenProvider(new StaticTokenProvider());
        configure?.Invoke(options);
        var httpClient = new HttpClient(new CannedResponseHandler(responder), disposeHandler: true);
        return new DataverseClient(httpClient, options);
    }
}
