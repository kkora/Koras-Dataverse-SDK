using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Xml.Linq;
using Koras.Dataverse.FetchXml;
using Koras.Dataverse.Queries;
using Koras.Dataverse.Serialization;

namespace Koras.Dataverse;

/// <content>OData and FetchXML query execution with automatic paging.</content>
public sealed partial class DataverseClient
{
    private const string MoreRecordsAnnotation = "@Microsoft.Dynamics.CRM.morerecords";
    private const string PagingCookieAnnotation = "@Microsoft.Dynamics.CRM.fetchxmlpagingcookie";

    /// <inheritdoc />
    public async Task<DataverseQueryResult> QueryAsync(ODataQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        string options = query.ToQueryString();
        string url = options.Length == 0 ? EntitySet(query.TableName) : EntitySet(query.TableName) + "?" + options;

        var prefer = new List<string>(2) { AnnotationsPreference() };
        if (query.PreferredPageSize is int pageSize)
        {
            prefer.Add($"odata.maxpagesize={pageSize.ToString(CultureInfo.InvariantCulture)}");
        }

        using HttpRequestMessage request = CreateRequest(HttpMethod.Get, url, null, prefer.ToArray());
        using HttpResponseMessage response = await SendAsync(request, "query", query.TableName, cancellationToken).ConfigureAwait(false);
        return await ReadPageAsync(response, query.TableName, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<Entity> QueryAllAsync(ODataQuery query, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        DataverseQueryResult page = await QueryAsync(query, cancellationToken).ConfigureAwait(false);
        while (true)
        {
            foreach (Entity entity in page.Entities)
            {
                yield return entity;
            }

            if (page.NextLink is null)
            {
                yield break;
            }

            page = await QueryNextPageAsync(page.NextLink, query.TableName, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task<DataverseQueryResult> FetchAsync(FetchXmlQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        string url = $"{EntitySet(query.TableName)}?fetchXml={Uri.EscapeDataString(query.Xml)}";
        using HttpRequestMessage request = CreateRequest(HttpMethod.Get, url, null, AnnotationsPreference());
        using HttpResponseMessage response = await SendAsync(request, "fetch", query.TableName, cancellationToken).ConfigureAwait(false);
        return await ReadPageAsync(response, query.TableName, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<Entity> FetchAllAsync(FetchXmlQuery query, int pageSize = 5000, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(pageSize, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, 5000);

        int page = 1;
        string? cookie = null;
        while (true)
        {
            DataverseQueryResult result = await FetchAsync(query.WithPage(page, pageSize, cookie), cancellationToken).ConfigureAwait(false);
            foreach (Entity entity in result.Entities)
            {
                yield return entity;
            }

            if (!result.MoreRecords)
            {
                yield break;
            }

            cookie = result.PagingCookie;
            page++;
        }
    }

    private async Task<DataverseQueryResult> QueryNextPageAsync(string nextLink, string tableName, CancellationToken cancellationToken)
    {
        // Next links are absolute URLs produced by Dataverse itself; they are followed verbatim.
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(nextLink, UriKind.Absolute));
        request.Headers.TryAddWithoutValidation("Prefer", AnnotationsPreference());
        using HttpResponseMessage response = await SendAsync(request, "query", tableName, cancellationToken).ConfigureAwait(false);
        return await ReadPageAsync(response, tableName, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<DataverseQueryResult> ReadPageAsync(HttpResponseMessage response, string tableName, CancellationToken cancellationToken)
    {
        using JsonDocument document = await ReadJsonAsync(response, cancellationToken).ConfigureAwait(false);
        JsonElement root = document.RootElement;

        var entities = new List<Entity>();
        if (root.TryGetProperty("value", out JsonElement value) && value.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in value.EnumerateArray())
            {
                entities.Add(EntityJsonSerializer.ReadEntity(item, tableName));
            }
        }

        string? nextLink = root.TryGetProperty("@odata.nextLink", out JsonElement link) && link.ValueKind == JsonValueKind.String
            ? link.GetString()
            : null;

        long? totalCount = root.TryGetProperty("@odata.count", out JsonElement count) && count.ValueKind == JsonValueKind.Number
            ? count.GetInt64()
            : null;

        bool? moreRecords = root.TryGetProperty(MoreRecordsAnnotation, out JsonElement more) && more.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? more.GetBoolean()
            : null;

        string? pagingCookie = null;
        if (root.TryGetProperty(PagingCookieAnnotation, out JsonElement cookieElement) && cookieElement.ValueKind == JsonValueKind.String)
        {
            pagingCookie = ExtractPagingCookie(cookieElement.GetString());
        }

        return new DataverseQueryResult(entities, nextLink, pagingCookie, totalCount, moreRecords);
    }

    /// <summary>
    /// The paging-cookie annotation wraps the actual cookie in an XML envelope with double
    /// URL-encoding; this extracts the raw cookie ready to send back on the next page request.
    /// </summary>
    internal static string? ExtractPagingCookie(string? annotationValue)
    {
        if (string.IsNullOrWhiteSpace(annotationValue))
        {
            return null;
        }

        try
        {
            XElement envelope = XElement.Parse(annotationValue);
            string? encoded = envelope.Attribute("pagingcookie")?.Value;
            if (string.IsNullOrEmpty(encoded))
            {
                return null;
            }

            return Uri.UnescapeDataString(Uri.UnescapeDataString(encoded));
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }
    }
}
