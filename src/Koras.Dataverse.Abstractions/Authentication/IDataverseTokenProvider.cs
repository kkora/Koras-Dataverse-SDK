namespace Koras.Dataverse.Authentication;

/// <summary>
/// Supplies access tokens for a Dataverse environment. The default implementation adapts an
/// <c>Azure.Core.TokenCredential</c>; implement this interface to plug in any other token source.
/// </summary>
/// <remarks>Implementations must be thread-safe and should cache tokens until close to expiry.</remarks>
public interface IDataverseTokenProvider
{
    /// <summary>Returns a valid bearer token for the given environment.</summary>
    /// <param name="environmentUrl">The environment base URL, for example <c>https://contoso.crm.dynamics.com</c>.</param>
    /// <param name="cancellationToken">Cancels the token acquisition.</param>
    ValueTask<string> GetAccessTokenAsync(Uri environmentUrl, CancellationToken cancellationToken = default);
}
