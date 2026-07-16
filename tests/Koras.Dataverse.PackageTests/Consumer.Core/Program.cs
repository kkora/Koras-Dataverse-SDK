using Koras.Dataverse;
using Koras.Dataverse.FetchXml;
using Koras.Dataverse.OpenTelemetry;
using Koras.Dataverse.Queries;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

// Late-bound entity model (Abstractions).
var account = new Entity("account")
{
    ["name"] = "Contoso",
    ["revenue"] = 250_000m,
    ["primarycontactid"] = new EntityReference("contact", Guid.NewGuid()),
};

// Injection-safe OData query builder.
ODataQuery query = ODataQuery.For("account")
    .Select("name", "revenue")
    .Where(f => f.Eq("statecode", 0))
    .OrderBy("name")
    .Top(5);

// FetchXML builder (transitive Koras.Dataverse.FetchXml reference).
FetchXmlQuery fetch = FetchXml.For("account").Attributes("name").Top(1).Build();

// DI registration and client resolution — no network I/O happens at resolve time.
var services = new ServiceCollection();
services.AddDataverse(o =>
{
    o.EnvironmentUrl = new Uri("https://packagetests.crm.dynamics.com");
    o.Authentication.UseClientSecret(
        tenantId: "00000000-0000-0000-0000-000000000001",
        clientId: "00000000-0000-0000-0000-000000000002",
        clientSecret: "package-test-secret");
});

using ServiceProvider provider = services.BuildServiceProvider();
IDataverseClient client = provider.GetRequiredService<IDataverseClient>();

if (account.TableName != "account" || fetch.Xml.Length == 0 || client is null)
{
    Console.Error.WriteLine("Package consumption smoke checks failed.");
    return 1;
}

Console.WriteLine($"Core consumer OK on {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}: query='{query}'");
return 0;

// Compile-time proof the OpenTelemetry package wires up against OpenTelemetry.Api only.
static class OtelWiring
{
    internal static TracerProviderBuilder Traces(TracerProviderBuilder builder) => builder.AddKorasDataverseInstrumentation();
    internal static MeterProviderBuilder Metrics(MeterProviderBuilder builder) => builder.AddKorasDataverseInstrumentation();
}
