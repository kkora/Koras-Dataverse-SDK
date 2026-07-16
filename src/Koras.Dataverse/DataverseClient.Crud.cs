using System.Globalization;
using System.Net;
using System.Text.Json;
using Koras.Dataverse.Queries;
using Koras.Dataverse.Serialization;

namespace Koras.Dataverse;

/// <content>Create, retrieve, update, upsert, delete, and association operations.</content>
public sealed partial class DataverseClient
{
    /// <inheritdoc />
    public async Task<Guid> CreateAsync(Entity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        using HttpRequestMessage request = CreateRequest(HttpMethod.Post, EntitySet(entity.TableName), _serializer.WritePayload(entity));
        using HttpResponseMessage response = await SendAsync(request, "create", entity.TableName, cancellationToken).ConfigureAwait(false);
        Guid id = ReadEntityIdHeader(response);
        entity.Id = id;
        return id;
    }

    /// <inheritdoc />
    public async Task<Entity> CreateAndReturnAsync(Entity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        using HttpRequestMessage request = CreateRequest(
            HttpMethod.Post,
            EntitySet(entity.TableName),
            _serializer.WritePayload(entity),
            "return=representation",
            AnnotationsPreference());
        using HttpResponseMessage response = await SendAsync(request, "create", entity.TableName, cancellationToken).ConfigureAwait(false);
        using JsonDocument document = await ReadJsonAsync(response, cancellationToken).ConfigureAwait(false);
        Entity created = EntityJsonSerializer.ReadEntity(document.RootElement, entity.TableName);
        entity.Id = created.Id;
        return created;
    }

    /// <inheritdoc />
    public async Task<Entity> RetrieveAsync(string tableName, Guid id, ColumnSet? columns = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        string url = $"{EntitySet(tableName)}({id:D})" + SelectClause(columns);
        using HttpRequestMessage request = CreateRequest(HttpMethod.Get, url, null, AnnotationsPreference());
        using HttpResponseMessage response = await SendAsync(request, "retrieve", tableName, cancellationToken).ConfigureAwait(false);
        using JsonDocument document = await ReadJsonAsync(response, cancellationToken).ConfigureAwait(false);
        return EntityJsonSerializer.ReadEntity(document.RootElement, tableName);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(Entity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (entity.Id == Guid.Empty)
        {
            throw new ArgumentException("The entity must have an id to be updated.", nameof(entity));
        }

        using HttpRequestMessage request = CreateRequest(
            HttpMethod.Patch,
            $"{EntitySet(entity.TableName)}({entity.Id:D})",
            _serializer.WritePayload(entity));
        request.Headers.TryAddWithoutValidation("If-Match", "*"); // strict update: never create
        using HttpResponseMessage response = await SendAsync(request, "update", entity.TableName, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<UpsertResult> UpsertAsync(Entity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (entity.Id == Guid.Empty)
        {
            throw new ArgumentException("The entity must have an id to be upserted; use the alternate-key overload otherwise.", nameof(entity));
        }

        return await UpsertCoreAsync(entity, $"{EntitySet(entity.TableName)}({entity.Id:D})", cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<UpsertResult> UpsertAsync(Entity entity, IReadOnlyDictionary<string, object> alternateKey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(alternateKey);
        if (alternateKey.Count == 0)
        {
            throw new ArgumentException("Provide at least one alternate key column.", nameof(alternateKey));
        }

        string keySegment = string.Join(",", alternateKey.Select(pair =>
            $"{DataverseNames.ValidColumnName(pair.Key, nameof(alternateKey))}={KeyLiteral(pair.Value)}"));
        return await UpsertCoreAsync(entity, $"{EntitySet(entity.TableName)}({keySegment})", cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string tableName, Guid id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        using HttpRequestMessage request = CreateRequest(HttpMethod.Delete, $"{EntitySet(tableName)}({id:D})");
        using HttpResponseMessage response = await SendAsync(request, "delete", tableName, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task DeleteAsync(EntityReference reference, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return DeleteAsync(reference.TableName, reference.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task AssociateAsync(EntityReference primary, string relationshipName, EntityReference related, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(primary);
        ArgumentNullException.ThrowIfNull(related);
        DataverseNames.ValidColumnName(relationshipName, nameof(relationshipName));

        string payload = $"{{\"@odata.id\":\"{AbsoluteUrl($"{EntitySet(related.TableName)}({related.Id:D})")}\"}}";
        using HttpRequestMessage request = CreateRequest(
            HttpMethod.Post,
            $"{EntitySet(primary.TableName)}({primary.Id:D})/{relationshipName}/$ref",
            payload);
        using HttpResponseMessage response = await SendAsync(request, "associate", primary.TableName, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DisassociateAsync(EntityReference primary, string relationshipName, EntityReference related, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(primary);
        ArgumentNullException.ThrowIfNull(related);
        DataverseNames.ValidColumnName(relationshipName, nameof(relationshipName));

        string relatedUrl = AbsoluteUrl($"{EntitySet(related.TableName)}({related.Id:D})").ToString();
        using HttpRequestMessage request = CreateRequest(
            HttpMethod.Delete,
            $"{EntitySet(primary.TableName)}({primary.Id:D})/{relationshipName}/$ref?$id={Uri.EscapeDataString(relatedUrl)}");
        using HttpResponseMessage response = await SendAsync(request, "disassociate", primary.TableName, cancellationToken).ConfigureAwait(false);
    }

    private async Task<UpsertResult> UpsertCoreAsync(Entity entity, string url, CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = CreateRequest(HttpMethod.Patch, url, _serializer.WritePayload(entity), "return=representation");
        using HttpResponseMessage response = await SendAsync(request, "upsert", entity.TableName, cancellationToken).ConfigureAwait(false);

        bool created = response.StatusCode == HttpStatusCode.Created;
        Guid id = entity.Id;
        if (response.Content.Headers.ContentLength is > 0)
        {
            using JsonDocument document = await ReadJsonAsync(response, cancellationToken).ConfigureAwait(false);
            Entity returned = EntityJsonSerializer.ReadEntity(document.RootElement, entity.TableName);
            if (returned.Id != Guid.Empty)
            {
                id = returned.Id;
            }
        }
        else if (id == Guid.Empty && TryReadEntityIdHeader(response, out Guid headerId))
        {
            id = headerId;
        }

        entity.Id = id;
        return new UpsertResult(id, created);
    }

    private static string SelectClause(ColumnSet? columns)
    {
        if (columns is null || columns.IsAll)
        {
            return string.Empty;
        }

        foreach (string column in columns.Columns)
        {
            DataverseNames.ValidColumnName(column, nameof(columns));
        }

        return "?$select=" + Uri.EscapeDataString(string.Join(",", columns.Columns));
    }

    private static string KeyLiteral(object value) => value switch
    {
        null => throw new ArgumentException("Alternate key values cannot be null."),
        string s => "'" + Uri.EscapeDataString(s.Replace("'", "''", StringComparison.Ordinal)) + "'",
        Guid g => g.ToString("D"),
        int or long or short => Convert.ToString(value, CultureInfo.InvariantCulture)!,
        _ => Uri.EscapeDataString(ODataFilterBuilder.Literal(value)),
    };

    private static Guid ReadEntityIdHeader(HttpResponseMessage response) =>
        TryReadEntityIdHeader(response, out Guid id)
            ? id
            : throw new Errors.DataverseException(new Errors.DataverseError
            {
                Category = Errors.DataverseErrorCategory.Unknown,
                Message = "Dataverse did not return an OData-EntityId header for the created row.",
                HttpStatusCode = (int)response.StatusCode,
            });

    private static bool TryReadEntityIdHeader(HttpResponseMessage response, out Guid id)
    {
        id = Guid.Empty;
        if (!response.Headers.TryGetValues("OData-EntityId", out IEnumerable<string>? values))
        {
            return false;
        }

        string? value = values.FirstOrDefault();
        if (value is null)
        {
            return false;
        }

        int open = value.LastIndexOf('(');
        int close = value.LastIndexOf(')');
        return open >= 0 && close > open && Guid.TryParse(value[(open + 1)..close], out id);
    }
}
