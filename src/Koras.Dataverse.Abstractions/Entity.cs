using System.Globalization;

namespace Koras.Dataverse;

/// <summary>
/// A late-bound Dataverse row: a table logical name, an id, and a bag of attribute values.
/// Values use plain CLR types (<see cref="string"/>, <see cref="int"/>, <see cref="decimal"/>,
/// <see cref="bool"/>, <see cref="DateTimeOffset"/>, <see cref="Guid"/>) and
/// <see cref="EntityReference"/> for lookups. Choice columns are <see cref="int"/> values; money
/// columns are <see cref="decimal"/> values.
/// </summary>
/// <remarks>Instances are not thread-safe; do not mutate a shared instance concurrently.</remarks>
public sealed class Entity
{
    /// <summary>Creates a new row for the given table.</summary>
    /// <param name="tableName">The table's logical name, for example <c>account</c>.</param>
    public Entity(string tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException("A table logical name must be provided.", nameof(tableName));
        }

        TableName = tableName;
    }

    /// <summary>Creates a row for the given table with a known id.</summary>
    public Entity(string tableName, Guid id)
        : this(tableName)
    {
        Id = id;
    }

    /// <summary>The table's logical name.</summary>
    public string TableName { get; }

    /// <summary>The row's primary key. <see cref="Guid.Empty"/> when not yet created.</summary>
    public Guid Id { get; set; }

    /// <summary>The attribute values keyed by column logical name.</summary>
    public IDictionary<string, object?> Attributes { get; } = new Dictionary<string, object?>(StringComparer.Ordinal);

    /// <summary>
    /// Display-formatted values returned by Dataverse (for example choice labels and formatted
    /// dates), keyed by column logical name. Populated on retrieve/query when annotations are
    /// enabled; not sent on write.
    /// </summary>
    public IDictionary<string, string> FormattedValues { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Gets or sets an attribute value. Reading a missing attribute returns null.</summary>
    /// <param name="attributeName">The column logical name.</param>
    public object? this[string attributeName]
    {
        get => Attributes.TryGetValue(attributeName, out object? value) ? value : null;
        set => Attributes[attributeName] = value;
    }

    /// <summary>
    /// Returns an attribute value converted to <typeparamref name="T"/>. Missing attributes and
    /// null values return <c>default</c>. Numeric widening, <see cref="Guid"/>/<see cref="DateTimeOffset"/>
    /// parsing from strings, and enum conversion from integers are supported.
    /// </summary>
    /// <exception cref="InvalidCastException">The stored value cannot be converted to <typeparamref name="T"/>.</exception>
    public T? GetValue<T>(string attributeName)
    {
        if (!Attributes.TryGetValue(attributeName, out object? value) || value is null)
        {
            return default;
        }

        return ConvertValue<T>(attributeName, value);
    }

    /// <summary>Attempts to read an attribute value converted to <typeparamref name="T"/>.</summary>
    /// <returns><c>true</c> when the attribute exists, is non-null, and converts successfully.</returns>
    public bool TryGetValue<T>(string attributeName, out T value)
    {
        if (Attributes.TryGetValue(attributeName, out object? stored) && stored is not null)
        {
            try
            {
                value = ConvertValue<T>(attributeName, stored)!;
                return true;
            }
            catch (InvalidCastException)
            {
                // fall through to the failure return below
            }
        }

        value = default!;
        return false;
    }

    /// <summary>Returns an <see cref="EntityReference"/> pointing at this row.</summary>
    /// <exception cref="InvalidOperationException">The row has no id yet.</exception>
    public EntityReference ToReference()
    {
        if (Id == Guid.Empty)
        {
            throw new InvalidOperationException($"This '{TableName}' row has no id yet; create it first or set Entity.Id.");
        }

        return new EntityReference(TableName, Id);
    }

    private static T? ConvertValue<T>(string attributeName, object value)
    {
        if (value is T typed)
        {
            return typed;
        }

        Type target = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);

        try
        {
            if (value is T t)
            {
                return t;
            }

            if (target == typeof(Guid))
            {
                return value is string s ? (T)(object)Guid.Parse(s) : throw Incompatible();
            }

            if (target == typeof(DateTimeOffset))
            {
                return value switch
                {
                    string s => (T)(object)DateTimeOffset.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                    DateTime dt => (T)(object)new DateTimeOffset(dt.ToUniversalTime()),
                    _ => throw Incompatible(),
                };
            }

            if (target == typeof(DateTime))
            {
                return value switch
                {
                    string s => (T)(object)DateTime.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                    DateTimeOffset dto => (T)(object)dto.UtcDateTime,
                    _ => throw Incompatible(),
                };
            }

            if (target.IsEnum && value is IConvertible)
            {
                return (T)Enum.ToObject(target, Convert.ToInt64(value, CultureInfo.InvariantCulture));
            }

            if (value is IConvertible)
            {
                return (T)Convert.ChangeType(value, target, CultureInfo.InvariantCulture);
            }
        }
        catch (Exception exception) when (exception is FormatException or OverflowException or InvalidCastException)
        {
            throw Incompatible(exception);
        }

        throw Incompatible();

        InvalidCastException Incompatible(Exception? inner = null) => new(
            $"Attribute '{attributeName}' holds a value of type '{value.GetType().Name}' and cannot be converted to '{typeof(T).Name}'.",
            inner);
    }
}
