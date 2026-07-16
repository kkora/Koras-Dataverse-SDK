using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Koras.Dataverse.Serialization;

/// <summary>
/// Converts between the SDK's <see cref="Entity"/> model and Dataverse Web API JSON.
/// Lookup values are written as <c>@odata.bind</c> references; display annotations are read into
/// <see cref="Entity.FormattedValues"/> and lookup columns into <see cref="EntityReference"/>.
/// </summary>
internal sealed class EntityJsonSerializer
{
    private const string FormattedValueAnnotation = "@OData.Community.Display.V1.FormattedValue";
    private const string LookupLogicalNameAnnotation = "@Microsoft.Dynamics.CRM.lookuplogicalname";

    private readonly EntitySetNameResolver _resolver;

    public EntityJsonSerializer(EntitySetNameResolver resolver) => _resolver = resolver;

    /// <summary>Serializes an entity's attributes into a Web API JSON payload.</summary>
    public string WritePayload(Entity entity)
    {
        var json = new JsonObject();
        foreach (KeyValuePair<string, object?> attribute in entity.Attributes)
        {
            if (attribute.Value is EntityReference reference)
            {
                json[attribute.Key + "@odata.bind"] = $"/{_resolver.Resolve(reference.TableName)}({reference.Id:D})";
                continue;
            }

            json[attribute.Key] = ToJsonValue(attribute.Key, attribute.Value);
        }

        return json.ToJsonString();
    }

    private static JsonValue? ToJsonValue(string attributeName, object? value) => value switch
    {
        null => null,
        string s => JsonValue.Create(s),
        bool b => JsonValue.Create(b),
        int i => JsonValue.Create(i),
        long l => JsonValue.Create(l),
        decimal m => JsonValue.Create(m),
        double d => JsonValue.Create(d),
        float f => JsonValue.Create(f),
        Guid g => JsonValue.Create(g.ToString("D")),
        DateTimeOffset dto => JsonValue.Create(dto.UtcDateTime.ToString("o", CultureInfo.InvariantCulture)),
        DateTime dt => JsonValue.Create(dt.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture)),
        DateOnly date => JsonValue.Create(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
        byte[] bytes => JsonValue.Create(Convert.ToBase64String(bytes)),
        Enum e => JsonValue.Create(Convert.ToInt32(e, CultureInfo.InvariantCulture)),
        _ => throw new NotSupportedException(
            $"Attribute '{attributeName}' has unsupported value type '{value.GetType().Name}'. " +
            "Use plain CLR values (string, numbers, bool, Guid, DateTimeOffset, byte[]) or EntityReference for lookups."),
    };

    /// <summary>Materializes an entity from a Web API JSON object.</summary>
    public static Entity ReadEntity(JsonElement element, string tableName)
    {
        var entity = new Entity(tableName);
        string primaryIdAttribute = tableName + "id";
        var lookupTables = new Dictionary<string, string>(StringComparer.Ordinal);

        // First pass: collect annotations so lookups can be materialized with names.
        foreach (JsonProperty property in element.EnumerateObject())
        {
            int annotationIndex = property.Name.IndexOf('@', StringComparison.Ordinal);
            if (annotationIndex <= 0)
            {
                continue;
            }

            string baseName = property.Name[..annotationIndex];
            string annotation = property.Name[annotationIndex..];
            if (annotation.Equals(FormattedValueAnnotation, StringComparison.OrdinalIgnoreCase) &&
                property.Value.ValueKind == JsonValueKind.String)
            {
                entity.FormattedValues[baseName] = property.Value.GetString()!;
            }
            else if (annotation.Equals(LookupLogicalNameAnnotation, StringComparison.OrdinalIgnoreCase) &&
                     property.Value.ValueKind == JsonValueKind.String)
            {
                lookupTables[baseName] = property.Value.GetString()!;
            }
        }

        // Second pass: materialize values.
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (property.Name.Contains('@', StringComparison.Ordinal))
            {
                continue; // annotation or OData control data
            }

            object? value = ReadValue(property.Value, property.Name, lookupTables, entity.FormattedValues);
            entity[property.Name] = value;

            if (value is Guid id && property.Name.Equals(primaryIdAttribute, StringComparison.Ordinal))
            {
                entity.Id = id;
            }
        }

        return entity;
    }

    private static object? ReadValue(
        JsonElement value,
        string name,
        Dictionary<string, string> lookupTables,
        IDictionary<string, string> formattedValues)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Null or JsonValueKind.Undefined:
                return null;
            case JsonValueKind.True:
                return true;
            case JsonValueKind.False:
                return false;
            case JsonValueKind.Number:
                if (value.TryGetInt32(out int i))
                {
                    return i;
                }

                if (value.TryGetInt64(out long l))
                {
                    return l;
                }

                return value.GetDecimal();
            case JsonValueKind.String:
                string text = value.GetString()!;
                if (Guid.TryParseExact(text, "D", out Guid guid))
                {
                    if (lookupTables.TryGetValue(name, out string? lookupTable))
                    {
                        formattedValues.TryGetValue(name, out string? display);
                        return new EntityReference(lookupTable, guid) { Name = display };
                    }

                    return guid;
                }

                return text;
            case JsonValueKind.Object:
                // Expanded navigation property: materialize a nested entity. The table name is not
                // part of the payload; the navigation property name is used as a stand-in.
                return ReadEntity(value, SafeNestedTableName(name));
            case JsonValueKind.Array:
                var list = new List<object?>(value.GetArrayLength());
                foreach (JsonElement item in value.EnumerateArray())
                {
                    list.Add(item.ValueKind == JsonValueKind.Object
                        ? ReadEntity(item, SafeNestedTableName(name))
                        : ReadValue(item, name, lookupTables, formattedValues));
                }

                return list;
            default:
                return null;
        }
    }

    private static string SafeNestedTableName(string navigationProperty)
    {
        // Navigation properties may carry casing/underscores not valid in a logical name; the
        // Entity constructor only rejects empty values, so pass the property name through.
        return navigationProperty.Length == 0 ? "unknown" : navigationProperty;
    }
}
