using System.Collections.Concurrent;
using System.Reflection;

namespace Koras.Dataverse.Mapping;

/// <summary>
/// Converts between late-bound <see cref="Entity"/> instances and POCOs annotated with
/// <see cref="DataverseTableAttribute"/> and <see cref="DataverseColumnAttribute"/>.
/// Reflection metadata is cached per type; conversion allocates only the target instance.
/// </summary>
/// <example>
/// <code>
/// [DataverseTable("account")]
/// public sealed class Account
/// {
///     [DataverseColumn("accountid")] public Guid Id { get; set; }
///     [DataverseColumn("name")] public string? Name { get; set; }
///     [DataverseColumn("revenue")] public decimal? Revenue { get; set; }
/// }
///
/// Account account = entity.ToObject&lt;Account&gt;();
/// Entity row = EntityMapper.ToEntity(account);
/// </code>
/// </example>
public static class EntityMapper
{
    private static readonly ConcurrentDictionary<Type, TypeMap> Maps = new();

    /// <summary>Creates a POCO of type <typeparamref name="T"/> from an entity's attributes.</summary>
    /// <exception cref="InvalidOperationException"><typeparamref name="T"/> is not annotated with <see cref="DataverseTableAttribute"/>.</exception>
    public static T ToObject<T>(this Entity entity)
        where T : new()
    {
        ArgumentNullException.ThrowIfNull(entity);
        TypeMap map = GetMap(typeof(T));
        var result = new T();

        foreach (PropertyMap property in map.Properties)
        {
            object? value = property.IsPrimaryId && entity.Id != Guid.Empty
                ? entity.Id
                : entity[property.ColumnName];

            if (value is null)
            {
                continue;
            }

            property.Set(result, value);
        }

        return result;
    }

    /// <summary>Creates a late-bound <see cref="Entity"/> from an annotated POCO.</summary>
    /// <remarks>Null property values are skipped (they are not sent as column clears). To clear a
    /// column explicitly, set <c>entity["column"] = null</c> on the returned entity.</remarks>
    public static Entity ToEntity<T>(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        TypeMap map = GetMap(typeof(T));
        var entity = new Entity(map.TableName);

        foreach (PropertyMap property in map.Properties)
        {
            object? propertyValue = property.Get(value);
            if (propertyValue is null)
            {
                continue;
            }

            if (property.IsPrimaryId)
            {
                if (propertyValue is Guid id && id != Guid.Empty)
                {
                    entity.Id = id;
                }

                continue;
            }

            entity[property.ColumnName] = propertyValue;
        }

        return entity;
    }

    /// <summary>Returns the table logical name declared on <typeparamref name="T"/>.</summary>
    public static string TableNameOf<T>() => GetMap(typeof(T)).TableName;

    private static TypeMap GetMap(Type type) => Maps.GetOrAdd(type, static t =>
    {
        DataverseTableAttribute? table = t.GetCustomAttribute<DataverseTableAttribute>();
        if (table is null)
        {
            throw new InvalidOperationException($"Type '{t.Name}' is not annotated with [DataverseTable].");
        }

        string primaryId = table.LogicalName + "id";
        var properties = new List<PropertyMap>();
        foreach (PropertyInfo property in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            DataverseColumnAttribute? column = property.GetCustomAttribute<DataverseColumnAttribute>();
            if (column is null || !property.CanRead || !property.CanWrite)
            {
                continue;
            }

            properties.Add(new PropertyMap(property, column.LogicalName, string.Equals(column.LogicalName, primaryId, StringComparison.Ordinal)));
        }

        return new TypeMap(table.LogicalName, properties);
    });

    private sealed class TypeMap
    {
        public TypeMap(string tableName, IReadOnlyList<PropertyMap> properties)
        {
            TableName = tableName;
            Properties = properties;
        }

        public string TableName { get; }

        public IReadOnlyList<PropertyMap> Properties { get; }
    }

    private sealed class PropertyMap
    {
        private readonly PropertyInfo _property;

        public PropertyMap(PropertyInfo property, string columnName, bool isPrimaryId)
        {
            _property = property;
            ColumnName = columnName;
            IsPrimaryId = isPrimaryId;
        }

        public string ColumnName { get; }

        public bool IsPrimaryId { get; }

        public object? Get(object instance) => _property.GetValue(instance);

        public void Set(object instance, object value)
        {
            Type target = Nullable.GetUnderlyingType(_property.PropertyType) ?? _property.PropertyType;

            object converted;
            if (target.IsInstanceOfType(value))
            {
                converted = value;
            }
            else if (target == typeof(Guid) && value is string s)
            {
                converted = Guid.Parse(s);
            }
            else if (target.IsEnum)
            {
                converted = Enum.ToObject(target, Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture));
            }
            else if (value is IConvertible)
            {
                converted = Convert.ChangeType(value, target, System.Globalization.CultureInfo.InvariantCulture);
            }
            else
            {
                throw new InvalidCastException(
                    $"Column '{ColumnName}' value of type '{value.GetType().Name}' cannot be assigned to property '{_property.Name}' ({_property.PropertyType.Name}).");
            }

            _property.SetValue(instance, converted);
        }
    }
}
