using Koras.Dataverse.Mapping;

namespace Koras.Dataverse.UnitTests.Model;

public class EntityMapperTests
{
    [DataverseTable("account")]
    private sealed class Account
    {
        [DataverseColumn("accountid")]
        public Guid Id { get; set; }

        [DataverseColumn("name")]
        public string? Name { get; set; }

        [DataverseColumn("revenue")]
        public decimal? Revenue { get; set; }

        [DataverseColumn("primarycontactid")]
        public EntityReference? PrimaryContact { get; set; }

        public string? NotMapped { get; set; }
    }

    private sealed class Unmapped
    {
    }

    [Fact]
    public void ToEntity_maps_annotated_properties_and_primary_id()
    {
        var id = Guid.NewGuid();
        var contact = new EntityReference("contact", Guid.NewGuid());
        var account = new Account { Id = id, Name = "Contoso", Revenue = 5m, PrimaryContact = contact, NotMapped = "skip" };

        Entity entity = EntityMapper.ToEntity(account);

        Assert.Equal("account", entity.TableName);
        Assert.Equal(id, entity.Id);
        Assert.Equal("Contoso", entity["name"]);
        Assert.Equal(5m, entity["revenue"]);
        Assert.Same(contact, entity["primarycontactid"]);
        Assert.False(entity.Attributes.ContainsKey("accountid"));
        Assert.False(entity.Attributes.ContainsKey("NotMapped"));
    }

    [Fact]
    public void ToObject_round_trips_and_converts()
    {
        var id = Guid.NewGuid();
        var entity = new Entity("account", id)
        {
            ["name"] = "Contoso",
            ["revenue"] = 5, // int → decimal?
        };

        Account account = entity.ToObject<Account>();
        Assert.Equal(id, account.Id);
        Assert.Equal("Contoso", account.Name);
        Assert.Equal(5m, account.Revenue);
        Assert.Null(account.PrimaryContact);
    }

    [Fact]
    public void Null_property_values_are_omitted_from_entities()
    {
        Entity entity = EntityMapper.ToEntity(new Account { Name = null });
        Assert.False(entity.Attributes.ContainsKey("name"));
    }

    [Fact]
    public void Unannotated_types_are_rejected()
    {
        Assert.Throws<InvalidOperationException>(() => EntityMapper.ToEntity(new Unmapped()));
    }

    [Fact]
    public void TableNameOf_returns_declared_name()
    {
        Assert.Equal("account", EntityMapper.TableNameOf<Account>());
    }
}
