using System.Net;
using System.Text;
using BenchmarkDotNet.Attributes;
using Koras.Dataverse.Errors;

namespace Koras.Dataverse.Benchmarks;

/// <summary>
/// Non-success response body → <see cref="DataverseError"/> (docs/performance/benchmarks.md §2.4,
/// KDV-009). Measured through the public client path (throwing <see cref="DataverseException"/>),
/// which is the only supported entry to the parser; retries are disabled so the 429 case measures
/// classification, not backoff.
/// </summary>
[MemoryDiagnoser]
public class ErrorParsingBenchmarks
{
    private static readonly string Standard1KbBody =
        "{\"error\":{\"code\":\"0x80040265\",\"message\":\"" + new string('x', 900) + "\"}}";

    private static readonly string NestedBody = """
        {"error":{"code":"0x80048d19","message":"Outer validation failure.",
          "innererror":{"message":"Middle: attribute rejected.","type":"System.ServiceModel.FaultException",
            "innererror":{"message":"Inner: value out of range.","type":"Microsoft.Xrm.Sdk.InvalidPluginExecutionException",
              "innererror":{"message":"Deepest cause.","type":"System.ArgumentOutOfRangeException"}}}}}
        """;

    private static readonly string Large50KbBody =
        "{\"error\":{\"code\":\"0x80040265\",\"message\":\"" + new string('y', 50_000) + "\"}}";

    private static readonly string HtmlBody =
        "<!DOCTYPE html><html><head><title>502 Bad Gateway</title></head><body><h1>Bad Gateway</h1>" +
        new string('z', 2_000) + "</body></html>";

    private DataverseClient _standard = null!;
    private DataverseClient _nested = null!;
    private DataverseClient _large = null!;
    private DataverseClient _html = null!;
    private DataverseClient _throttled = null!;

    [GlobalSetup]
    public void Setup()
    {
        static Action<DataverseClientOptions> NoRetry() => o => o.Retry.MaxRetries = 0;

        _standard = BenchmarkClient.Create(_ => Json(HttpStatusCode.BadRequest, Standard1KbBody), NoRetry());
        _nested = BenchmarkClient.Create(_ => Json(HttpStatusCode.BadRequest, NestedBody), NoRetry());
        _large = BenchmarkClient.Create(_ => Json(HttpStatusCode.BadRequest, Large50KbBody), NoRetry());
        _html = BenchmarkClient.Create(_ => Html(HttpStatusCode.BadGateway, HtmlBody), NoRetry());
        _throttled = BenchmarkClient.Create(_ =>
        {
            HttpResponseMessage response = Json((HttpStatusCode)429,
                """{"error":{"code":"0x80072322","message":"Number of requests exceeded the limit of 6000 over time window of 300 seconds."}}""");
            response.Headers.TryAddWithoutValidation("Retry-After", "30");
            return response;
        }, NoRetry());
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _standard.Dispose();
        _nested.Dispose();
        _large.Dispose();
        _html.Dispose();
        _throttled.Dispose();
    }

    [Benchmark]
    public Task<DataverseError> StandardPayload1Kb() => CatchAsync(_standard);

    [Benchmark]
    public Task<DataverseError> NestedInnerErrorsDepth3() => CatchAsync(_nested);

    [Benchmark]
    public Task<DataverseError> LargePayload50Kb() => CatchAsync(_large);

    [Benchmark]
    public Task<DataverseError> NonJsonHtmlFallback() => CatchAsync(_html);

    [Benchmark]
    public Task<DataverseError> ServiceProtection429RetryAfter() => CatchAsync(_throttled);

    private static async Task<DataverseError> CatchAsync(DataverseClient client)
    {
        try
        {
            await client.WhoAmIAsync().ConfigureAwait(false);
            throw new InvalidOperationException("Expected a DataverseException.");
        }
        catch (DataverseException exception)
        {
            return exception.Error;
        }
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) => new(status)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json"),
    };

    private static HttpResponseMessage Html(HttpStatusCode status, string body) => new(status)
    {
        Content = new StringContent(body, Encoding.UTF8, "text/html"),
    };
}
