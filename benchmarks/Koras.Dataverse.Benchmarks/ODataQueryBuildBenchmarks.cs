using BenchmarkDotNet.Attributes;
using Koras.Dataverse.Queries;

namespace Koras.Dataverse.Benchmarks;

/// <summary>ODataQuery → encoded query string (docs/performance/benchmarks.md §2.2, KDV-003).</summary>
[MemoryDiagnoser]
public class ODataQueryBuildBenchmarks
{
    private static readonly string[] WideSelect = Enumerable.Range(0, 25).Select(i => "column" + i).ToArray();

    [Benchmark]
    [Arguments(3)]
    [Arguments(25)]
    public string SelectOnly(int columns) => ODataQuery.For("account")
        .Select(columns == 3 ? ["name", "revenue", "statecode"] : WideSelect)
        .ToQueryString();

    [Benchmark]
    [Arguments(1)]
    [Arguments(10)]
    [Arguments(100)]
    public string Filtered(int conditions) => ODataQuery.For("account")
        .Select("name")
        .Where(f =>
        {
            for (int i = 0; i < conditions; i++)
            {
                switch (i % 4)
                {
                    case 0: f.Eq("name", "Contoso " + i); break;
                    case 1: f.Eq("primarycontactid", new Guid("00000000-0000-0000-0000-00000000000" + (i % 10))); break;
                    case 2: f.Gt("createdon", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).AddDays(i)); break;
                    default: f.Le("revenue", 250_000.55m + i); break;
                }
            }
        })
        .ToQueryString();

    [Benchmark]
    public string OrderTopCount() => ODataQuery.For("account")
        .Select("name")
        .OrderBy("name")
        .OrderByDescending("revenue")
        .Top(50)
        .IncludeCount()
        .ToQueryString();

    [Benchmark]
    [Arguments(1)]
    [Arguments(3)]
    public string Expanded(int expands)
    {
        ODataQuery query = ODataQuery.For("account").Select("name");
        for (int i = 0; i < expands; i++)
        {
            query = query.Expand("navproperty" + i, "name", "createdon");
        }

        return query.ToQueryString();
    }

    [Benchmark]
    public string EscapingHeavy() => ODataQuery.For("account")
        .Select("name")
        .Where(f => f.Eq("name", "O'Brien & \"Söhne\" #100% <html>?/\\")
            .Contains("description", "50% off & more' — ダッシュ"))
        .ToQueryString();
}
