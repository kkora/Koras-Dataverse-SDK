using Koras.Dataverse.Queries;

namespace Koras.Dataverse.UnitTests.Queries;

public class ODataFilterBuilderTests
{
    private static string Build(Action<ODataQuery> configure)
    {
        ODataQuery query = ODataQuery.For("account");
        configure(query);
        string qs = query.ToQueryString();
        string marker = "$filter=";
        int index = qs.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(index >= 0, $"no $filter in '{qs}'");
        string encoded = qs[(index + marker.Length)..];
        int end = encoded.IndexOf('&', StringComparison.Ordinal);
        return Uri.UnescapeDataString(end < 0 ? encoded : encoded[..end]);
    }

    [Fact]
    public void Comparisons_render_odata_literals()
    {
        string filter = Build(q => q.Where(f => f.Eq("name", "Contoso").Gt("revenue", 100.5m).Eq("statecode", 0)));
        Assert.Equal("name eq 'Contoso' and revenue gt 100.5 and statecode eq 0", filter);
    }

    [Fact]
    public void String_quotes_are_doubled_blocking_injection()
    {
        string filter = Build(q => q.Where(f => f.Eq("name", "x' or 1 eq 1 or name eq '")));
        Assert.Equal("name eq 'x'' or 1 eq 1 or name eq '''", filter);
    }

    [Fact]
    public void Guids_dates_and_bools_render_unquoted()
    {
        var id = Guid.Parse("0f3f3f3f-3f3f-3f3f-3f3f-3f3f3f3f3f3f");
        var when = new DateTimeOffset(2026, 1, 31, 8, 30, 0, TimeSpan.FromHours(2));
        string filter = Build(q => q.Where(f => f.Eq("_ownerid_value", id).Ge("createdon", when).Eq("donotemail", true)));
        Assert.Equal(
            "_ownerid_value eq 0f3f3f3f-3f3f-3f3f-3f3f-3f3f3f3f3f3f and createdon ge 2026-01-31T06:30:00.000Z and donotemail eq true",
            filter);
    }

    [Fact]
    public void Functions_and_null_checks_render()
    {
        string filter = Build(q => q.Where(f => f.Contains("name", "o'brien").IsNull("parentaccountid").IsNotNull("primarycontactid")));
        Assert.Equal("contains(name,'o''brien') and parentaccountid eq null and primarycontactid ne null", filter);
    }

    [Fact]
    public void In_expands_to_or_chain()
    {
        string filter = Build(q => q.Where(f => f.In("industrycode", 1, 2)));
        Assert.Equal("(industrycode eq 1 or industrycode eq 2)", filter);
    }

    [Fact]
    public void Groups_and_not_compose()
    {
        string filter = Build(q => q.Where(f => f
            .Eq("statecode", 0)
            .Or(o => o.Eq("industrycode", 1).Eq("industrycode", 2))
            .Not(n => n.Eq("name", "skip"))));
        Assert.Equal("statecode eq 0 and (industrycode eq 1 or industrycode eq 2) and not (name eq 'skip')", filter);
    }

    [Theory]
    [InlineData("name eq 'x' or 1")]
    [InlineData("bad column")]
    public void Column_names_are_validated(string column)
    {
        Assert.Throws<ArgumentException>(() => ODataQuery.For("account").Where(f => f.Eq(column, 1)));
    }

    [Fact]
    public void Empty_nested_groups_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => ODataQuery.For("account").Where(f => f.Or(_ => { })));
    }

    [Fact]
    public void Unsupported_literal_types_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => ODataFilterBuilder.Literal(new object()));
    }
}
