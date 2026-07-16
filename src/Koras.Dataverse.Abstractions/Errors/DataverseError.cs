namespace Koras.Dataverse.Errors;

/// <summary>
/// A normalized description of a Dataverse failure, independent of transport details.
/// </summary>
public sealed record DataverseError
{
    /// <summary>The failure classification. Stable across SDK versions.</summary>
    public required DataverseErrorCategory Category { get; init; }

    /// <summary>A human-readable message. Never contains credentials or tokens.</summary>
    public required string Message { get; init; }

    /// <summary>The HTTP status code, when an HTTP response was received.</summary>
    public int? HttpStatusCode { get; init; }

    /// <summary>The Dataverse error code (hexadecimal string such as <c>0x80040217</c>), when provided.</summary>
    public string? ErrorCode { get; init; }

    /// <summary>The service request id (<c>x-ms-service-request-id</c>), useful for Microsoft support.</summary>
    public string? RequestId { get; init; }

    /// <summary>The server-suggested delay before retrying, when the response carried <c>Retry-After</c>.</summary>
    public TimeSpan? RetryAfter { get; init; }

    /// <summary>Whether retrying the identical request may succeed.</summary>
    public bool IsTransient { get; init; }

    /// <summary>Returns a single-line diagnostic summary.</summary>
    public override string ToString() =>
        $"{Category} (HTTP {HttpStatusCode?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "-"}, code {ErrorCode ?? "-"}): {Message}";
}
