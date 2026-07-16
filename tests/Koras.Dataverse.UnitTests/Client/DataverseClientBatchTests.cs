using System.Net;
using System.Text;
using Koras.Dataverse.Batches;
using Koras.Dataverse.Errors;
using Koras.Dataverse.UnitTests.TestInfrastructure;

namespace Koras.Dataverse.UnitTests.Client;

public class DataverseClientBatchTests
{
    private static readonly Guid Id1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Id2 = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static HttpResponseMessage MultipartResponse(string boundary, string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8)
        {
            Headers = { ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse($"multipart/mixed; boundary={boundary}") },
        },
    };

    [Fact]
    public async Task Atomic_batch_wraps_operations_in_one_changeset()
    {
        var fake = new FakeHttpMessageHandler();
        fake.Enqueue(request => MultipartResponse("rsp", BuildSuccessResponse()));

        using DataverseClient client = ClientFactory.Create(fake);
        var batch = new BatchRequest()
            .AddCreate(new Entity("account") { ["name"] = "A" })
            .AddUpdate(new Entity("account", Id1) { ["name"] = "B" })
            .AddDelete("account", Id2);

        BatchResponse response = await client.ExecuteBatchAsync(batch);

        Assert.True(response.Succeeded);
        string body = fake.Requests[0].Body!;
        Assert.Contains("Content-Type: multipart/mixed; boundary=changeset_", body, StringComparison.Ordinal);
        Assert.Contains("Content-ID: 1", body, StringComparison.Ordinal);
        Assert.Contains("POST https://unittest.crm.dynamics.com/api/data/v9.2/accounts HTTP/1.1", body, StringComparison.Ordinal);
        Assert.Contains($"PATCH https://unittest.crm.dynamics.com/api/data/v9.2/accounts({Id1:D}) HTTP/1.1", body, StringComparison.Ordinal);
        Assert.Contains("If-Match: *", body, StringComparison.Ordinal);
        Assert.Contains($"DELETE https://unittest.crm.dynamics.com/api/data/v9.2/accounts({Id2:D}) HTTP/1.1", body, StringComparison.Ordinal);
        Assert.False(fake.Requests[0].Headers.ContainsKey("Prefer"));
    }

    [Fact]
    public async Task NonAtomic_batch_sends_continue_on_error_and_skips_changeset()
    {
        var fake = new FakeHttpMessageHandler();
        fake.Enqueue(_ => MultipartResponse("rsp", BuildSuccessResponse()));

        using DataverseClient client = ClientFactory.Create(fake);
        var batch = new BatchRequest { Atomic = false }
            .AddCreate(new Entity("account") { ["name"] = "A" });
        await client.ExecuteBatchAsync(batch);

        Assert.Contains("odata.continue-on-error", fake.Requests[0].Headers["Prefer"], StringComparison.Ordinal);
        Assert.DoesNotContain("changeset_", fake.Requests[0].Body!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Batch_parses_per_item_results()
    {
        string responseBody =
            "--rsp\r\n" +
            "Content-Type: multipart/mixed; boundary=cs1\r\n\r\n" +
            "--cs1\r\n" +
            "Content-Type: application/http\r\n" +
            "Content-Transfer-Encoding: binary\r\n" +
            "Content-ID: 1\r\n\r\n" +
            $"HTTP/1.1 204 No Content\r\nOData-EntityId: https://unittest.crm.dynamics.com/api/data/v9.2/accounts({Id1:D})\r\n\r\n" +
            "--cs1--\r\n" +
            "--rsp--\r\n";

        var fake = new FakeHttpMessageHandler();
        fake.Enqueue(_ => MultipartResponse("rsp", responseBody));

        using DataverseClient client = ClientFactory.Create(fake);
        BatchResponse response = await client.ExecuteBatchAsync(new BatchRequest().AddCreate(new Entity("account")));

        BatchItemResult item = Assert.Single(response.Results);
        Assert.True(item.Succeeded);
        Assert.Equal(204, item.StatusCode);
        Assert.Equal(Id1, item.CreatedId);
    }

    [Fact]
    public async Task Atomic_failure_throws_with_the_inner_error()
    {
        string responseBody =
            "--rsp\r\n" +
            "Content-Type: application/http\r\n" +
            "Content-Transfer-Encoding: binary\r\n\r\n" +
            "HTTP/1.1 400 Bad Request\r\nContent-Type: application/json\r\n\r\n" +
            "{\"error\":{\"code\":\"0x80048d19\",\"message\":\"Invalid attribute\"}}\r\n" +
            "--rsp--\r\n";

        var fake = new FakeHttpMessageHandler();
        fake.Enqueue(_ => MultipartResponse("rsp", responseBody));

        using DataverseClient client = ClientFactory.Create(fake);
        DataverseException exception = await Assert.ThrowsAsync<DataverseException>(
            () => client.ExecuteBatchAsync(new BatchRequest().AddCreate(new Entity("account"))));

        Assert.Equal(DataverseErrorCategory.Validation, exception.Category);
        Assert.Equal("0x80048d19", exception.Error.ErrorCode);
    }

    [Fact]
    public async Task NonAtomic_failures_are_reported_per_item()
    {
        string responseBody =
            "--rsp\r\n" +
            "Content-Type: application/http\r\n" +
            "Content-Transfer-Encoding: binary\r\n\r\n" +
            $"HTTP/1.1 204 No Content\r\nOData-EntityId: https://unittest.crm.dynamics.com/api/data/v9.2/accounts({Id1:D})\r\n\r\n" +
            "--rsp\r\n" +
            "Content-Type: application/http\r\n" +
            "Content-Transfer-Encoding: binary\r\n\r\n" +
            "HTTP/1.1 404 Not Found\r\nContent-Type: application/json\r\n\r\n" +
            "{\"error\":{\"code\":\"0x80040217\",\"message\":\"row missing\"}}\r\n" +
            "--rsp--\r\n";

        var fake = new FakeHttpMessageHandler();
        fake.Enqueue(_ => MultipartResponse("rsp", responseBody));

        using DataverseClient client = ClientFactory.Create(fake);
        var batch = new BatchRequest { Atomic = false }
            .AddCreate(new Entity("account"))
            .AddDelete("account", Id2);

        BatchResponse response = await client.ExecuteBatchAsync(batch);

        Assert.False(response.Succeeded);
        Assert.Equal(2, response.Results.Count);
        Assert.True(response.Results[0].Succeeded);
        Assert.False(response.Results[1].Succeeded);
        Assert.Equal(DataverseErrorCategory.NotFound, response.Results[1].Error!.Category);
    }

    [Fact]
    public async Task Empty_batches_are_rejected()
    {
        using DataverseClient client = ClientFactory.Create(new FakeHttpMessageHandler());
        await Assert.ThrowsAsync<ArgumentException>(() => client.ExecuteBatchAsync(new BatchRequest()));
    }

    [Fact]
    public void Batch_request_enforces_limits_and_preconditions()
    {
        var batch = new BatchRequest();
        Assert.Throws<ArgumentException>(() => batch.AddUpdate(new Entity("account")));  // no id
        Assert.Throws<ArgumentException>(() => batch.AddUpsert(new Entity("account"))); // no id
    }

    private static string BuildSuccessResponse() =>
        "--rsp\r\n" +
        "Content-Type: multipart/mixed; boundary=cs1\r\n\r\n" +
        "--cs1\r\n" +
        "Content-Type: application/http\r\n" +
        "Content-Transfer-Encoding: binary\r\n" +
        "Content-ID: 1\r\n\r\n" +
        "HTTP/1.1 204 No Content\r\n\r\n" +
        "--cs1--\r\n" +
        "--rsp--\r\n";
}
