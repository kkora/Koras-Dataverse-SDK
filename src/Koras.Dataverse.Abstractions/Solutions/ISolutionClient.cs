namespace Koras.Dataverse.Solutions;

/// <summary>
/// Solution lifecycle helpers: export, import, publish, and lookup. Exposed via
/// <c>IDataverseClient.Solutions</c>.
/// </summary>
/// <remarks>
/// Solution operations are long-running server-side; pass generous timeouts through
/// <c>DataverseClientOptions.Timeout</c> or per-call cancellation tokens.
/// Implementations are thread-safe.
/// </remarks>
public interface ISolutionClient
{
    /// <summary>Exports a solution and returns the solution zip contents.</summary>
    /// <param name="solutionUniqueName">The solution's unique name.</param>
    /// <param name="managed">Whether to export as managed.</param>
    /// <param name="cancellationToken">Cancels the export.</param>
    Task<byte[]> ExportAsync(string solutionUniqueName, bool managed = false, CancellationToken cancellationToken = default);

    /// <summary>Imports a solution zip.</summary>
    Task ImportAsync(ReadOnlyMemory<byte> solutionZip, SolutionImportOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>Publishes all customizations.</summary>
    Task PublishAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns information about an installed solution, or <c>null</c> when not installed.</summary>
    Task<SolutionInfo?> FindAsync(string uniqueName, CancellationToken cancellationToken = default);
}
