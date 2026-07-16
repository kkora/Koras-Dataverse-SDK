namespace Koras.Dataverse.Batches;

/// <summary>The kind of a batched operation.</summary>
public enum BatchOperationType
{
    /// <summary>Create a row.</summary>
    Create,

    /// <summary>Update a row's attributes.</summary>
    Update,

    /// <summary>Create the row if it does not exist, otherwise update it.</summary>
    Upsert,

    /// <summary>Delete a row.</summary>
    Delete,
}

/// <summary>One operation inside a <see cref="BatchRequest"/>.</summary>
public sealed class BatchOperation
{
    internal BatchOperation(BatchOperationType type, Entity? entity, EntityReference? reference)
    {
        Type = type;
        Entity = entity;
        Reference = reference;
    }

    /// <summary>The operation kind.</summary>
    public BatchOperationType Type { get; }

    /// <summary>The entity payload for create/update/upsert operations.</summary>
    public Entity? Entity { get; }

    /// <summary>The target reference for delete operations.</summary>
    public EntityReference? Reference { get; }
}
