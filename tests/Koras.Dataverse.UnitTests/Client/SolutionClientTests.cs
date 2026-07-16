using System.Net;
using System.Text.Json;
using Koras.Dataverse.Errors;
using Koras.Dataverse.Solutions;
using Koras.Dataverse.UnitTests.TestInfrastructure;

namespace Koras.Dataverse.UnitTests.Client;

public class SolutionClientTests
{
    [Fact]
    public async Task ExportAsync_posts_action_and_decodes_zip()
    {
        byte[] zip = [0x50, 0x4B, 0x03, 0x04, 0x01];
        var fake = new FakeHttpMessageHandler();
        fake.EnqueueJson(HttpStatusCode.OK, $$"""{"ExportSolutionFile":"{{Convert.ToBase64String(zip)}}"}""");

        using DataverseClient client = ClientFactory.Create(fake);
        byte[] result = await client.Solutions.ExportAsync("MySolution", managed: true);

        Assert.Equal(zip, result);
        FakeHttpMessageHandler.RecordedRequest request = Assert.Single(fake.Requests);
        Assert.EndsWith("/ExportSolution", request.Uri!.AbsolutePath, StringComparison.Ordinal);
        using JsonDocument body = JsonDocument.Parse(request.Body!);
        Assert.Equal("MySolution", body.RootElement.GetProperty("SolutionName").GetString());
        Assert.True(body.RootElement.GetProperty("Managed").GetBoolean());
    }

    [Fact]
    public async Task ExportAsync_without_file_in_response_fails_clearly()
    {
        var fake = new FakeHttpMessageHandler();
        fake.EnqueueJson(HttpStatusCode.OK, "{}");

        using DataverseClient client = ClientFactory.Create(fake);
        DataverseException exception = await Assert.ThrowsAsync<DataverseException>(
            () => client.Solutions.ExportAsync("MySolution"));
        Assert.Contains("ExportSolutionFile", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ImportAsync_sends_options_and_generates_job_id()
    {
        var fake = new FakeHttpMessageHandler();
        fake.Enqueue(new HttpResponseMessage(HttpStatusCode.NoContent));

        using DataverseClient client = ClientFactory.Create(fake);
        await client.Solutions.ImportAsync(new byte[] { 1, 2, 3 }, new SolutionImportOptions
        {
            OverwriteUnmanagedCustomizations = true,
            PublishWorkflows = false,
        });

        using JsonDocument body = JsonDocument.Parse(fake.Requests[0].Body!);
        Assert.True(body.RootElement.GetProperty("OverwriteUnmanagedCustomizations").GetBoolean());
        Assert.False(body.RootElement.GetProperty("PublishWorkflows").GetBoolean());
        Assert.Equal(Convert.ToBase64String(new byte[] { 1, 2, 3 }), body.RootElement.GetProperty("CustomizationFile").GetString());
        Assert.NotEqual(Guid.Empty, Guid.Parse(body.RootElement.GetProperty("ImportJobId").GetString()!));
    }

    [Fact]
    public async Task ImportAsync_rejects_empty_zip()
    {
        using DataverseClient client = ClientFactory.Create(new FakeHttpMessageHandler());
        await Assert.ThrowsAsync<ArgumentException>(() => client.Solutions.ImportAsync(ReadOnlyMemory<byte>.Empty));
    }

    [Fact]
    public async Task PublishAllAsync_posts_the_action()
    {
        var fake = new FakeHttpMessageHandler();
        fake.Enqueue(new HttpResponseMessage(HttpStatusCode.NoContent));

        using DataverseClient client = ClientFactory.Create(fake);
        await client.Solutions.PublishAllAsync();

        Assert.EndsWith("/PublishAllXml", fake.Requests[0].Uri!.AbsolutePath, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FindAsync_maps_solution_info_and_encodes_filter()
    {
        var fake = new FakeHttpMessageHandler();
        fake.EnqueueJson(HttpStatusCode.OK, """
            {"value":[{"solutionid":"11111111-1111-1111-1111-111111111111",
                       "uniquename":"MySolution","friendlyname":"My Solution",
                       "version":"1.2.0.0","ismanaged":true,"installedon":"2026-01-31T08:30:00Z"}]}
            """);

        using DataverseClient client = ClientFactory.Create(fake);
        SolutionInfo? info = await client.Solutions.FindAsync("MySolution");

        Assert.NotNull(info);
        Assert.Equal("MySolution", info.UniqueName);
        Assert.True(info.IsManaged);
        Assert.Equal("1.2.0.0", info.Version);
        Assert.Contains("uniquename%20eq%20%27MySolution%27", fake.Requests[0].Uri!.Query, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FindAsync_returns_null_when_not_installed()
    {
        var fake = new FakeHttpMessageHandler();
        fake.EnqueueJson(HttpStatusCode.OK, """{"value":[]}""");

        using DataverseClient client = ClientFactory.Create(fake);
        Assert.Null(await client.Solutions.FindAsync("Missing"));
    }
}
