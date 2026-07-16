namespace Koras.Dataverse.Errors;

/// <summary>
/// The exception thrown for all Dataverse operation failures. Inspect <see cref="Error"/> for the
/// normalized category, Dataverse error code, HTTP status, and retry hints.
/// </summary>
/// <remarks>
/// Cancellation is never reported through this type: cooperative cancellation surfaces as
/// <see cref="OperationCanceledException"/>.
/// </remarks>
public class DataverseException : Exception
{
    /// <summary>Creates the exception from a normalized error.</summary>
    public DataverseException(DataverseError error)
        : base((error ?? throw new ArgumentNullException(nameof(error))).Message)
    {
        Error = error;
    }

    /// <summary>Creates the exception from a normalized error and an inner cause.</summary>
    public DataverseException(DataverseError error, Exception? innerException)
        : base((error ?? throw new ArgumentNullException(nameof(error))).Message, innerException)
    {
        Error = error;
    }

    /// <summary>The normalized error details.</summary>
    public DataverseError Error { get; }

    /// <summary>The failure classification (shortcut for <c>Error.Category</c>).</summary>
    public DataverseErrorCategory Category => Error.Category;

    /// <summary>Whether retrying the identical operation may succeed (shortcut for <c>Error.IsTransient</c>).</summary>
    public bool IsTransient => Error.IsTransient;
}
