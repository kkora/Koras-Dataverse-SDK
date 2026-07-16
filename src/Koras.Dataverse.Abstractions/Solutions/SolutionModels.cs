namespace Koras.Dataverse.Solutions;

/// <summary>Summary information about an installed solution.</summary>
public sealed record SolutionInfo
{
    /// <summary>The solution row id.</summary>
    public Guid SolutionId { get; init; }

    /// <summary>The unique (technical) name.</summary>
    public required string UniqueName { get; init; }

    /// <summary>The display name.</summary>
    public string? FriendlyName { get; init; }

    /// <summary>The solution version, for example <c>1.2.0.0</c>.</summary>
    public string? Version { get; init; }

    /// <summary>Whether the solution is managed.</summary>
    public bool IsManaged { get; init; }

    /// <summary>When the solution was installed or last updated, when known.</summary>
    public DateTimeOffset? InstalledOn { get; init; }
}

/// <summary>Options controlling a solution import.</summary>
public sealed class SolutionImportOptions
{
    /// <summary>Whether unmanaged customizations are overwritten. Default <c>false</c>.</summary>
    public bool OverwriteUnmanagedCustomizations { get; set; }

    /// <summary>Whether processes (workflows) are activated after import. Default <c>true</c>.</summary>
    public bool PublishWorkflows { get; set; } = true;

    /// <summary>Whether an unmanaged solution is converted to managed on import. Default <c>false</c>.</summary>
    public bool ConvertToManaged { get; set; }

    /// <summary>
    /// The import job id. When left <see cref="Guid.Empty"/> the SDK generates one; set it to
    /// correlate the import with monitoring queries on <c>importjob</c>.
    /// </summary>
    public Guid ImportJobId { get; set; }
}
