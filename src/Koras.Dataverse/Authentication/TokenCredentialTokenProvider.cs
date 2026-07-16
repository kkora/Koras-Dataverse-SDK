using Azure.Core;
using Koras.Dataverse.Errors;

namespace Koras.Dataverse.Authentication;

/// <summary>
/// The default <see cref="IDataverseTokenProvider"/>: adapts an <c>Azure.Core.TokenCredential</c>
/// to Dataverse's <c>{environment}/.default</c> scope, caching the token and refreshing it
/// single-flight five minutes before expiry.
/// </summary>
/// <remarks>Thread-safe. Reuse one instance per environment so the cache is effective.</remarks>
public sealed class TokenCredentialTokenProvider : IDataverseTokenProvider, IDisposable
{
    private static readonly TimeSpan RefreshWindow = TimeSpan.FromMinutes(5);

    private readonly TokenCredential _credential;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private AccessToken _token;
    private bool _hasToken;

    /// <summary>Creates the provider.</summary>
    /// <param name="credential">The credential used to request tokens.</param>
    /// <param name="timeProvider">Clock used for expiry checks; defaults to the system clock.</param>
    public TokenCredentialTokenProvider(TokenCredential credential, TimeProvider? timeProvider = null)
    {
        _credential = credential ?? throw new ArgumentNullException(nameof(credential));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async ValueTask<string> GetAccessTokenAsync(Uri environmentUrl, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(environmentUrl);

        if (_hasToken && !NeedsRefresh())
        {
            return _token.Token;
        }

        await _refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_hasToken && !NeedsRefresh())
            {
                return _token.Token;
            }

            string scope = environmentUrl.GetLeftPart(UriPartial.Authority) + "/.default";
            try
            {
                _token = await _credential
                    .GetTokenAsync(new TokenRequestContext(new[] { scope }), cancellationToken)
                    .ConfigureAwait(false);
                _hasToken = true;
                return _token.Token;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new DataverseException(
                    new DataverseError
                    {
                        Category = DataverseErrorCategory.Authentication,
                        Message = $"Failed to acquire a Dataverse access token for scope '{scope}'. {exception.Message}",
                        IsTransient = false,
                    },
                    exception);
            }
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose() => _refreshLock.Dispose();

    private bool NeedsRefresh() => _timeProvider.GetUtcNow() >= _token.ExpiresOn - RefreshWindow;
}
