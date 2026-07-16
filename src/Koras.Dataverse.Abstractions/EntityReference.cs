namespace Koras.Dataverse;

/// <summary>
/// A reference to a Dataverse row: table logical name plus id. Used as the value of lookup
/// columns and as a compact way to address rows.
/// </summary>
/// <param name="TableName">The table's logical name, for example <c>contact</c>.</param>
/// <param name="Id">The referenced row's primary key.</param>
public sealed record EntityReference(string TableName, Guid Id)
{
    /// <summary>The display name of the referenced row, when known (populated from query annotations).</summary>
    public string? Name { get; init; }

    /// <summary>Returns <c>tableName(id)</c>, for example <c>contact(0f3f3f…)</c>.</summary>
    public override string ToString() => $"{TableName}({Id:D})";
}
