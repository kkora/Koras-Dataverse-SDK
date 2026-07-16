using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Koras.Dataverse.Serialization;

namespace Koras.Dataverse.Benchmarks;

/// <summary>Entity JSON read/write (docs/performance/benchmarks.md §2.3, KDV-002).</summary>
[MemoryDiagnoser]
public class EntitySerializationBenchmarks
{
    private static readonly EntityJsonSerializer Serializer = new(new EntitySetNameResolver(new Dictionary<string, string>()));

    private static readonly Entity Row = new("account", Guid.NewGuid())
    {
        ["name"] = "Contoso Ltd.",
        ["revenue"] = 250_000m,
        ["numberofemployees"] = 320,
        ["primarycontactid"] = new EntityReference("contact", Guid.NewGuid()),
        ["modifiedon"] = DateTimeOffset.UtcNow,
    };

    private static readonly string ResponseJson = """
        {
          "@odata.etag": "W/\"123\"",
          "accountid": "0f3f3f3f-3f3f-3f3f-3f3f-3f3f3f3f3f3f",
          "name": "Contoso Ltd.",
          "revenue": 250000.0,
          "revenue@OData.Community.Display.V1.FormattedValue": "$250,000.00",
          "numberofemployees": 320,
          "_primarycontactid_value": "1a2b3c4d-0000-0000-0000-000000000000",
          "_primarycontactid_value@Microsoft.Dynamics.CRM.lookuplogicalname": "contact",
          "_primarycontactid_value@OData.Community.Display.V1.FormattedValue": "Ada Contact"
        }
        """;

    [Benchmark]
    public string WritePayload() => Serializer.WritePayload(Row);

    [Benchmark]
    public Entity ReadEntity()
    {
        using JsonDocument document = JsonDocument.Parse(ResponseJson);
        return EntityJsonSerializer.ReadEntity(document.RootElement, "account");
    }
}
