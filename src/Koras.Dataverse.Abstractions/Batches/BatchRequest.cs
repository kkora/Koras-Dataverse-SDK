namespace Koras.Dataverse.Batches;

/// <summary>
/// A set of write operations executed in a single Dataverse <c>$batch</c> request.
/// By default all operations run in one atomic change set (all succeed or all roll back).
/// </summary>
/// <remarks>This type is not thread-safe while being populated.</remarks>
public sealed class BatchRequest
{
    /// <summary>The maximum number of operations Dataverse accepts in one batch.</summary>
    public const int MaxOperations = 1000;

    private readonly List<BatchOperation> _operations = new();

    /// <summary>The operations in submission order.</summary>
    public IReadOnlyList<BatchOperation> Operations => _operations;

    /// <summary>
    /// When <c>true</c> (default), all operations execute in a single atomic change set.
    /// When <c>false</c>, operations execute independently and later operations continue after a
    /// failure; inspect per-item results on the response.
    /// </summary>
    public bool Atomic { get; set; } = true;

    /// <summary>Queues a create. The entity's attributes are sent as the new row.</summary>
    public BatchRequest AddCreate(Entity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        Add(new BatchOperation(BatchOperationType.Create, entity, null));
        return this;
    }

    /// <summary>Queues an update of the entity's attributes. The entity must have an id.</summary>
    public BatchRequest AddUpdate(Entity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (entity.Id == Guid.Empty)
        {
            throw new ArgumentException("The entity must have an id to be updated.", nameof(entity));
        }

        Add(new BatchOperation(BatchOperationType.Update, entity, null));
        return this;
    }

    /// <summary>Queues an upsert by id: creates the row when it does not exist.</summary>
    public BatchRequest AddUpsert(Entity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (entity.Id == Guid.Empty)
        {
            throw new ArgumentException("The entity must have an id to be upserted.", nameof(entity));
        }

        Add(new BatchOperation(BatchOperationType.Upsert, entity, null));
        return this;
    }

    /// <summary>Queues a delete.</summary>
    public BatchRequest AddDelete(string tableName, Guid id) => AddDelete(new EntityReference(tableName, id));

    /// <summary>Queues a delete.</summary>
    public BatchRequest AddDelete(EntityReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        Add(new BatchOperation(BatchOperationType.Delete, null, reference));
        return this;
    }

    private void Add(BatchOperation operation)
    {
        if (_operations.Count >= MaxOperations)
        {
            throw new InvalidOperationException($"A batch cannot contain more than {MaxOperations} operations. Split the work into multiple batches.");
        }

        _operations.Add(operation);
    }
}
