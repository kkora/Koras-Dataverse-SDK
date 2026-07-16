using Koras.Dataverse.Errors;

namespace Koras.Dataverse.Batches;

/// <summary>The outcome of one operation inside a batch.</summary>
public sealed class BatchItemResult
{
    /// <summary>Creates an item result.</summary>
    public BatchItemResult(int index, int statusCode, Guid? createdId, DataverseError? error)
    {
        Index = index;
        StatusCode = statusCode;
        CreatedId = createdId;
        Error = error;
    }

    /// <summary>The zero-based position of the operation in the request.</summary>
    public int Index { get; }

    /// <summary>The HTTP status code of the individual operation.</summary>
    public int StatusCode { get; }

    /// <summary>Whether the operation succeeded.</summary>
    public bool Succeeded => Error is null;

    /// <summary>The id of the created row, for create operations.</summary>
    public Guid? CreatedId { get; }

    /// <summary>The normalized error, when the operation failed.</summary>
    public DataverseError? Error { get; }
}

/// <summary>The outcome of a <c>$batch</c> execution.</summary>
public sealed class BatchResponse
{
    /// <summary>Creates a batch response.</summary>
    public BatchResponse(IReadOnlyList<BatchItemResult> results)
    {
        Results = results ?? throw new ArgumentNullException(nameof(results));
    }

    /// <summary>Per-operation results, in request order.</summary>
    public IReadOnlyList<BatchItemResult> Results { get; }

    /// <summary>Whether every operation succeeded.</summary>
    public bool Succeeded => Results.All(r => r.Succeeded);
}
