using BenchmarkDotNet.Attributes;
using Koras.Dataverse.FetchXml;
using Koras.Dataverse.Queries;

namespace Koras.Dataverse.Benchmarks;

/// <summary>Benchmarks for the query builders (KDV-003, KDV-004 hot paths).</summary>
[MemoryDiagnoser]
public class QueryBuilderBenchmarks
{
    [Benchmark]
    public string BuildFetchXml() => FetchXml.FetchXml.For("account")
        .Attributes("name", "revenue", "statecode")
        .Where(f => f.Eq("statecode", 0).Like("name", "Contoso%").In("industrycode", 1, 2, 3))
        .Link("contact", "primarycontactid", "contactid", l => l.Alias("pc").Attributes("fullname"))
        .OrderBy("name")
        .Top(50)
        .Build()
        .Xml;

    [Benchmark]
    public string BuildODataQueryString() => ODataQuery.For("account")
        .Select("name", "revenue", "statecode")
        .Where(f => f.Eq("statecode", 0).Contains("name", "Contoso").In("industrycode", 1, 2, 3))
        .OrderBy("name")
        .Top(50)
        .ToQueryString();
}
