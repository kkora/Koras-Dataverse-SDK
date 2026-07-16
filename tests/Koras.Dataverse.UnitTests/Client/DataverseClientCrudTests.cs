using System.Net;
using Koras.Dataverse.Errors;
using Koras.Dataverse.UnitTests.TestInfrastructure;

namespace Koras.Dataverse.UnitTests.Client;

public class DataverseClientCrudTests
{
    private static readonly Guid RowId = Guid.Parse("0f3f3f3f-3f3f-3f3f-3f3f-3f3f3f3f3f3f");

    [Fact]
    public async Task CreateAsync_posts_to_entity_set_and_reads_id_header()
    {
        var fake = new FakeHttpMessageHandler();
        var created = new HttpResponseMessage(HttpStatusCode.NoContent);
        created.Headers.Add("OData-EntityId", $"https://unittest.crm.dynamics.com/api/data/v9.2/accounts({RowId:D})");
        fake.Enqueue(created);

        using DataverseClient client = ClientFactory.Create(fake);
        var entity = new Entity("account") { ["name"] = "Contoso" };
        Guid id = await client.CreateAsync(entity);

        Assert.Equal(RowId, id);
        Assert.Equal(RowId, entity.Id);
        FakeHttpMessageHandler.RecordedRequest request = Assert.Single(fake.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/data/v9.2/accounts", request.Uri!.AbsolutePath);
        Assert.Contains("\"name\":\"Contoso\"", request.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RetrieveAsync_selects_columns_and_parses_entity()
    {
        var fake = new FakeHttpMessageHandler();
        fake.EnqueueJson(HttpStatusCode.OK, $$"""{"accountid":"{{RowId:D}}","name":"Contoso"}""");

        using DataverseClient client = ClientFactory.Create(fake);
        Entity entity = await client.RetrieveAsync("account", RowId, ColumnSet.Of("name"));

        Assert.Equal(RowId, entity.Id);
        Assert.Equal("Contoso", entity["name"]);
        FakeHttpMessageHandler.RecordedRequest request = Assert.Single(fake.Requests);
        Assert.Equal($"/api/data/v9.2/accounts({RowId:D})", request.Uri!.AbsolutePath);
        Assert.Contains("%24select=name", request.Uri.Query.Replace("$", "%24", StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Contains("odata.include-annotations", request.Headers["Prefer"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateAsync_patches_with_if_match_star()
    {
        var fake = new FakeHttpMessageHandler();
        fake.Enqueue(new HttpResponseMessage(HttpStatusCode.NoContent));

        using DataverseClient client = ClientFactory.Create(fake);
        await client.UpdateAsync(new Entity("account", RowId) { ["name"] = "New" });

        FakeHttpMessageHandler.RecordedRequest request = Assert.Single(fake.Requests);
        Assert.Equal(HttpMethod.Patch, request.Method);
        Assert.Equal("*", request.Headers["If-Match"]);
    }

    [Fact]
    public async Task UpdateAsync_requires_an_id()
    {
        using DataverseClient client = ClientFactory.Create(new FakeHttpMessageHandler());
        await Assert.ThrowsAsync<ArgumentException>(() => client.UpdateAsync(new Entity("account")));
    }

    [Fact]
    public async Task UpsertAsync_reports_created_from_status_code()
    {
        var fake = new FakeHttpMessageHandler();
        fake.Enqueue(FakeHttpMessageHandler.Json(HttpStatusCode.Created, $$"""{"accountid":"{{RowId:D}}"}"""));

        using DataverseClient client = ClientFactory.Create(fake);
        UpsertResult result = await client.UpsertAsync(new Entity("account", RowId) { ["name"] = "x" });

        Assert.True(result.Created);
        Assert.Equal(RowId, result.Id);
        Assert.DoesNotContain("If-Match", fake.Requests[0].Headers.Keys);
    }

    [Fact]
    public async Task UpsertAsync_by_alternate_key_encodes_key_segment()
    {
        var fake = new FakeHttpMessageHandler();
        fake.Enqueue(FakeHttpMessageHandler.Json(HttpStatusCode.OK, $$"""{"accountid":"{{RowId:D}}"}"""));

        using DataverseClient client = ClientFactory.Create(fake);
        UpsertResult result = await client.UpsertAsync(
            new Entity("account") { ["name"] = "x" },
            new Dictionary<string, object> { ["accountnumber"] = "A-100'X" });

        Assert.False(result.Created);
        string path = fake.Requests[0].Uri!.AbsolutePath;
        Assert.StartsWith("/api/data/v9.2/accounts(accountnumber=", path, StringComparison.Ordinal);
        Assert.Contains("A-100''X", Uri.UnescapeDataString(path), StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeleteAsync_targets_the_row()
    {
        var fake = new FakeHttpMessageHandler();
        fake.Enqueue(new HttpResponseMessage(HttpStatusCode.NoContent));

        using DataverseClient client = ClientFactory.Create(fake);
        await client.DeleteAsync(new EntityReference("account", RowId));

        FakeHttpMessageHandler.RecordedRequest request = Assert.Single(fake.Requests);
        Assert.Equal(HttpMethod.Delete, request.Method);
        Assert.Equal($"/api/data/v9.2/accounts({RowId:D})", request.Uri!.AbsolutePath);
    }

    [Fact]
    public async Task Missing_rows_surface_not_found_category()
    {
        var fake = new FakeHttpMessageHandler();
        fake.EnqueueJson(HttpStatusCode.NotFound, """{"error":{"code":"0x80040217","message":"account does not exist"}}""");

        using DataverseClient client = ClientFactory.Create(fake);
        DataverseException exception = await Assert.ThrowsAsync<DataverseException>(
            () => client.RetrieveAsync("account", RowId));

        Assert.Equal(DataverseErrorCategory.NotFound, exception.Category);
        Assert.Equal("0x80040217", exception.Error.ErrorCode);
        Assert.Equal(404, exception.Error.HttpStatusCode);
    }

    [Fact]
    public async Task AssociateAsync_posts_odata_id_ref()
    {
        var fake = new FakeHttpMessageHandler();
        fake.Enqueue(new HttpResponseMessage(HttpStatusCode.NoContent));
        var contactId = Guid.NewGuid();

        using DataverseClient client = ClientFactory.Create(fake);
        await client.AssociateAsync(
            new EntityReference("account", RowId),
            "contact_customer_accounts",
            new EntityReference("contact", contactId));

        FakeHttpMessageHandler.RecordedRequest request = Assert.Single(fake.Requests);
        Assert.EndsWith("/contact_customer_accounts/$ref", request.Uri!.AbsolutePath, StringComparison.Ordinal);
        Assert.Contains($"contacts({contactId:D})", request.Body, StringComparison.Ordinal);
        Assert.Contains("@odata.id", request.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WhoAmIAsync_parses_ids()
    {
        var fake = new FakeHttpMessageHandler();
        fake.EnqueueJson(HttpStatusCode.OK, """
            {"UserId":"11111111-1111-1111-1111-111111111111",
             "BusinessUnitId":"22222222-2222-2222-2222-222222222222",
             "OrganizationId":"33333333-3333-3333-3333-333333333333"}
            """);

        using DataverseClient client = ClientFactory.Create(fake);
        WhoAmIResponse who = await client.WhoAmIAsync();

        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), who.UserId);
        Assert.Equal("/api/data/v9.2/WhoAmI", fake.Requests[0].Uri!.AbsolutePath);
    }

    [Fact]
    public async Task Canceled_token_surfaces_operation_canceled()
    {
        var fake = new FakeHttpMessageHandler();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        using DataverseClient client = ClientFactory.Create(fake);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.RetrieveAsync("account", RowId, null, cts.Token));
    }

    [Fact]
    public async Task Timeout_surfaces_timeout_category()
    {
        var fake = new FakeHttpMessageHandler();
        fake.Enqueue(_ => throw new OperationCanceledException("simulated linked-timeout cancellation"));

        using DataverseClient client = ClientFactory.Create(fake, o => o.Timeout = TimeSpan.FromMilliseconds(1));
        DataverseException exception = await Assert.ThrowsAsync<DataverseException>(
            () => client.RetrieveAsync("account", RowId));

        Assert.Equal(DataverseErrorCategory.Timeout, exception.Category);
        Assert.True(exception.IsTransient);
    }

    [Fact]
    public async Task Network_failures_surface_network_category()
    {
        var fake = new FakeHttpMessageHandler();
        fake.Enqueue(_ => throw new HttpRequestException("dns failure"));

        using DataverseClient client = ClientFactory.Create(fake);
        DataverseException exception = await Assert.ThrowsAsync<DataverseException>(
            () => client.WhoAmIAsync());

        Assert.Equal(DataverseErrorCategory.Network, exception.Category);
        Assert.True(exception.IsTransient);
    }
}
