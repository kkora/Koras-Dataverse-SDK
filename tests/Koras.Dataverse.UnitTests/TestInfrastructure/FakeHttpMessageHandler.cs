using System.Net;
using System.Text;

namespace Koras.Dataverse.UnitTests.TestInfrastructure;

/// <summary>Scripted HTTP handler: enqueue responses, inspect recorded requests.</summary>
public sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new();

    public List<RecordedRequest> Requests { get; } = new();

    public void Enqueue(HttpResponseMessage response) => _responses.Enqueue(_ => response);

    public void Enqueue(Func<HttpRequestMessage, HttpResponseMessage> factory) => _responses.Enqueue(factory);

    public void EnqueueJson(HttpStatusCode statusCode, string json) => Enqueue(Json(statusCode, json));

    public static HttpResponseMessage Json(HttpStatusCode statusCode, string json) => new(statusCode)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string? body = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add(new RecordedRequest(request.Method, request.RequestUri, body, request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value))));

        if (_responses.Count == 0)
        {
            throw new InvalidOperationException($"No scripted response for {request.Method} {request.RequestUri}.");
        }

        return _responses.Dequeue()(request);
    }

    public sealed record RecordedRequest(HttpMethod Method, Uri? Uri, string? Body, IReadOnlyDictionary<string, string> Headers);
}
