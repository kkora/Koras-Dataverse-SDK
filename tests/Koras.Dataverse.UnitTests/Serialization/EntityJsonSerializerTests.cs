using System.Text.Json;
using Koras.Dataverse.Serialization;

namespace Koras.Dataverse.UnitTests.Serialization;

public class EntityJsonSerializerTests
{
    private static readonly EntityJsonSerializer Serializer = new(new EntitySetNameResolver(new Dictionary<string, string>()));

    [Fact]
    public void WritePayload_serializes_plain_values_and_lookups()
    {
        var contactId = Guid.Parse("1a2b3c4d-0000-0000-0000-000000000000");
        var entity = new Entity("account")
        {
            ["name"] = "Contoso",
            ["revenue"] = 250000.5m,
            ["numberofemployees"] = 12,
            ["donotemail"] = true,
            ["primarycontactid"] = new EntityReference("contact", contactId),
            ["description"] = null,
        };

        using JsonDocument payload = JsonDocument.Parse(Serializer.WritePayload(entity));
        JsonElement root = payload.RootElement;

        Assert.Equal("Contoso", root.GetProperty("name").GetString());
        Assert.Equal(250000.5m, root.GetProperty("revenue").GetDecimal());
        Assert.Equal(12, root.GetProperty("numberofemployees").GetInt32());
        Assert.True(root.GetProperty("donotemail").GetBoolean());
        Assert.Equal($"/contacts({contactId:D})", root.GetProperty("primarycontactid@odata.bind").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("description").ValueKind);
        Assert.False(root.TryGetProperty("primarycontactid", out _));
    }

    [Fact]
    public void WritePayload_rejects_unsupported_types()
    {
        var entity = new Entity("account") { ["bad"] = new object() };
        Assert.Throws<NotSupportedException>(() => Serializer.WritePayload(entity));
    }

    [Fact]
    public void ReadEntity_materializes_id_formatted_values_and_lookups()
    {
        const string json = """
            {
              "@odata.etag": "W/\"42\"",
              "accountid": "0f3f3f3f-3f3f-3f3f-3f3f-3f3f3f3f3f3f",
              "name": "Contoso",
              "revenue": 250000.5,
              "revenue@OData.Community.Display.V1.FormattedValue": "$250,000.50",
              "numberofemployees": 12,
              "donotemail": false,
              "_primarycontactid_value": "1a2b3c4d-0000-0000-0000-000000000000",
              "_primarycontactid_value@Microsoft.Dynamics.CRM.lookuplogicalname": "contact",
              "_primarycontactid_value@OData.Community.Display.V1.FormattedValue": "Ada Lovelace"
            }
            """;

        using JsonDocument document = JsonDocument.Parse(json);
        Entity entity = EntityJsonSerializer.ReadEntity(document.RootElement, "account");

        Assert.Equal(Guid.Parse("0f3f3f3f-3f3f-3f3f-3f3f-3f3f3f3f3f3f"), entity.Id);
        Assert.Equal("Contoso", entity["name"]);
        Assert.Equal(250000.5m, entity["revenue"]);
        Assert.Equal(12, entity["numberofemployees"]);
        Assert.Equal(false, entity["donotemail"]);
        Assert.Equal("$250,000.50", entity.FormattedValues["revenue"]);

        var lookup = Assert.IsType<EntityReference>(entity["_primarycontactid_value"]);
        Assert.Equal("contact", lookup.TableName);
        Assert.Equal("Ada Lovelace", lookup.Name);
        Assert.False(entity.Attributes.ContainsKey("@odata.etag"));
    }

    [Fact]
    public void ReadEntity_handles_expanded_navigation_objects()
    {
        const string json = """
            {
              "accountid": "0f3f3f3f-3f3f-3f3f-3f3f-3f3f3f3f3f3f",
              "primarycontactid": { "contactid": "1a2b3c4d-0000-0000-0000-000000000000", "fullname": "Ada" }
            }
            """;

        using JsonDocument document = JsonDocument.Parse(json);
        Entity entity = EntityJsonSerializer.ReadEntity(document.RootElement, "account");

        var nested = Assert.IsType<Entity>(entity["primarycontactid"]);
        Assert.Equal("Ada", nested["fullname"]);
    }

    [Theory]
    [InlineData("account", "accounts")]
    [InlineData("opportunity", "opportunities")]
    [InlineData("bus", "buses")]
    [InlineData("fax", "faxes")]
    [InlineData("quiz", "quizes")]
    [InlineData("branch", "branches")]
    [InlineData("wish", "wishes")]
    [InlineData("day", "days")]
    [InlineData("contact", "contacts")]
    public void Resolver_pluralizes_like_dataverse(string logical, string expected)
    {
        Assert.Equal(expected, EntitySetNameResolver.Pluralize(logical));
    }

    [Fact]
    public void Resolver_overrides_win()
    {
        var resolver = new EntitySetNameResolver(new Dictionary<string, string> { ["new_weird"] = "new_weirdset" });
        Assert.Equal("new_weirdset", resolver.Resolve("new_weird"));
        Assert.Equal("accounts", resolver.Resolve("account"));
    }
}
