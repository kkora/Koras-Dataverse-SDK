using System.Net;
using Koras.Dataverse.Metadata;
using Koras.Dataverse.UnitTests.TestInfrastructure;

namespace Koras.Dataverse.UnitTests.Client;

public class MetadataClientTests
{
    [Fact]
    public async Task GetTableAsync_parses_entity_definition()
    {
        var fake = new FakeHttpMessageHandler();
        fake.EnqueueJson(HttpStatusCode.OK, """
            {"MetadataId":"11111111-1111-1111-1111-111111111111",
             "LogicalName":"account","SchemaName":"Account","EntitySetName":"accounts",
             "PrimaryIdAttribute":"accountid","PrimaryNameAttribute":"name",
             "IsCustomEntity":false,
             "DisplayName":{"UserLocalizedLabel":{"Label":"Account"}}}
            """);

        using DataverseClient client = ClientFactory.Create(fake);
        TableMetadata table = await client.Metadata.GetTableAsync("account");

        Assert.Equal("account", table.LogicalName);
        Assert.Equal("accounts", table.EntitySetName);
        Assert.Equal("Account", table.DisplayName);
        Assert.Equal("accountid", table.PrimaryIdAttribute);
        Assert.False(table.IsCustom);
        Assert.Contains("EntityDefinitions(LogicalName='account')", fake.Requests[0].Uri!.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetColumnsAsync_parses_attributes_and_managed_property_bools()
    {
        var fake = new FakeHttpMessageHandler();
        fake.EnqueueJson(HttpStatusCode.OK, """
            {"value":[
              {"MetadataId":"11111111-1111-1111-1111-111111111111","LogicalName":"name",
               "SchemaName":"Name","AttributeType":"String",
               "RequiredLevel":{"Value":"ApplicationRequired"},
               "IsCustomAttribute":false,"IsPrimaryId":false,"IsPrimaryName":true,
               "DisplayName":{"UserLocalizedLabel":{"Label":"Account Name"}}},
              {"LogicalName":"new_custom","AttributeType":"Picklist","IsCustomAttribute":true}
            ]}
            """);

        using DataverseClient client = ClientFactory.Create(fake);
        IReadOnlyList<ColumnMetadata> columns = await client.Metadata.GetColumnsAsync("account");

        Assert.Equal(2, columns.Count);
        Assert.Equal("Account Name", columns[0].DisplayName);
        Assert.Equal("ApplicationRequired", columns[0].RequiredLevel);
        Assert.True(columns[0].IsPrimaryName);
        Assert.True(columns[1].IsCustom);
    }

    [Fact]
    public async Task GetRelationshipsAsync_merges_all_three_kinds()
    {
        var fake = new FakeHttpMessageHandler();
        fake.EnqueueJson(HttpStatusCode.OK, """
            {"LogicalName":"account",
             "OneToManyRelationships":[{"SchemaName":"account_tasks","ReferencedEntity":"account","ReferencingEntity":"task","ReferencingAttribute":"regardingobjectid"}],
             "ManyToOneRelationships":[{"SchemaName":"account_owner","ReferencedEntity":"systemuser","ReferencingEntity":"account","ReferencingAttribute":"ownerid"}],
             "ManyToManyRelationships":[{"SchemaName":"accountleads","Entity1LogicalName":"account","Entity2LogicalName":"lead","IntersectEntityName":"accountleads"}]}
            """);

        using DataverseClient client = ClientFactory.Create(fake);
        IReadOnlyList<RelationshipMetadata> relationships = await client.Metadata.GetRelationshipsAsync("account");

        Assert.Equal(3, relationships.Count);
        Assert.Equal(RelationshipKind.OneToMany, relationships[0].Kind);
        Assert.Equal("task", relationships[0].ReferencingTable);
        Assert.Equal(RelationshipKind.ManyToOne, relationships[1].Kind);
        Assert.Equal(RelationshipKind.ManyToMany, relationships[2].Kind);
        Assert.Equal("accountleads", relationships[2].IntersectTable);
    }

    [Fact]
    public async Task GetChoicesAsync_reads_local_or_global_option_set()
    {
        var fake = new FakeHttpMessageHandler();
        fake.EnqueueJson(HttpStatusCode.OK, """
            {"LogicalName":"industrycode",
             "OptionSet":{"Options":[
               {"Value":1,"Label":{"UserLocalizedLabel":{"Label":"Accounting"}},"Color":"#0000ff"},
               {"Value":2,"Label":{"UserLocalizedLabel":{"Label":"Agriculture"}}}]}}
            """);

        using DataverseClient client = ClientFactory.Create(fake);
        IReadOnlyList<ChoiceOption> choices = await client.Metadata.GetChoicesAsync("account", "industrycode");

        Assert.Equal(2, choices.Count);
        Assert.Equal(new ChoiceOption(1, "Accounting") { Color = "#0000ff" }, choices[0]);
    }

    [Fact]
    public async Task GetEntitySetNameAsync_returns_set_name()
    {
        var fake = new FakeHttpMessageHandler();
        fake.EnqueueJson(HttpStatusCode.OK, """{"EntitySetName":"accounts","LogicalName":"account"}""");

        using DataverseClient client = ClientFactory.Create(fake);
        Assert.Equal("accounts", await client.Metadata.GetEntitySetNameAsync("account"));
    }

    [Fact]
    public async Task Invalid_names_are_rejected_before_any_request()
    {
        using DataverseClient client = ClientFactory.Create(new FakeHttpMessageHandler());
        await Assert.ThrowsAsync<ArgumentException>(() => client.Metadata.GetTableAsync("Account'"));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Metadata.GetChoicesAsync("account", "bad column"));
    }
}
