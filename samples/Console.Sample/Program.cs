// Console sample: connect to Dataverse without dependency injection, check identity, and run a
// query. See README.md for setup. Configuration comes from environment variables so no secret
// ever lives in this source file.
using Koras.Dataverse;
using Koras.Dataverse.Errors;
using Koras.Dataverse.Queries;

string? environmentUrl = Environment.GetEnvironmentVariable("DATAVERSE_URL");
if (environmentUrl is null)
{
    Console.Error.WriteLine("Set DATAVERSE_URL (e.g. https://contoso.crm.dynamics.com) before running.");
    return 1;
}

var options = new DataverseClientOptions { EnvironmentUrl = new Uri(environmentUrl) };

// DefaultAzureCredential: works with `az login` locally and managed identity in Azure.
// For an interactive browser sign-in instead, use: options.Authentication.UseInteractive();
options.Authentication.UseDefault();

using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(2));
using DataverseClient client = DataverseClient.Create(options);

try
{
    WhoAmIResponse who = await client.WhoAmIAsync(cancellation.Token);
    Console.WriteLine($"Connected as user {who.UserId} in organization {who.OrganizationId}.");

    Console.WriteLine("Top 5 accounts by name:");
    var query = ODataQuery.For("account").Select("name", "revenue").OrderBy("name").Top(5);
    DataverseQueryResult page = await client.QueryAsync(query, cancellation.Token);
    foreach (Entity account in page.Entities)
    {
        string revenue = account.FormattedValues.TryGetValue("revenue", out string? formatted) ? formatted : "-";
        Console.WriteLine($"  {account.GetValue<string>("name")}  (revenue: {revenue})");
    }

    return 0;
}
catch (DataverseException exception)
{
    Console.Error.WriteLine($"Dataverse call failed: {exception.Category} — {exception.Message}");
    Console.Error.WriteLine($"HTTP {exception.Error.HttpStatusCode}, code {exception.Error.ErrorCode}, request {exception.Error.RequestId}");
    return 2;
}
