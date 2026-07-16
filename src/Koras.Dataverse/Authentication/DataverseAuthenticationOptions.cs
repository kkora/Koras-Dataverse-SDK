using System.Security.Cryptography.X509Certificates;
using Azure.Core;
using Azure.Identity;
using Koras.Dataverse.Authentication;

namespace Koras.Dataverse;

/// <summary>How the client authenticates to Dataverse.</summary>
public enum DataverseAuthenticationKind
{
    /// <summary>Chained credential discovery (<c>DefaultAzureCredential</c>): environment variables, workload identity, managed identity, Azure CLI, and more. Good default for both local development and Azure hosting.</summary>
    Default = 0,

    /// <summary>Entra ID application with a client secret.</summary>
    ClientSecret,

    /// <summary>Entra ID application with a certificate. Preferred over secrets.</summary>
    Certificate,

    /// <summary>Azure managed identity (system- or user-assigned). Preferred in Azure hosting.</summary>
    ManagedIdentity,

    /// <summary>Interactive browser sign-in. Development and admin tooling only.</summary>
    Interactive,

    /// <summary>A caller-supplied <c>Azure.Core.TokenCredential</c>.</summary>
    TokenCredential,

    /// <summary>A caller-supplied <see cref="IDataverseTokenProvider"/>.</summary>
    TokenProvider,
}

/// <summary>
/// Authentication settings for a Dataverse client. Use one of the <c>Use…</c> methods; the last
/// call wins. Secrets should come from a secret store (user-secrets, Key Vault, environment),
/// never from committed configuration.
/// </summary>
public sealed class DataverseAuthenticationOptions
{
    /// <summary>The selected authentication mechanism. Default <see cref="DataverseAuthenticationKind.Default"/>.</summary>
    public DataverseAuthenticationKind Kind { get; private set; } = DataverseAuthenticationKind.Default;

    /// <summary>The Entra ID tenant id, where applicable.</summary>
    public string? TenantId { get; private set; }

    /// <summary>The Entra ID application (client) id, where applicable.</summary>
    public string? ClientId { get; private set; }

    /// <summary>The client secret for <see cref="DataverseAuthenticationKind.ClientSecret"/>.</summary>
    public string? ClientSecret { get; private set; }

    /// <summary>The certificate for <see cref="DataverseAuthenticationKind.Certificate"/>.</summary>
    public X509Certificate2? Certificate { get; private set; }

    /// <summary>The user-assigned managed identity client id; null uses the system-assigned identity.</summary>
    public string? ManagedIdentityClientId { get; private set; }

    /// <summary>The caller-supplied credential for <see cref="DataverseAuthenticationKind.TokenCredential"/>.</summary>
    public TokenCredential? Credential { get; private set; }

    /// <summary>The caller-supplied token provider for <see cref="DataverseAuthenticationKind.TokenProvider"/>.</summary>
    public IDataverseTokenProvider? TokenProvider { get; private set; }

    /// <summary>Authenticates with <c>DefaultAzureCredential</c> (environment, workload identity, managed identity, developer tools).</summary>
    public DataverseAuthenticationOptions UseDefault()
    {
        Reset(DataverseAuthenticationKind.Default);
        return this;
    }

    /// <summary>Authenticates as an Entra ID application with a client secret.</summary>
    public DataverseAuthenticationOptions UseClientSecret(string tenantId, string clientId, string clientSecret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientSecret);
        Reset(DataverseAuthenticationKind.ClientSecret);
        TenantId = tenantId;
        ClientId = clientId;
        ClientSecret = clientSecret;
        return this;
    }

    /// <summary>Authenticates as an Entra ID application with a certificate.</summary>
    public DataverseAuthenticationOptions UseCertificate(string tenantId, string clientId, X509Certificate2 certificate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentNullException.ThrowIfNull(certificate);
        Reset(DataverseAuthenticationKind.Certificate);
        TenantId = tenantId;
        ClientId = clientId;
        Certificate = certificate;
        return this;
    }

    /// <summary>Authenticates with an Azure managed identity.</summary>
    /// <param name="clientId">The user-assigned identity's client id, or null for the system-assigned identity.</param>
    public DataverseAuthenticationOptions UseManagedIdentity(string? clientId = null)
    {
        Reset(DataverseAuthenticationKind.ManagedIdentity);
        ManagedIdentityClientId = clientId;
        return this;
    }

    /// <summary>Authenticates interactively through the system browser. Development and admin tooling only.</summary>
    public DataverseAuthenticationOptions UseInteractive(string? tenantId = null, string? clientId = null)
    {
        Reset(DataverseAuthenticationKind.Interactive);
        TenantId = tenantId;
        ClientId = clientId;
        return this;
    }

    /// <summary>Authenticates with any <c>Azure.Core.TokenCredential</c>.</summary>
    public DataverseAuthenticationOptions UseTokenCredential(TokenCredential credential)
    {
        ArgumentNullException.ThrowIfNull(credential);
        Reset(DataverseAuthenticationKind.TokenCredential);
        Credential = credential;
        return this;
    }

    /// <summary>Supplies tokens through a custom <see cref="IDataverseTokenProvider"/> (no Azure.Identity involvement).</summary>
    public DataverseAuthenticationOptions UseTokenProvider(IDataverseTokenProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        Reset(DataverseAuthenticationKind.TokenProvider);
        TokenProvider = provider;
        return this;
    }

    internal void Validate()
    {
        switch (Kind)
        {
            case DataverseAuthenticationKind.ClientSecret when string.IsNullOrWhiteSpace(TenantId) || string.IsNullOrWhiteSpace(ClientId) || string.IsNullOrWhiteSpace(ClientSecret):
                throw new InvalidOperationException("Client-secret authentication requires TenantId, ClientId, and ClientSecret.");
            case DataverseAuthenticationKind.Certificate when Certificate is null || string.IsNullOrWhiteSpace(TenantId) || string.IsNullOrWhiteSpace(ClientId):
                throw new InvalidOperationException("Certificate authentication requires TenantId, ClientId, and a certificate.");
            case DataverseAuthenticationKind.TokenCredential when Credential is null:
                throw new InvalidOperationException("UseTokenCredential requires a credential instance.");
            case DataverseAuthenticationKind.TokenProvider when TokenProvider is null:
                throw new InvalidOperationException("UseTokenProvider requires a provider instance.");
            default:
                break;
        }
    }

    internal TokenCredential BuildCredential()
    {
        return Kind switch
        {
            DataverseAuthenticationKind.Default => new DefaultAzureCredential(),
            DataverseAuthenticationKind.ClientSecret => new ClientSecretCredential(TenantId, ClientId, ClientSecret),
            DataverseAuthenticationKind.Certificate => new ClientCertificateCredential(TenantId, ClientId, Certificate),
            DataverseAuthenticationKind.ManagedIdentity when ManagedIdentityClientId is null =>
                new ManagedIdentityCredential(ManagedIdentityId.SystemAssigned),
            DataverseAuthenticationKind.ManagedIdentity =>
                new ManagedIdentityCredential(ManagedIdentityId.FromUserAssignedClientId(ManagedIdentityClientId)),
            DataverseAuthenticationKind.Interactive => new InteractiveBrowserCredential(new InteractiveBrowserCredentialOptions
            {
                TenantId = TenantId,
                ClientId = ClientId,
            }),
            DataverseAuthenticationKind.TokenCredential => Credential!,
            _ => throw new InvalidOperationException($"Authentication kind '{Kind}' does not build a TokenCredential."),
        };
    }

    private void Reset(DataverseAuthenticationKind kind)
    {
        Kind = kind;
        TenantId = null;
        ClientId = null;
        ClientSecret = null;
        Certificate = null;
        ManagedIdentityClientId = null;
        Credential = null;
        TokenProvider = null;
    }
}
