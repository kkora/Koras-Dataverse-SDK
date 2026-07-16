using Koras.Dataverse.Batches;
using Koras.Dataverse.FetchXml;
using Koras.Dataverse.Metadata;
using Koras.Dataverse.Queries;
using Koras.Dataverse.Solutions;

namespace Koras.Dataverse;

/// <summary>
/// The Dataverse client: CRUD, queries (OData and FetchXML) with automatic paging, batch
/// execution, association management, metadata, and solution operations against one environment.
/// </summary>
/// <remarks>
/// <para>Implementations are thread-safe and intended to be registered as singletons.</para>
/// <para>All failures surface as <see cref="Errors.DataverseException"/> (categorized via
/// <see cref="Errors.DataverseError"/>); cancellation surfaces as
/// <see cref="OperationCanceledException"/>.</para>
/// </remarks>
public interface IDataverseClient
{
    /// <summary>Metadata helpers for this environment.</summary>
    IMetadataClient Metadata { get; }

    /// <summary>Solution helpers for this environment.</summary>
    ISolutionClient Solutions { get; }

    /// <summary>Creates a row and returns its id.</summary>
    Task<Guid> CreateAsync(Entity entity, CancellationToken cancellationToken = default);

    /// <summary>Creates a row and returns the created row (including server-calculated columns).</summary>
    Task<Entity> CreateAndReturnAsync(Entity entity, CancellationToken cancellationToken = default);

    /// <summary>Retrieves a row by id.</summary>
    /// <param name="tableName">The table logical name.</param>
    /// <param name="id">The row id.</param>
    /// <param name="columns">The columns to return; <see cref="ColumnSet.All"/> when omitted.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <exception cref="Errors.DataverseException">Category <c>NotFound</c> when the row does not exist.</exception>
    Task<Entity> RetrieveAsync(string tableName, Guid id, ColumnSet? columns = null, CancellationToken cancellationToken = default);

    /// <summary>Updates the attributes present on the entity. The entity must have an id.</summary>
    Task UpdateAsync(Entity entity, CancellationToken cancellationToken = default);

    /// <summary>Creates the row when it does not exist, otherwise updates it, keyed by <see cref="Entity.Id"/>.</summary>
    Task<UpsertResult> UpsertAsync(Entity entity, CancellationToken cancellationToken = default);

    /// <summary>Creates or updates a row addressed by an alternate key.</summary>
    /// <param name="entity">The attributes to write.</param>
    /// <param name="alternateKey">Key column logical names and values identifying the row.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task<UpsertResult> UpsertAsync(Entity entity, IReadOnlyDictionary<string, object> alternateKey, CancellationToken cancellationToken = default);

    /// <summary>Deletes a row.</summary>
    Task DeleteAsync(string tableName, Guid id, CancellationToken cancellationToken = default);

    /// <summary>Deletes a row.</summary>
    Task DeleteAsync(EntityReference reference, CancellationToken cancellationToken = default);

    /// <summary>Associates two rows through a relationship.</summary>
    /// <param name="primary">The row owning the relationship's collection navigation.</param>
    /// <param name="relationshipName">The relationship (collection navigation property) name.</param>
    /// <param name="related">The row to associate.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task AssociateAsync(EntityReference primary, string relationshipName, EntityReference related, CancellationToken cancellationToken = default);

    /// <summary>Removes an association between two rows.</summary>
    Task DisassociateAsync(EntityReference primary, string relationshipName, EntityReference related, CancellationToken cancellationToken = default);

    /// <summary>Executes an OData query and returns one page of results.</summary>
    Task<DataverseQueryResult> QueryAsync(ODataQuery query, CancellationToken cancellationToken = default);

    /// <summary>Executes an OData query and streams all rows, following pages automatically.</summary>
    IAsyncEnumerable<Entity> QueryAllAsync(ODataQuery query, CancellationToken cancellationToken = default);

    /// <summary>Executes a FetchXML query and returns one page of results.</summary>
    Task<DataverseQueryResult> FetchAsync(FetchXmlQuery query, CancellationToken cancellationToken = default);

    /// <summary>Executes a FetchXML query and streams all rows, following paging cookies automatically.</summary>
    /// <param name="query">The query. Any explicit page on the query is replaced during enumeration.</param>
    /// <param name="pageSize">Rows fetched per round-trip (1–5000).</param>
    /// <param name="cancellationToken">Cancels the enumeration.</param>
    IAsyncEnumerable<Entity> FetchAllAsync(FetchXmlQuery query, int pageSize = 5000, CancellationToken cancellationToken = default);

    /// <summary>Executes a batch of write operations in a single request.</summary>
    /// <exception cref="Errors.DataverseException">
    /// Thrown when the batch request itself fails. Individual operation failures in a
    /// non-atomic batch are reported per item on the <see cref="BatchResponse"/> instead.
    /// </exception>
    Task<BatchResponse> ExecuteBatchAsync(BatchRequest batch, CancellationToken cancellationToken = default);

    /// <summary>Returns the authenticated caller's identity. Cheap connectivity probe.</summary>
    Task<WhoAmIResponse> WhoAmIAsync(CancellationToken cancellationToken = default);
}
