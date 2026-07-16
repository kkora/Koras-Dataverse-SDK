using Koras.Dataverse.Queries;

namespace Koras.Dataverse.UnitTests.Queries;

public class ODataQueryTests
{
    [Fact]
    public void Empty_query_renders_empty_string()
    {
        Assert.Equal(string.Empty, ODataQuery.For("account").ToQueryString());
    }

    [Fact]
    public void All_options_compose_in_stable_order()
    {
        string qs = ODataQuery.For("account")
            .Select("name", "revenue")
            .Where(f => f.Eq("statecode", 0))
            .OrderBy("name")
            .OrderByDescending("revenue")
            .Expand("primarycontactid", "fullname")
            .Top(10)
            .IncludeCount()
            .ToQueryString();

        Assert.Equal(
            "$select=name%2Crevenue&$filter=statecode%20eq%200&$orderby=name%2Crevenue%20desc" +
            "&$expand=primarycontactid%28%24select%3Dfullname%29&$top=10&$count=true",
            qs);
    }

    [Fact]
    public void PageSize_is_carried_but_not_in_query_string()
    {
        ODataQuery query = ODataQuery.For("account").PageSize(50);
        Assert.Equal(50, query.PreferredPageSize);
        Assert.Equal(string.Empty, query.ToQueryString());
    }

    [Fact]
    public void Table_and_column_names_are_validated()
    {
        Assert.Throws<ArgumentException>(() => ODataQuery.For("Account"));
        Assert.Throws<ArgumentException>(() => ODataQuery.For("account").Select("name,revenue"));
        Assert.Throws<ArgumentException>(() => ODataQuery.For("account").OrderBy("name desc"));
        Assert.Throws<ArgumentException>(() => ODataQuery.For("account").Expand("nav prop"));
    }

    [Fact]
    public void Top_and_page_size_bounds_are_enforced()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ODataQuery.For("account").Top(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ODataQuery.For("account").PageSize(5001));
    }

    [Fact]
    public void WhereRaw_is_passed_through_verbatim()
    {
        string qs = ODataQuery.For("account").WhereRaw("statecode eq 0").ToQueryString();
        Assert.Equal("$filter=" + Uri.EscapeDataString("(statecode eq 0)"), qs);
    }
}
