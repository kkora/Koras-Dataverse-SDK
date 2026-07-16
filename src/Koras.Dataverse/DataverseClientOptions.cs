namespace Koras.Dataverse;

/// <summary>Configuration for a Dataverse client. Configure via <c>AddDataverse</c> or pass to <c>DataverseClient.Create</c>.</summary>
public sealed class DataverseClientOptions
{
    /// <summary>
    /// The environment base URL, for example <c>https://contoso.crm.dynamics.com</c>.
    /// Required; must use HTTPS.
    /// </summary>
    public Uri? EnvironmentUrl { get; set; }

    /// <summary>The Web API version segment. Default <c>v9.2</c>.</summary>
    public string ApiVersion { get; set; } = "v9.2";

    /// <summary>
    /// The per-operation time budget covering all retry attempts. Default 100 seconds.
    /// Raise it for solution import/export. Expiry surfaces as a
    /// <see cref="Errors.DataverseException"/> with category <c>Timeout</c>.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(100);

    /// <summary>
    /// Whether responses include display annotations (formatted values, lookup names).
    /// Default <c>true</c>; disable to shrink payloads on hot paths.
    /// </summary>
    public bool IncludeAnnotations { get; set; } = true;

    /// <summary>Authentication configuration. Call one of the <c>Use…</c> methods.</summary>
    public DataverseAuthenticationOptions Authentication { get; } = new();

    /// <summary>Retry and throttling behavior.</summary>
    public DataverseRetryOptions Retry { get; } = new();

    /// <summary>
    /// Explicit entity-set-name overrides for tables whose set name does not follow Dataverse's
    /// standard pluralization, keyed by table logical name (for example
    /// <c>["new_metadata"] = "new_metadataset"</c>).
    /// </summary>
    public IDictionary<string, string> EntitySetNameOverrides { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

    internal void Validate()
    {
        if (EnvironmentUrl is null)
        {
            throw new InvalidOperationException("DataverseClientOptions.EnvironmentUrl is required (for example https://contoso.crm.dynamics.com).");
        }

        if (!EnvironmentUrl.IsAbsoluteUri || EnvironmentUrl.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException($"DataverseClientOptions.EnvironmentUrl must be an absolute HTTPS URL; '{EnvironmentUrl}' is not.");
        }

        if (string.IsNullOrWhiteSpace(ApiVersion) || !ApiVersion.StartsWith('v'))
        {
            throw new InvalidOperationException($"DataverseClientOptions.ApiVersion must look like 'v9.2'; '{ApiVersion}' is not.");
        }

        if (Timeout <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("DataverseClientOptions.Timeout must be positive.");
        }

        Retry.Validate();
        Authentication.Validate();
    }
}

/// <summary>Retry and throttling behavior for transient Dataverse failures.</summary>
public sealed class DataverseRetryOptions
{
    /// <summary>Maximum retry attempts after the initial try. Default 3. Set 0 to disable retries.</summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>Base delay for exponential backoff. Default 1 second.</summary>
    public TimeSpan BaseDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>Upper bound for a single backoff delay. Default 30 seconds. A longer server-provided <c>Retry-After</c> still wins.</summary>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Whether the server's <c>Retry-After</c> hint is honored. Default <c>true</c>.</summary>
    public bool RespectRetryAfter { get; set; } = true;

    internal void Validate()
    {
        if (MaxRetries < 0 || MaxRetries > 10)
        {
            throw new InvalidOperationException("DataverseRetryOptions.MaxRetries must be between 0 and 10.");
        }

        if (BaseDelay <= TimeSpan.Zero || MaxDelay < BaseDelay)
        {
            throw new InvalidOperationException("DataverseRetryOptions delays must be positive and MaxDelay must be at least BaseDelay.");
        }
    }
}
