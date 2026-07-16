namespace Koras.Dataverse.Metadata;

/// <summary>
/// Read-only access to Dataverse table, column, relationship, and choice metadata.
/// Exposed via <c>IDataverseClient.Metadata</c>.
/// </summary>
/// <remarks>Implementations are thread-safe.</remarks>
public interface IMetadataClient
{
    /// <summary>Returns the metadata of a single table.</summary>
    /// <exception cref="Errors.DataverseException">The table does not exist (category <c>NotFound</c>) or the call failed.</exception>
    Task<TableMetadata> GetTableAsync(string logicalName, CancellationToken cancellationToken = default);

    /// <summary>Returns summary metadata for all tables in the environment.</summary>
    Task<IReadOnlyList<TableMetadata>> GetTablesAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns the columns of a table.</summary>
    Task<IReadOnlyList<ColumnMetadata>> GetColumnsAsync(string tableLogicalName, CancellationToken cancellationToken = default);

    /// <summary>Returns the one-to-many, many-to-one, and many-to-many relationships of a table.</summary>
    Task<IReadOnlyList<RelationshipMetadata>> GetRelationshipsAsync(string tableLogicalName, CancellationToken cancellationToken = default);

    /// <summary>Returns the options of a local choice column.</summary>
    Task<IReadOnlyList<ChoiceOption>> GetChoicesAsync(string tableLogicalName, string columnLogicalName, CancellationToken cancellationToken = default);

    /// <summary>Returns the options of a global choice by name.</summary>
    Task<IReadOnlyList<ChoiceOption>> GetGlobalChoicesAsync(string choiceName, CancellationToken cancellationToken = default);

    /// <summary>Returns the Web API entity set name of a table (for example <c>accounts</c> for <c>account</c>).</summary>
    Task<string> GetEntitySetNameAsync(string tableLogicalName, CancellationToken cancellationToken = default);
}
