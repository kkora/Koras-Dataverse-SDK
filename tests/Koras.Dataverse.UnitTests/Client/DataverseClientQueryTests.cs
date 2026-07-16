using System.Net;
using Koras.Dataverse.Queries;
using Koras.Dataverse.UnitTests.TestInfrastructure;

namespace Koras.Dataverse.UnitTests.Client;

public class DataverseClientQueryTests
{
    [Fact]
    public async Task QueryAsync_returns_page_with_next_link_and_count()
    {
        var fake = new FakeHttpMessageHandler();
        fake.EnqueueJson(HttpStatusCode.OK, """
            {"@odata.count":42,
             "value":[{"accountid":"11111111-1111-1111-1111-111111111111","name":"A"},
                      {"accountid":"22222222-2222-2222-2222-222222222222","name":"B"}],
             "@odata.nextLink":"https://unittest.crm.dynamics.com/api/data/v9.2/accounts?$skiptoken=x"}
            """);

        using DataverseClient client = ClientFactory.Create(fake);
        DataverseQueryResult page = await client.QueryAsync(ODataQuery.For("account").Select("name").IncludeCount());

        Assert.Equal(2, page.Entities.Count);
        Assert.Equal("A", page.Entities[0]["name"]);
        Assert.Equal(42, page.TotalCount);
        Assert.True(page.MoreRecords);
        Assert.NotNull(page.NextLink);
    }

    [Fact]
    public async Task QueryAsync_sends_maxpagesize_preference()
    {
        var fake = new FakeHttpMessageHandler();
        fake.EnqueueJson(HttpStatusCode.OK, """{"value":[]}""");

        using DataverseClient client = ClientFactory.Create(fake);
        await client.QueryAsync(ODataQuery.For("account").PageSize(50));

        Assert.Contains("odata.maxpagesize=50", fake.Requests[0].Headers["Prefer"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryAllAsync_follows_next_links()
    {
        var fake = new FakeHttpMessageHandler();
        fake.EnqueueJson(HttpStatusCode.OK, """
            {"value":[{"accountid":"11111111-1111-1111-1111-111111111111","name":"A"}],
             "@odata.nextLink":"https://unittest.crm.dynamics.com/api/data/v9.2/accounts?$skiptoken=page2"}
            """);
        fake.EnqueueJson(HttpStatusCode.OK, """
            {"value":[{"accountid":"22222222-2222-2222-2222-222222222222","name":"B"}]}
            """);

        using DataverseClient client = ClientFactory.Create(fake);
        List<string?> names = new();
        await foreach (Entity entity in client.QueryAllAsync(ODataQuery.For("account").Select("name")))
        {
            names.Add(entity.GetValue<string>("name"));
        }

        Assert.Equal(new[] { "A", "B" }, names);
        Assert.Equal(2, fake.Requests.Count);
        Assert.Contains("skiptoken=page2", fake.Requests[1].Uri!.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FetchAsync_sends_fetchxml_and_extracts_paging_cookie()
    {
        var fake = new FakeHttpMessageHandler();
        // The cookie annotation double-encodes the inner cookie, as Dataverse does.
        string annotation = "<cookie pagenumber=\"2\" pagingcookie=\"" +
            Uri.EscapeDataString(Uri.EscapeDataString("<cookie page=\"1\"><accountid last=\"{X}\"/></cookie>")) +
            "\" istracking=\"False\" />";
        fake.EnqueueJson(HttpStatusCode.OK, $$"""
            {"value":[{"accountid":"11111111-1111-1111-1111-111111111111"}],
             "@Microsoft.Dynamics.CRM.morerecords":true,
             "@Microsoft.Dynamics.CRM.fetchxmlpagingcookie":{{System.Text.Json.JsonSerializer.Serialize(annotation)}}}
            """);

        using DataverseClient client = ClientFactory.Create(fake);
        var query = Dataverse.FetchXml.FetchXml.For("account").Attributes("name").Build();
        DataverseQueryResult page = await client.FetchAsync(query);

        Assert.True(page.MoreRecords);
        Assert.Equal("<cookie page=\"1\"><accountid last=\"{X}\"/></cookie>", page.PagingCookie);
        Assert.Contains("fetchXml=", fake.Requests[0].Uri!.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FetchAllAsync_pages_until_morerecords_is_false()
    {
        var fake = new FakeHttpMessageHandler();
        string cookieAnnotation = "<cookie pagenumber=\"2\" pagingcookie=\"" +
            Uri.EscapeDataString(Uri.EscapeDataString("<c/>")) + "\" istracking=\"False\" />";
        fake.EnqueueJson(HttpStatusCode.OK, $$"""
            {"value":[{"accountid":"11111111-1111-1111-1111-111111111111"}],
             "@Microsoft.Dynamics.CRM.morerecords":true,
             "@Microsoft.Dynamics.CRM.fetchxmlpagingcookie":{{System.Text.Json.JsonSerializer.Serialize(cookieAnnotation)}}}
            """);
        fake.EnqueueJson(HttpStatusCode.OK, """
            {"value":[{"accountid":"22222222-2222-2222-2222-222222222222"}]}
            """);

        using DataverseClient client = ClientFactory.Create(fake);
        var query = Dataverse.FetchXml.FetchXml.For("account").Attributes("name").Build();

        int count = 0;
        await foreach (Entity _ in client.FetchAllAsync(query, pageSize: 1))
        {
            count++;
        }

        Assert.Equal(2, count);
        Assert.Equal(2, fake.Requests.Count);

        // Second request must carry page=2 and the decoded cookie re-encoded into the fetch.
        string secondFetch = Uri.UnescapeDataString(fake.Requests[1].Uri!.Query);
        Assert.Contains("page=\"2\"", secondFetch, StringComparison.Ordinal);
        Assert.Contains("paging-cookie", secondFetch, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtractPagingCookie_handles_missing_and_malformed_values()
    {
        Assert.Null(DataverseClient.ExtractPagingCookie(null));
        Assert.Null(DataverseClient.ExtractPagingCookie(""));
        Assert.Null(DataverseClient.ExtractPagingCookie("not xml"));
        Assert.Null(DataverseClient.ExtractPagingCookie("<cookie pagenumber=\"2\" istracking=\"False\" />"));
    }
}
