namespace Koras.Dataverse.Mapping;

/// <summary>Maps a class to a Dataverse table for use with <see cref="EntityMapper"/>.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class DataverseTableAttribute : Attribute
{
    /// <summary>Declares the table logical name.</summary>
    public DataverseTableAttribute(string logicalName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logicalName);
        LogicalName = logicalName;
    }

    /// <summary>The table logical name, for example <c>account</c>.</summary>
    public string LogicalName { get; }
}

/// <summary>Maps a property to a Dataverse column for use with <see cref="EntityMapper"/>.</summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class DataverseColumnAttribute : Attribute
{
    /// <summary>Declares the column logical name.</summary>
    public DataverseColumnAttribute(string logicalName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logicalName);
        LogicalName = logicalName;
    }

    /// <summary>The column logical name, for example <c>name</c>.</summary>
    public string LogicalName { get; }
}
