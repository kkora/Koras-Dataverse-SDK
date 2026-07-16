using System.Text.Json;
using System.Text.Json.Nodes;
using Koras.Dataverse.Queries;

namespace Koras.Dataverse.Solutions;

/// <summary>Web API implementation of <see cref="ISolutionClient"/>.</summary>
internal sealed class SolutionClient : ISolutionClient
{
    private readonly DataverseClient _client;

    public SolutionClient(DataverseClient client) => _client = client;

    public async Task<byte[]> ExportAsync(string solutionUniqueName, bool managed = false, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(solutionUniqueName);

        var payload = new JsonObject
        {
            ["SolutionName"] = solutionUniqueName,
            ["Managed"] = managed,
        };

        using HttpRequestMessage request = DataverseClient.CreateRequest(HttpMethod.Post, "ExportSolution", payload.ToJsonString());
        using HttpResponseMessage response = await _client.SendAsync(request, "solution.export", null, cancellationToken).ConfigureAwait(false);
        using JsonDocument document = await DataverseClient.ReadJsonAsync(response, cancellationToken).ConfigureAwait(false);

        if (document.RootElement.TryGetProperty("ExportSolutionFile", out JsonElement file) && file.ValueKind == JsonValueKind.String)
        {
            return file.GetBytesFromBase64();
        }

        throw new Errors.DataverseException(new Errors.DataverseError
        {
            Category = Errors.DataverseErrorCategory.Unknown,
            Message = $"ExportSolution for '{solutionUniqueName}' returned no ExportSolutionFile.",
        });
    }

    public async Task ImportAsync(ReadOnlyMemory<byte> solutionZip, SolutionImportOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (solutionZip.IsEmpty)
        {
            throw new ArgumentException("The solution zip is empty.", nameof(solutionZip));
        }

        options ??= new SolutionImportOptions();
        Guid jobId = options.ImportJobId == Guid.Empty ? Guid.NewGuid() : options.ImportJobId;

        var payload = new JsonObject
        {
            ["OverwriteUnmanagedCustomizations"] = options.OverwriteUnmanagedCustomizations,
            ["PublishWorkflows"] = options.PublishWorkflows,
            ["ConvertToManaged"] = options.ConvertToManaged,
            ["CustomizationFile"] = Convert.ToBase64String(solutionZip.Span),
            ["ImportJobId"] = jobId.ToString("D"),
        };

        using HttpRequestMessage request = DataverseClient.CreateRequest(HttpMethod.Post, "ImportSolution", payload.ToJsonString());
        using HttpResponseMessage response = await _client.SendAsync(request, "solution.import", null, cancellationToken).ConfigureAwait(false);
    }

    public async Task PublishAllAsync(CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = DataverseClient.CreateRequest(HttpMethod.Post, "PublishAllXml", "{}");
        using HttpResponseMessage response = await _client.SendAsync(request, "solution.publishall", null, cancellationToken).ConfigureAwait(false);
    }

    public async Task<SolutionInfo?> FindAsync(string uniqueName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uniqueName);

        var query = ODataQuery.For("solution")
            .Select("solutionid", "uniquename", "friendlyname", "version", "ismanaged", "installedon")
            .Where(f => f.Eq("uniquename", uniqueName))
            .Top(1);

        DataverseQueryResult result = await _client.QueryAsync(query, cancellationToken).ConfigureAwait(false);
        if (result.Entities.Count == 0)
        {
            return null;
        }

        Entity solution = result.Entities[0];
        return new SolutionInfo
        {
            SolutionId = solution.GetValue<Guid>("solutionid"),
            UniqueName = solution.GetValue<string>("uniquename") ?? uniqueName,
            FriendlyName = solution.GetValue<string>("friendlyname"),
            Version = solution.GetValue<string>("version"),
            IsManaged = solution.GetValue<bool>("ismanaged"),
            InstalledOn = solution.GetValue<DateTimeOffset?>("installedon"),
        };
    }
}
