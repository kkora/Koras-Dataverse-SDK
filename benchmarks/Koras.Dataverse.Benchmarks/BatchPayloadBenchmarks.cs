using System.Net;
using System.Net.Http.Headers;
using System.Text;
using BenchmarkDotNet.Attributes;
using Koras.Dataverse.Batches;

namespace Koras.Dataverse.Benchmarks;

/// <summary>
/// BatchRequest → multipart payload and multipart response → BatchResponse
/// (docs/performance/benchmarks.md §2.5, KDV-005). The public surface executes both
/// directions in one round trip against the canned transport, so each benchmark measures
/// assembly + parsing for the given operation count.
/// </summary>
[MemoryDiagnoser]
public class BatchPayloadBenchmarks
{
    [Params(10, 100, 1000)]
    public int Operations { get; set; }

    private DataverseClient _atomicClient = null!;
    private DataverseClient _continueClient = null!;
    private BatchRequest _atomicBatch = null!;
    private BatchRequest _continueBatch = null!;

    [GlobalSetup]
    public void Setup()
    {
        _atomicBatch = new BatchRequest();
        _continueBatch = new BatchRequest { Atomic = false };
        for (int i = 0; i < Operations; i++)
        {
            var row = new Entity("account", Guid.NewGuid()) { ["name"] = "Contoso " + i, ["revenue"] = 100m + i };
            _atomicBatch.AddUpdate(row);
            _continueBatch.AddUpdate(row);
        }

        string atomicResponse = BuildChangeSetResponse(Operations, mixedErrors: false);
        string continueResponse = BuildDirectResponse(Operations, mixedErrors: true);
        _atomicClient = BenchmarkClient.Create(_ => Multipart(atomicResponse));
        _continueClient = BenchmarkClient.Create(_ => Multipart(continueResponse));
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _atomicClient.Dispose();
        _continueClient.Dispose();
    }

    [Benchmark]
    public Task<BatchResponse> AtomicAllSuccess() => _atomicClient.ExecuteBatchAsync(_atomicBatch);

    [Benchmark]
    public Task<BatchResponse> ContinueOnErrorMixed() => _continueClient.ExecuteBatchAsync(_continueBatch);

    private static string BuildChangeSetResponse(int items, bool mixedErrors)
    {
        var body = new StringBuilder(items * 128);
        body.Append("--rsp\r\nContent-Type: multipart/mixed; boundary=cs1\r\n\r\n");
        AppendItems(body, "cs1", items, mixedErrors);
        body.Append("--cs1--\r\n--rsp--\r\n");
        return body.ToString();
    }

    private static string BuildDirectResponse(int items, bool mixedErrors)
    {
        var body = new StringBuilder(items * 128);
        AppendItems(body, "rsp", items, mixedErrors);
        body.Append("--rsp--\r\n");
        return body.ToString();
    }

    private static void AppendItems(StringBuilder body, string boundary, int items, bool mixedErrors)
    {
        for (int i = 0; i < items; i++)
        {
            body.Append("--").Append(boundary).Append("\r\n");
            body.Append("Content-Type: application/http\r\nContent-Transfer-Encoding: binary\r\n\r\n");
            if (mixedErrors && i % 10 == 9)
            {
                body.Append("HTTP/1.1 404 Not Found\r\nContent-Type: application/json\r\n\r\n");
                body.Append("{\"error\":{\"code\":\"0x80040217\",\"message\":\"row missing\"}}\r\n");
            }
            else
            {
                body.Append("HTTP/1.1 204 No Content\r\n\r\n");
            }
        }
    }

    private static HttpResponseMessage Multipart(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8)
        {
            Headers = { ContentType = MediaTypeHeaderValue.Parse("multipart/mixed; boundary=rsp") },
        },
    };
}
