using Microsoft.Extensions.DependencyInjection;

namespace Koras.Dataverse.DependencyInjection;

/// <summary>
/// Fluent continuation of <c>AddDataverse</c> for advanced customization of one named client.
/// </summary>
public sealed class DataverseBuilder
{
    internal DataverseBuilder(IServiceCollection services, string name, IHttpClientBuilder httpClientBuilder)
    {
        Services = services;
        Name = name;
        HttpClientBuilder = httpClientBuilder;
    }

    /// <summary>The underlying service collection.</summary>
    public IServiceCollection Services { get; }

    /// <summary>The client name this builder configures.</summary>
    public string Name { get; }

    /// <summary>
    /// The HTTP client pipeline for this Dataverse client. Add custom
    /// <see cref="DelegatingHandler"/>s here; they run inside the SDK's retry and
    /// authentication handlers.
    /// </summary>
    public IHttpClientBuilder HttpClientBuilder { get; }

    /// <summary>Adds a custom message handler to this client's HTTP pipeline.</summary>
    public DataverseBuilder AddHttpMessageHandler(Func<IServiceProvider, DelegatingHandler> configureHandler)
    {
        ArgumentNullException.ThrowIfNull(configureHandler);
        HttpClientBuilder.AddHttpMessageHandler(configureHandler);
        return this;
    }
}
