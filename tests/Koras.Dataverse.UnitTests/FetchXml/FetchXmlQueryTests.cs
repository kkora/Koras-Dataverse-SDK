using System.Xml.Linq;
using Koras.Dataverse.FetchXml;

namespace Koras.Dataverse.UnitTests.FetchXml;

public class FetchXmlQueryTests
{
    [Fact]
    public void FromXml_accepts_valid_fetchxml()
    {
        FetchXmlQuery query = FetchXmlQuery.FromXml("<fetch top='5'><entity name='account'><attribute name='name'/></entity></fetch>");
        Assert.Equal("account", query.TableName);
        Assert.Contains("account", query.Xml, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not xml at all")]
    [InlineData("<query><entity name='account'/></query>")]
    [InlineData("<fetch><wrong/></fetch>")]
    public void FromXml_rejects_invalid_documents(string xml)
    {
        Assert.Throws<ArgumentException>(() => FetchXmlQuery.FromXml(xml));
    }

    [Fact]
    public void WithPage_replaces_paging_attributes_and_drops_top()
    {
        FetchXmlQuery query = Dataverse.FetchXml.FetchXml.For("account").Top(10).Build();
        FetchXmlQuery paged = query.WithPage(3, 500, "<cookie page=\"2\"/>");

        XElement fetch = XElement.Parse(paged.Xml);
        Assert.Null(fetch.Attribute("top"));
        Assert.Equal("3", fetch.Attribute("page")!.Value);
        Assert.Equal("500", fetch.Attribute("count")!.Value);
        Assert.Equal("<cookie page=\"2\"/>", fetch.Attribute("paging-cookie")!.Value);

        // The original query is unchanged (immutability).
        Assert.NotNull(XElement.Parse(query.Xml).Attribute("top"));
    }

    [Fact]
    public void WithPage_validates_arguments()
    {
        FetchXmlQuery query = Dataverse.FetchXml.FetchXml.For("account").Build();
        Assert.Throws<ArgumentOutOfRangeException>(() => query.WithPage(0, 100));
        Assert.Throws<ArgumentOutOfRangeException>(() => query.WithPage(1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => query.WithPage(1, 5001));
    }
}
