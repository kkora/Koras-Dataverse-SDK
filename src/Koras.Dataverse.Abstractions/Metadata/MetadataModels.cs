namespace Koras.Dataverse.Metadata;

/// <summary>Summary metadata of a Dataverse table.</summary>
public sealed record TableMetadata
{
    /// <summary>The metadata row id.</summary>
    public Guid MetadataId { get; init; }

    /// <summary>The table logical name (lowercase), for example <c>account</c>.</summary>
    public required string LogicalName { get; init; }

    /// <summary>The schema name, for example <c>Account</c>.</summary>
    public string? SchemaName { get; init; }

    /// <summary>The localized display name, when available.</summary>
    public string? DisplayName { get; init; }

    /// <summary>The Web API entity set name, for example <c>accounts</c>.</summary>
    public string? EntitySetName { get; init; }

    /// <summary>The logical name of the primary key column.</summary>
    public string? PrimaryIdAttribute { get; init; }

    /// <summary>The logical name of the primary name column.</summary>
    public string? PrimaryNameAttribute { get; init; }

    /// <summary>Whether the table is a custom table.</summary>
    public bool IsCustom { get; init; }
}

/// <summary>Summary metadata of a Dataverse column.</summary>
public sealed record ColumnMetadata
{
    /// <summary>The metadata row id.</summary>
    public Guid MetadataId { get; init; }

    /// <summary>The column logical name (lowercase).</summary>
    public required string LogicalName { get; init; }

    /// <summary>The schema name.</summary>
    public string? SchemaName { get; init; }

    /// <summary>The localized display name, when available.</summary>
    public string? DisplayName { get; init; }

    /// <summary>The attribute type name reported by Dataverse (for example <c>String</c>, <c>Picklist</c>, <c>Lookup</c>).</summary>
    public string? AttributeType { get; init; }

    /// <summary>The requirement level (<c>None</c>, <c>ApplicationRequired</c>, …), when available.</summary>
    public string? RequiredLevel { get; init; }

    /// <summary>The maximum length for text columns, when applicable.</summary>
    public int? MaxLength { get; init; }

    /// <summary>Whether the column is a custom column.</summary>
    public bool IsCustom { get; init; }

    /// <summary>Whether the column is the table's primary key.</summary>
    public bool IsPrimaryId { get; init; }

    /// <summary>Whether the column is the table's primary name.</summary>
    public bool IsPrimaryName { get; init; }
}

/// <summary>The kind of a Dataverse relationship.</summary>
public enum RelationshipKind
{
    /// <summary>One-to-many (this table is the "one" side).</summary>
    OneToMany,

    /// <summary>Many-to-one (this table is the "many" side).</summary>
    ManyToOne,

    /// <summary>Many-to-many.</summary>
    ManyToMany,
}

/// <summary>Summary metadata of a Dataverse relationship.</summary>
public sealed record RelationshipMetadata
{
    /// <summary>The metadata row id.</summary>
    public Guid MetadataId { get; init; }

    /// <summary>The relationship schema name, for example <c>account_primary_contact</c>.</summary>
    public required string SchemaName { get; init; }

    /// <summary>The relationship kind.</summary>
    public required RelationshipKind Kind { get; init; }

    /// <summary>The referenced ("one") table for 1:N / N:1 relationships.</summary>
    public string? ReferencedTable { get; init; }

    /// <summary>The referencing ("many") table for 1:N / N:1 relationships.</summary>
    public string? ReferencingTable { get; init; }

    /// <summary>The lookup column on the referencing table for 1:N / N:1 relationships.</summary>
    public string? ReferencingColumn { get; init; }

    /// <summary>The first table of an N:N relationship.</summary>
    public string? Table1 { get; init; }

    /// <summary>The second table of an N:N relationship.</summary>
    public string? Table2 { get; init; }

    /// <summary>The intersect table of an N:N relationship.</summary>
    public string? IntersectTable { get; init; }
}

/// <summary>One option of a choice (option set) column.</summary>
/// <param name="Value">The stored integer value.</param>
/// <param name="Label">The localized label.</param>
public sealed record ChoiceOption(int Value, string Label)
{
    /// <summary>The option color, when defined (hex string such as <c>#0000ff</c>).</summary>
    public string? Color { get; init; }
}
