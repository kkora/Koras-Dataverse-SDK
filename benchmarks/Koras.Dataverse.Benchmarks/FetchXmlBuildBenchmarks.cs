using BenchmarkDotNet.Attributes;
using Koras.Dataverse.FetchXml;

namespace Koras.Dataverse.Benchmarks;

/// <summary>FetchXML builder → XML string (docs/performance/benchmarks.md §2.1, KDV-004).</summary>
[MemoryDiagnoser]
public class FetchXmlBuildBenchmarks
{
    [Benchmark]
    public string Minimal() => FetchXml.FetchXml.For("account")
        .Attributes("name", "revenue", "statecode")
        .Build()
        .Xml;

    [Benchmark]
    [Arguments(1)]
    [Arguments(10)]
    [Arguments(100)]
    public string Filtered(int conditions) => FetchXml.FetchXml.For("account")
        .Attributes("name")
        .Where(f =>
        {
            for (int i = 0; i < conditions; i++)
            {
                switch (i % 4)
                {
                    case 0: f.Eq("statecode", i); break;
                    case 1: f.Like("name", "Contoso" + i + "%"); break;
                    case 2: f.Gt("revenue", 1000m + i); break;
                    default: f.In("industrycode", i, i + 1, i + 2); break;
                }
            }
        })
        .Build()
        .Xml;

    [Benchmark]
    public string NestedFilters() => FetchXml.FetchXml.For("account")
        .Attributes("name")
        .Where(f => f.Eq("statecode", 0)
            .Or(o => o.Like("name", "A%").Like("name", "B%")
                .And(a => a.Gt("revenue", 100m).Lt("revenue", 1_000_000m))))
        .Build()
        .Xml;

    [Benchmark]
    [Arguments(1)]
    [Arguments(3)]
    [Arguments(5)]
    public string Links(int links)
    {
        var builder = FetchXml.FetchXml.For("account").Attributes("name");
        for (int i = 0; i < links; i++)
        {
            string alias = "l" + i;
            builder = builder.Link("contact", "contactid", "primarycontactid",
                l => l.Alias(alias).Attributes("fullname", "emailaddress1"));
        }

        return builder.Build().Xml;
    }

    [Benchmark]
    public string FullComposite() => FetchXml.FetchXml.For("account")
        .Attributes("name", "revenue", "statecode", "industrycode", "createdon")
        .Where(f => f.Eq("statecode", 0).Like("name", "Contoso%").In("industrycode", 1, 2, 3).IsNotNull("primarycontactid"))
        .Link("contact", "contactid", "primarycontactid", l => l.Alias("pc").Attributes("fullname"))
        .Link("systemuser", "systemuserid", "ownerid", l => l.Alias("owner").Attributes("fullname"))
        .OrderBy("name")
        .Top(50)
        .Build()
        .Xml;

    [Benchmark]
    public string EscapingHeavy() => FetchXml.FetchXml.For("account")
        .Attributes("name")
        .Where(f => f.Eq("name", "\"O'Brien & Söhne\" <international>")
            .Like("description", "%<script>&amp;\"quotes\"—ダッシュ%"))
        .Build()
        .Xml;
}
