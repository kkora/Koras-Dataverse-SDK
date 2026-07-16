using System.Net.Http.Headers;
using Koras.Dataverse.Authentication;

namespace Koras.Dataverse.Http;

/// <summary>Attaches a bearer token from an <see cref="IDataverseTokenProvider"/> to every request.</summary>
internal sealed class AuthenticationHandler : DelegatingHandler
{
    private readonly IDataverseTokenProvider _tokenProvider;
    private readonly Uri _environmentUrl;

    public AuthenticationHandler(IDataverseTokenProvider tokenProvider, Uri environmentUrl)
    {
        _tokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
        _environmentUrl = environmentUrl ?? throw new ArgumentNullException(nameof(environmentUrl));
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string token = await _tokenProvider.GetAccessTokenAsync(_environmentUrl, cancellationToken).ConfigureAwait(false);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
