namespace Koras.Dataverse;

/// <summary>
/// The set of columns to return from a retrieve operation. Prefer explicit columns; retrieving
/// all columns costs bandwidth and API allowance.
/// </summary>
public sealed class ColumnSet
{
    private ColumnSet(IReadOnlyList<string> columns, bool isAll)
    {
        Columns = columns;
        IsAll = isAll;
    }

    /// <summary>All columns. Use sparingly.</summary>
    public static ColumnSet All { get; } = new ColumnSet(Array.Empty<string>(), isAll: true);

    /// <summary>Selects the given columns by logical name.</summary>
    public static ColumnSet Of(params string[] columns)
    {
        ArgumentNullException.ThrowIfNull(columns);
        if (columns.Length == 0)
        {
            throw new ArgumentException("Provide at least one column, or use ColumnSet.All.", nameof(columns));
        }

        return new ColumnSet(columns.ToArray(), isAll: false);
    }

    /// <summary>The selected column logical names. Empty when <see cref="IsAll"/> is true.</summary>
    public IReadOnlyList<string> Columns { get; }

    /// <summary>Whether every column is returned.</summary>
    public bool IsAll { get; }
}
