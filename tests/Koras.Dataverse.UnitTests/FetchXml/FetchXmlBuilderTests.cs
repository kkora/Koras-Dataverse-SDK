using System.Xml.Linq;
using Koras.Dataverse.FetchXml;

namespace Koras.Dataverse.UnitTests.FetchXml;

public class FetchXmlBuilderTests
{
    [Fact]
    public void Build_produces_fetch_entity_structure()
    {
        FetchXmlQuery query = Dataverse.FetchXml.FetchXml.For("account")
            .Attributes("name", "revenue")
            .Build();

        XElement fetch = XElement.Parse(query.Xml);
        Assert.Equal("fetch", fetch.Name.LocalName);
        XElement entity = Assert.Single(fetch.Elements("entity"));
        Assert.Equal("account", entity.Attribute("name")!.Value);
        Assert.Equal(new[] { "name", "revenue" }, entity.Elements("attribute").Select(a => a.Attribute("name")!.Value));
        Assert.Equal("account", query.TableName);
    }

    [Fact]
    public void Where_conditions_are_and_combined()
    {
        FetchXmlQuery query = Dataverse.FetchXml.FetchXml.For("account")
            .Where(f => f.Eq("statecode", 0).Like("name", "Contoso%"))
            .Build();

        XElement filter = XElement.Parse(query.Xml).Element("entity")!.Element("filter")!;
        Assert.Equal("and", filter.Attribute("type")!.Value);
        List<XElement> conditions = filter.Elements("condition").ToList();
        Assert.Equal(2, conditions.Count);
        Assert.Equal("eq", conditions[0].Attribute("operator")!.Value);
        Assert.Equal("0", conditions[0].Attribute("value")!.Value);
        Assert.Equal("like", conditions[1].Attribute("operator")!.Value);
        Assert.Equal("Contoso%", conditions[1].Attribute("value")!.Value);
    }

    [Fact]
    public void Or_group_nests_a_filter()
    {
        FetchXmlQuery query = Dataverse.FetchXml.FetchXml.For("account")
            .Where(f => f.Eq("statecode", 0).Or(o => o.Eq("industrycode", 1).Eq("industrycode", 2)))
            .Build();

        XElement filter = XElement.Parse(query.Xml).Element("entity")!.Element("filter")!;
        XElement nested = Assert.Single(filter.Elements("filter"));
        Assert.Equal("or", nested.Attribute("type")!.Value);
        Assert.Equal(2, nested.Elements("condition").Count());
    }

    [Fact]
    public void Values_are_xml_encoded_not_injected()
    {
        FetchXmlQuery query = Dataverse.FetchXml.FetchXml.For("account")
            .Where(f => f.Eq("name", "\"/><script>alert(1)</script>"))
            .Build();

        // The malicious text must survive as a value, not as markup.
        XElement condition = XElement.Parse(query.Xml).Descendants("condition").Single();
        Assert.Equal("\"/><script>alert(1)</script>", condition.Attribute("value")!.Value);
        Assert.DoesNotContain("<script>", query.Xml, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Account")]
    [InlineData("account name")]
    [InlineData("account'--")]
    [InlineData("")]
    public void Invalid_logical_names_are_rejected(string tableName)
    {
        Assert.Throws<ArgumentException>(() => Dataverse.FetchXml.FetchXml.For(tableName));
    }

    [Fact]
    public void In_condition_renders_value_elements()
    {
        FetchXmlQuery query = Dataverse.FetchXml.FetchXml.For("account")
            .Where(f => f.In("industrycode", 1, 2, 3))
            .Build();

        XElement condition = XElement.Parse(query.Xml).Descendants("condition").Single();
        Assert.Equal("in", condition.Attribute("operator")!.Value);
        Assert.Equal(new[] { "1", "2", "3" }, condition.Elements("value").Select(v => v.Value));
    }

    [Fact]
    public void Null_operators_require_no_value_and_valued_operators_require_one()
    {
        FetchXmlQuery query = Dataverse.FetchXml.FetchXml.For("account").Where(f => f.IsNull("parentaccountid")).Build();
        Assert.Null(XElement.Parse(query.Xml).Descendants("condition").Single().Attribute("value"));

        Assert.Throws<ArgumentException>(() =>
            Dataverse.FetchXml.FetchXml.For("account").Where(f => f.Condition("name", FetchConditionOperator.Equal)).Build());
    }

    [Fact]
    public void Link_renders_link_entity_with_alias_and_nested_content()
    {
        FetchXmlQuery query = Dataverse.FetchXml.FetchXml.For("account")
            .Link("contact", "primarycontactid", "contactid",
                l => l.Alias("pc").Attributes("fullname").Where(f => f.IsNotNull("emailaddress1")),
                FetchLinkType.Outer)
            .Build();

        XElement link = XElement.Parse(query.Xml).Descendants("link-entity").Single();
        Assert.Equal("contact", link.Attribute("name")!.Value);
        Assert.Equal("primarycontactid", link.Attribute("from")!.Value);
        Assert.Equal("contactid", link.Attribute("to")!.Value);
        Assert.Equal("outer", link.Attribute("link-type")!.Value);
        Assert.Equal("pc", link.Attribute("alias")!.Value);
        Assert.Single(link.Elements("attribute"));
        Assert.Single(link.Elements("filter"));
    }

    [Fact]
    public void Orders_top_distinct_render_as_attributes()
    {
        FetchXmlQuery query = Dataverse.FetchXml.FetchXml.For("account")
            .Distinct()
            .Top(25)
            .OrderBy("name")
            .OrderByDescending("revenue")
            .Build();

        XElement fetch = XElement.Parse(query.Xml);
        Assert.Equal("true", fetch.Attribute("distinct")!.Value);
        Assert.Equal("25", fetch.Attribute("top")!.Value);
        List<XElement> orders = fetch.Element("entity")!.Elements("order").ToList();
        Assert.Null(orders[0].Attribute("descending"));
        Assert.Equal("true", orders[1].Attribute("descending")!.Value);
    }

    [Fact]
    public void Top_and_page_cannot_be_combined()
    {
        FetchXmlBuilder builder = Dataverse.FetchXml.FetchXml.For("account").Top(10).Page(1, 100);
        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void Dates_and_enums_are_formatted_invariantly()
    {
        var moment = new DateTime(2026, 1, 31, 13, 45, 0, DateTimeKind.Utc);
        FetchXmlQuery query = Dataverse.FetchXml.FetchXml.For("account")
            .Where(f => f.Ge("createdon", moment).Eq("statuscode", DayOfWeek.Friday))
            .Build();

        List<XElement> conditions = XElement.Parse(query.Xml).Descendants("condition").ToList();
        Assert.Equal("2026-01-31T13:45:00Z", conditions[0].Attribute("value")!.Value);
        Assert.Equal("5", conditions[1].Attribute("value")!.Value);
    }
}
