namespace Koras.Dataverse.UnitTests.Model;

public class EntityTests
{
    [Fact]
    public void Indexer_reads_missing_attribute_as_null()
    {
        var entity = new Entity("account");
        Assert.Null(entity["name"]);
        entity["name"] = "Contoso";
        Assert.Equal("Contoso", entity["name"]);
    }

    [Fact]
    public void GetValue_converts_common_shapes()
    {
        var id = Guid.NewGuid();
        var entity = new Entity("account")
        {
            ["accountid"] = id.ToString("D"),
            ["revenue"] = 100,               // int stored, decimal requested
            ["modifiedon"] = "2026-01-31T08:30:00Z",
            ["statuscode"] = 5,
        };

        Assert.Equal(id, entity.GetValue<Guid>("accountid"));
        Assert.Equal(100m, entity.GetValue<decimal>("revenue"));
        Assert.Equal(new DateTimeOffset(2026, 1, 31, 8, 30, 0, TimeSpan.Zero), entity.GetValue<DateTimeOffset>("modifiedon"));
        Assert.Equal(DayOfWeek.Friday, entity.GetValue<DayOfWeek>("statuscode"));
        Assert.Null(entity.GetValue<string>("missing"));
        Assert.Null(entity.GetValue<int?>("missing"));
    }

    [Fact]
    public void GetValue_throws_informative_invalid_cast()
    {
        var entity = new Entity("account") { ["name"] = "text" };
        InvalidCastException exception = Assert.Throws<InvalidCastException>(() => entity.GetValue<Guid>("name"));
        Assert.Contains("name", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Guid", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TryGetValue_reports_success_and_failure()
    {
        var entity = new Entity("account") { ["revenue"] = 10m, ["name"] = "x" };
        Assert.True(entity.TryGetValue("revenue", out decimal revenue));
        Assert.Equal(10m, revenue);
        Assert.False(entity.TryGetValue("missing", out decimal _));
        Assert.False(entity.TryGetValue("name", out Guid _));
    }

    [Fact]
    public void ToReference_requires_an_id()
    {
        var entity = new Entity("account");
        Assert.Throws<InvalidOperationException>(() => entity.ToReference());

        entity.Id = Guid.NewGuid();
        EntityReference reference = entity.ToReference();
        Assert.Equal("account", reference.TableName);
        Assert.Equal(entity.Id, reference.Id);
    }

    [Fact]
    public void Constructor_rejects_blank_table_names()
    {
        Assert.Throws<ArgumentException>(() => new Entity(" "));
    }
}
