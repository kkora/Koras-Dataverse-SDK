using Azure.Core;
using Koras.Dataverse.Authentication;
using Koras.Dataverse.Errors;
using Koras.Dataverse.UnitTests.TestInfrastructure;

namespace Koras.Dataverse.UnitTests.Authentication;

public class TokenCredentialTokenProviderTests
{
    private static readonly Uri Environment = new("https://unittest.crm.dynamics.com");

    private sealed class CountingCredential : TokenCredential
    {
        private readonly Func<DateTimeOffset> _expiry;
        public int Calls;
        public string? LastScope;

        public CountingCredential(Func<DateTimeOffset> expiry) => _expiry = expiry;

        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            GetTokenAsync(requestContext, cancellationToken).AsTask().GetAwaiter().GetResult();

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            Calls++;
            LastScope = requestContext.Scopes.Single();
            return ValueTask.FromResult(new AccessToken($"token-{Calls}", _expiry()));
        }
    }

    [Fact]
    public async Task Caches_token_until_refresh_window()
    {
        var time = new FakeTimeProvider();
        var credential = new CountingCredential(() => time.GetUtcNow().AddHours(1));
        using var provider = new TokenCredentialTokenProvider(credential, time);

        string first = await provider.GetAccessTokenAsync(Environment);
        string second = await provider.GetAccessTokenAsync(Environment);

        Assert.Equal("token-1", first);
        Assert.Equal("token-1", second);
        Assert.Equal(1, credential.Calls);
        Assert.Equal("https://unittest.crm.dynamics.com/.default", credential.LastScope);
    }

    [Fact]
    public async Task Refreshes_within_five_minutes_of_expiry()
    {
        var time = new FakeTimeProvider();
        var credential = new CountingCredential(() => time.GetUtcNow().AddHours(1));
        using var provider = new TokenCredentialTokenProvider(credential, time);

        await provider.GetAccessTokenAsync(Environment);
        time.Advance(TimeSpan.FromMinutes(56)); // inside the 5-minute refresh window
        string refreshed = await provider.GetAccessTokenAsync(Environment);

        Assert.Equal("token-2", refreshed);
        Assert.Equal(2, credential.Calls);
    }

    [Fact]
    public async Task Credential_failures_surface_as_authentication_errors()
    {
        var failing = new FailingCredential();
        using var provider = new TokenCredentialTokenProvider(failing, new FakeTimeProvider());

        DataverseException exception = await Assert.ThrowsAsync<DataverseException>(
            async () => await provider.GetAccessTokenAsync(Environment));

        Assert.Equal(DataverseErrorCategory.Authentication, exception.Category);
        Assert.False(exception.IsTransient);
        Assert.NotNull(exception.InnerException);
    }

    private sealed class FailingCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("AADSTS700016: app not found");

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("AADSTS700016: app not found");
    }
}
