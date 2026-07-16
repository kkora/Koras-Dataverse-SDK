namespace Koras.Dataverse.Errors;

/// <summary>
/// Coarse-grained classification of Dataverse failures. Categories are part of the SDK's public
/// contract: code can branch on them without parsing provider-specific error codes.
/// </summary>
public enum DataverseErrorCategory
{
    /// <summary>The failure could not be classified.</summary>
    Unknown = 0,

    /// <summary>Acquiring or using an access token failed (HTTP 401).</summary>
    Authentication,

    /// <summary>The caller is authenticated but lacks privileges (HTTP 403).</summary>
    Authorization,

    /// <summary>The addressed row, table, or resource does not exist (HTTP 404).</summary>
    NotFound,

    /// <summary>An optimistic-concurrency or duplicate conflict occurred (HTTP 409/412).</summary>
    Concurrency,

    /// <summary>Dataverse service-protection limits were hit (HTTP 429). Transient.</summary>
    Throttling,

    /// <summary>The request was rejected as invalid (HTTP 400 and business-rule errors).</summary>
    Validation,

    /// <summary>The operation exceeded its time budget (HTTP 408 or client timeout).</summary>
    Timeout,

    /// <summary>The request never produced an HTTP response (DNS, TLS, socket failures). Transient.</summary>
    Network,

    /// <summary>Dataverse reported a server-side failure (HTTP 5xx). Usually transient.</summary>
    Server,
}
