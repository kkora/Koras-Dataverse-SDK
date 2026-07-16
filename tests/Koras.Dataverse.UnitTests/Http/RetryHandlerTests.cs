using System.Net;
using Koras.Dataverse.Http;
using Koras.Dataverse.UnitTests.TestInfrastructure;

namespace Koras.Dataverse.UnitTests.Http;

public class RetryHandlerTests
{
    private static HttpClient Client(FakeHttpMessageHandler fake, DataverseRetryOptions options, FakeTimeProvider? time = null)
    {
        var retry = new RetryHandler(options, time ?? new FakeTimeProvider()) { InnerHandler = fake };
        return new HttpClient(retry) { BaseAddress = new Uri("https://unittest.crm.dynamics.com/") };
    }

    [Fact]
    public async Task Retries_429_and_succeeds()
    {
        var fake = new FakeHttpMessageHandler();
        fake.Enqueue(new HttpResponseMessage((HttpStatusCode)429));
        fake.Enqueue(new HttpResponseMessage(HttpStatusCode.OK));

        using HttpClient client = Client(fake, new DataverseRetryOptions());
        HttpResponseMessage response = await client.GetAsync(new Uri("x", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, fake.Requests.Count);
    }

    [Fact]
    public async Task Honors_retry_after_header()
    {
        var time = new FakeTimeProvider();
        var fake = new FakeHttpMessageHandler();
        var throttled = new HttpResponseMessage((HttpStatusCode)429);
        throttled.Headers.Add("Retry-After", "7");
        fake.Enqueue(throttled);
        fake.Enqueue(new HttpResponseMessage(HttpStatusCode.OK));

        using HttpClient client = Client(fake, new DataverseRetryOptions(), time);
        await client.GetAsync(new Uri("x", UriKind.Relative));

        TimeSpan delay = Assert.Single(time.RequestedDelays);
        Assert.Equal(TimeSpan.FromSeconds(7), delay);
    }

    [Fact]
    public async Task Does_not_retry_client_errors()
    {
        var fake = new FakeHttpMessageHandler();
        fake.Enqueue(new HttpResponseMessage(HttpStatusCode.BadRequest));

        using HttpClient client = Client(fake, new DataverseRetryOptions());
        HttpResponseMessage response = await client.GetAsync(new Uri("x", UriKind.Relative));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Single(fake.Requests);
    }

    [Fact]
    public async Task Stops_after_max_retries_and_returns_last_response()
    {
        var fake = new FakeHttpMessageHandler();
        for (int i = 0; i < 3; i++)
        {
            fake.Enqueue(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        }

        using HttpClient client = Client(fake, new DataverseRetryOptions { MaxRetries = 2 });
        HttpResponseMessage response = await client.GetAsync(new Uri("x", UriKind.Relative));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(3, fake.Requests.Count); // initial + 2 retries
    }

    [Fact]
    public async Task Retries_network_failures_then_rethrows()
    {
        var fake = new FakeHttpMessageHandler();
        fake.Enqueue(_ => throw new HttpRequestException("socket reset"));
        fake.Enqueue(_ => throw new HttpRequestException("socket reset"));

        using HttpClient client = Client(fake, new DataverseRetryOptions { MaxRetries = 1 });
        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync(new Uri("x", UriKind.Relative)));
        Assert.Equal(2, fake.Requests.Count);
    }

    [Fact]
    public async Task Request_content_is_replayed_on_retry()
    {
        var fake = new FakeHttpMessageHandler();
        fake.Enqueue(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        fake.Enqueue(new HttpResponseMessage(HttpStatusCode.OK));

        using HttpClient client = Client(fake, new DataverseRetryOptions());
        using var content = new StringContent("{\"name\":\"x\"}", System.Text.Encoding.UTF8, "application/json");
        await client.PostAsync(new Uri("x", UriKind.Relative), content);

        Assert.Equal(2, fake.Requests.Count);
        Assert.Equal(fake.Requests[0].Body, fake.Requests[1].Body);
    }

    [Fact]
    public async Task Zero_max_retries_disables_retrying()
    {
        var fake = new FakeHttpMessageHandler();
        fake.Enqueue(new HttpResponseMessage((HttpStatusCode)429));

        using HttpClient client = Client(fake, new DataverseRetryOptions { MaxRetries = 0 });
        HttpResponseMessage response = await client.GetAsync(new Uri("x", UriKind.Relative));

        Assert.Equal((HttpStatusCode)429, response.StatusCode);
        Assert.Single(fake.Requests);
    }

    [Fact]
    public async Task Cancellation_stops_the_pipeline()
    {
        var fake = new FakeHttpMessageHandler();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        using HttpClient client = Client(fake, new DataverseRetryOptions());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.GetAsync(new Uri("x", UriKind.Relative), cts.Token));
    }
}
