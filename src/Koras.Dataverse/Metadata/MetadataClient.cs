using System.Text.Json;
using Koras.Dataverse.Queries;

namespace Koras.Dataverse.Metadata;

/// <summary>Web API implementation of <see cref="IMetadataClient"/> over <c>EntityDefinitions</c>.</summary>
internal sealed class MetadataClient : IMetadataClient
{
    private const string TableSelect = "$select=MetadataId,LogicalName,SchemaName,EntitySetName,PrimaryIdAttribute,PrimaryNameAttribute,IsCustomEntity,DisplayName";

    private readonly DataverseClient _client;

    public MetadataClient(DataverseClient client) => _client = client;

    public async Task<TableMetadata> GetTableAsync(string logicalName, CancellationToken cancellationToken = default)
    {
        DataverseNames.ValidTableName(logicalName, nameof(logicalName));
        using JsonDocument document = await GetAsync($"EntityDefinitions(LogicalName='{logicalName}')?{TableSelect}", "metadata.table", logicalName, cancellationToken).ConfigureAwait(false);
        return ReadTable(document.RootElement);
    }

    public async Task<IReadOnlyList<TableMetadata>> GetTablesAsync(CancellationToken cancellationToken = default)
    {
        using JsonDocument document = await GetAsync($"EntityDefinitions?{TableSelect}", "metadata.tables", null, cancellationToken).ConfigureAwait(false);
        var tables = new List<TableMetadata>();
        foreach (JsonElement item in document.RootElement.GetProperty("value").EnumerateArray())
        {
            tables.Add(ReadTable(item));
        }

        return tables;
    }

    public async Task<IReadOnlyList<ColumnMetadata>> GetColumnsAsync(string tableLogicalName, CancellationToken cancellationToken = default)
    {
        DataverseNames.ValidTableName(tableLogicalName, nameof(tableLogicalName));
        string url = $"EntityDefinitions(LogicalName='{tableLogicalName}')/Attributes" +
                     "?$select=MetadataId,LogicalName,SchemaName,AttributeType,IsCustomAttribute,IsPrimaryId,IsPrimaryName,RequiredLevel,DisplayName";
        using JsonDocument document = await GetAsync(url, "metadata.columns", tableLogicalName, cancellationToken).ConfigureAwait(false);

        var columns = new List<ColumnMetadata>();
        foreach (JsonElement item in document.RootElement.GetProperty("value").EnumerateArray())
        {
            columns.Add(new ColumnMetadata
            {
                MetadataId = GetGuid(item, "MetadataId"),
                LogicalName = GetString(item, "LogicalName") ?? string.Empty,
                SchemaName = GetString(item, "SchemaName"),
                DisplayName = GetLabel(item, "DisplayName"),
                AttributeType = GetString(item, "AttributeType"),
                RequiredLevel = item.TryGetProperty("RequiredLevel", out JsonElement level) && level.ValueKind == JsonValueKind.Object
                    ? GetString(level, "Value")
                    : null,
                MaxLength = item.TryGetProperty("MaxLength", out JsonElement max) && max.ValueKind == JsonValueKind.Number
                    ? max.GetInt32()
                    : null,
                IsCustom = GetBool(item, "IsCustomAttribute"),
                IsPrimaryId = GetBool(item, "IsPrimaryId"),
                IsPrimaryName = GetBool(item, "IsPrimaryName"),
            });
        }

        return columns;
    }

    public async Task<IReadOnlyList<RelationshipMetadata>> GetRelationshipsAsync(string tableLogicalName, CancellationToken cancellationToken = default)
    {
        DataverseNames.ValidTableName(tableLogicalName, nameof(tableLogicalName));
        string url = $"EntityDefinitions(LogicalName='{tableLogicalName}')?$select=LogicalName" +
                     "&$expand=OneToManyRelationships($select=MetadataId,SchemaName,ReferencedEntity,ReferencingEntity,ReferencingAttribute)," +
                     "ManyToOneRelationships($select=MetadataId,SchemaName,ReferencedEntity,ReferencingEntity,ReferencingAttribute)," +
                     "ManyToManyRelationships($select=MetadataId,SchemaName,Entity1LogicalName,Entity2LogicalName,IntersectEntityName)";
        using JsonDocument document = await GetAsync(url, "metadata.relationships", tableLogicalName, cancellationToken).ConfigureAwait(false);
        JsonElement root = document.RootElement;

        var relationships = new List<RelationshipMetadata>();
        AddOneToMany(root, "OneToManyRelationships", RelationshipKind.OneToMany, relationships);
        AddOneToMany(root, "ManyToOneRelationships", RelationshipKind.ManyToOne, relationships);

        if (root.TryGetProperty("ManyToManyRelationships", out JsonElement manyToMany) && manyToMany.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in manyToMany.EnumerateArray())
            {
                relationships.Add(new RelationshipMetadata
                {
                    MetadataId = GetGuid(item, "MetadataId"),
                    SchemaName = GetString(item, "SchemaName") ?? string.Empty,
                    Kind = RelationshipKind.ManyToMany,
                    Table1 = GetString(item, "Entity1LogicalName"),
                    Table2 = GetString(item, "Entity2LogicalName"),
                    IntersectTable = GetString(item, "IntersectEntityName"),
                });
            }
        }

        return relationships;
    }

    public async Task<IReadOnlyList<ChoiceOption>> GetChoicesAsync(string tableLogicalName, string columnLogicalName, CancellationToken cancellationToken = default)
    {
        DataverseNames.ValidTableName(tableLogicalName, nameof(tableLogicalName));
        DataverseNames.ValidColumnName(columnLogicalName, nameof(columnLogicalName));
        string url = $"EntityDefinitions(LogicalName='{tableLogicalName}')/Attributes(LogicalName='{columnLogicalName}')" +
                     "/Microsoft.Dynamics.CRM.PicklistAttributeMetadata?$select=LogicalName&$expand=OptionSet($select=Options),GlobalOptionSet($select=Options)";
        using JsonDocument document = await GetAsync(url, "metadata.choices", tableLogicalName, cancellationToken).ConfigureAwait(false);
        JsonElement root = document.RootElement;

        JsonElement optionSet;
        if (root.TryGetProperty("OptionSet", out JsonElement local) && local.ValueKind == JsonValueKind.Object)
        {
            optionSet = local;
        }
        else if (root.TryGetProperty("GlobalOptionSet", out JsonElement global) && global.ValueKind == JsonValueKind.Object)
        {
            optionSet = global;
        }
        else
        {
            return Array.Empty<ChoiceOption>();
        }

        return ReadOptions(optionSet);
    }

    public async Task<IReadOnlyList<ChoiceOption>> GetGlobalChoicesAsync(string choiceName, CancellationToken cancellationToken = default)
    {
        DataverseNames.ValidTableName(choiceName, nameof(choiceName));
        using JsonDocument document = await GetAsync($"GlobalOptionSetDefinitions(Name='{choiceName}')", "metadata.globalchoices", null, cancellationToken).ConfigureAwait(false);
        return ReadOptions(document.RootElement);
    }

    public async Task<string> GetEntitySetNameAsync(string tableLogicalName, CancellationToken cancellationToken = default)
    {
        DataverseNames.ValidTableName(tableLogicalName, nameof(tableLogicalName));
        using JsonDocument document = await GetAsync($"EntityDefinitions(LogicalName='{tableLogicalName}')?$select=EntitySetName", "metadata.entityset", tableLogicalName, cancellationToken).ConfigureAwait(false);
        return GetString(document.RootElement, "EntitySetName")
            ?? throw new Errors.DataverseException(new Errors.DataverseError
            {
                Category = Errors.DataverseErrorCategory.Unknown,
                Message = $"Dataverse did not return an EntitySetName for table '{tableLogicalName}'.",
            });
    }

    private async Task<JsonDocument> GetAsync(string url, string operation, string? table, CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = DataverseClient.CreateRequest(HttpMethod.Get, url);
        using HttpResponseMessage response = await _client.SendAsync(request, operation, table, cancellationToken).ConfigureAwait(false);
        return await DataverseClient.ReadJsonAsync(response, cancellationToken).ConfigureAwait(false);
    }

    private static void AddOneToMany(JsonElement root, string property, RelationshipKind kind, List<RelationshipMetadata> relationships)
    {
        if (!root.TryGetProperty(property, out JsonElement array) || array.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (JsonElement item in array.EnumerateArray())
        {
            relationships.Add(new RelationshipMetadata
            {
                MetadataId = GetGuid(item, "MetadataId"),
                SchemaName = GetString(item, "SchemaName") ?? string.Empty,
                Kind = kind,
                ReferencedTable = GetString(item, "ReferencedEntity"),
                ReferencingTable = GetString(item, "ReferencingEntity"),
                ReferencingColumn = GetString(item, "ReferencingAttribute"),
            });
        }
    }

    private static TableMetadata ReadTable(JsonElement element) => new()
    {
        MetadataId = GetGuid(element, "MetadataId"),
        LogicalName = GetString(element, "LogicalName") ?? string.Empty,
        SchemaName = GetString(element, "SchemaName"),
        DisplayName = GetLabel(element, "DisplayName"),
        EntitySetName = GetString(element, "EntitySetName"),
        PrimaryIdAttribute = GetString(element, "PrimaryIdAttribute"),
        PrimaryNameAttribute = GetString(element, "PrimaryNameAttribute"),
        IsCustom = GetBool(element, "IsCustomEntity"),
    };

    private static IReadOnlyList<ChoiceOption> ReadOptions(JsonElement optionSet)
    {
        if (!optionSet.TryGetProperty("Options", out JsonElement options) || options.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<ChoiceOption>();
        }

        var choices = new List<ChoiceOption>();
        foreach (JsonElement option in options.EnumerateArray())
        {
            int value = option.TryGetProperty("Value", out JsonElement v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : 0;
            string label = GetLabel(option, "Label") ?? value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            choices.Add(new ChoiceOption(value, label) { Color = GetString(option, "Color") });
        }

        return choices;
    }

    private static string? GetLabel(JsonElement element, string property)
    {
        if (element.TryGetProperty(property, out JsonElement label) && label.ValueKind == JsonValueKind.Object &&
            label.TryGetProperty("UserLocalizedLabel", out JsonElement localized) && localized.ValueKind == JsonValueKind.Object &&
            localized.TryGetProperty("Label", out JsonElement text) && text.ValueKind == JsonValueKind.String)
        {
            return text.GetString();
        }

        return null;
    }

    private static string? GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static bool GetBool(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out JsonElement value))
        {
            return false;
        }

        // Managed properties (IsCustomEntity, IsCustomAttribute) may arrive as {"Value": bool}.
        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.Object when value.TryGetProperty("Value", out JsonElement inner) => inner.ValueKind == JsonValueKind.True,
            _ => false,
        };
    }

    private static Guid GetGuid(JsonElement element, string property) =>
        element.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.String && Guid.TryParse(value.GetString(), out Guid id)
            ? id
            : Guid.Empty;
}
