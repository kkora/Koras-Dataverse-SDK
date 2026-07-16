namespace Koras.Dataverse;

/// <summary>
/// Resolves named Dataverse clients when an application talks to multiple environments.
/// Register environments with <c>AddDataverse(name, …)</c>.
/// </summary>
public interface IDataverseClientFactory
{
    /// <summary>Returns the client registered under the given name.</summary>
    /// <exception cref="InvalidOperationException">No client was registered under that name.</exception>
    IDataverseClient GetClient(string name);
}
