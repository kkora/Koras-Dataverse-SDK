using Koras.Dataverse.Queries;

namespace Koras.Dataverse.IntegrationTests;

/// <summary>
/// Round-trip tests against a real Dataverse environment. Skipped unless the
/// <c>KORAS_DATAVERSE_*</c> environment variables are set. Rows are created with a unique
/// prefix and always deleted, even on assertion failure.
/// </summary>
public class DataverseLiveTests
{
    private const string Prefix = "korassdk-it-";

    [LiveDataverseFact]
    public async Task WhoAmI_returns_caller_identity()
    {
        using DataverseClient client = LiveDataverse.CreateClient();
        WhoAmIResponse who = await client.WhoAmIAsync();
        Assert.NotEqual(Guid.Empty, who.UserId);
        Assert.NotEqual(Guid.Empty, who.OrganizationId);
    }

    [LiveDataverseFact]
    public async Task Account_crud_round_trip()
    {
        using DataverseClient client = LiveDataverse.CreateClient();
        CancellationToken ct = CancellationToken.None;
        string name = Prefix + Guid.NewGuid().ToString("N");

        Guid id = await client.CreateAsync(new Entity("account") { ["name"] = name }, ct);
        try
        {
            Entity fetched = await client.RetrieveAsync("account", id, ColumnSet.Of("name"), ct);
            Assert.Equal(name, fetched["name"]);

            await client.UpdateAsync(new Entity("account", id) { ["name"] = name + "-updated" }, ct);

            DataverseQueryResult page = await client.QueryAsync(
                ODataQuery.For("account").Select("name").Where(f => f.Eq("name", name + "-updated")), ct);
            Assert.Single(page.Entities);
        }
        finally
        {
            await client.DeleteAsync("account", id, ct);
        }
    }

    [LiveDataverseFact]
    public async Task Metadata_reads_account_table()
    {
        using DataverseClient client = LiveDataverse.CreateClient();
        CancellationToken ct = CancellationToken.None;

        Metadata.TableMetadata table = await client.Metadata.GetTableAsync("account", ct);
        Assert.Equal("accounts", table.EntitySetName);
        Assert.Equal("accountid", table.PrimaryIdAttribute);

        IReadOnlyList<Metadata.ColumnMetadata> columns = await client.Metadata.GetColumnsAsync("account", ct);
        Assert.Contains(columns, c => c.LogicalName == "name");
    }
}
