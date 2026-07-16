using Koras.Dataverse;
using Koras.Dataverse.Authentication;
using Koras.Dataverse.DependencyInjection;
using Koras.Dataverse.HealthChecks;
using Koras.Dataverse.Http;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Dependency-injection registration for the Koras Dataverse SDK.</summary>
public static class DataverseServiceCollectionExtensions
{
    /// <summary>The name used when a client is registered without an explicit name.</summary>
    public const string DefaultClientName = "Default";

    /// <summary>
    /// Registers the default <see cref="IDataverseClient"/> for one Dataverse environment.
    /// The client is a thread-safe singleton; options are validated at startup.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configures <see cref="DataverseClientOptions"/> (environment URL, authentication, retry).</param>
    public static DataverseBuilder AddDataverse(this IServiceCollection services, Action<DataverseClientOptions> configure) =>
        services.AddDataverse(DefaultClientName, configure);

    /// <summary>
    /// Registers a named <see cref="IDataverseClient"/>; resolve it through
    /// <see cref="IDataverseClientFactory"/>. Use one name per environment.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="name">The client name, for example <c>"crm-prod"</c>.</param>
    /// <param name="configure">Configures <see cref="DataverseClientOptions"/> for this client.</param>
    public static DataverseBuilder AddDataverse(this IServiceCollection services, string name, Action<DataverseClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddOptions<DataverseClientOptions>(name).Configure(configure).ValidateOnStart();
        services.TryAddSingleton<IValidateOptions<DataverseClientOptions>, DataverseClientOptionsValidator>();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IDataverseClientFactory, DataverseClientFactory>();
        services.AddSingleton(new DataverseClientRegistration(name));

        services.TryAddSingleton<IDataverseClient>(provider =>
        {
            var factory = (DataverseClientFactory)provider.GetRequiredService<IDataverseClientFactory>();
            return factory.GetDefaultClient();
        });

        // Per-name token provider so the token cache survives handler rotation.
        services.TryAddKeyedSingleton<IDataverseTokenProvider>(name, (provider, _) =>
        {
            DataverseClientOptions options = provider.GetRequiredService<IOptionsMonitor<DataverseClientOptions>>().Get(name);
            return options.Authentication.TokenProvider
                ?? new TokenCredentialTokenProvider(options.Authentication.BuildCredential(), provider.GetService<TimeProvider>());
        });

        IHttpClientBuilder httpBuilder = services
            .AddHttpClient(DataverseClientFactory.HttpClientName(name))
            .ConfigureHttpClient((provider, client) =>
            {
                DataverseClientOptions options = provider.GetRequiredService<IOptionsMonitor<DataverseClientOptions>>().Get(name);
                options.Validate();
                client.BaseAddress = new Uri(options.EnvironmentUrl!, $"/api/data/{options.ApiVersion}/");
                client.Timeout = System.Threading.Timeout.InfiniteTimeSpan; // the client enforces the operation timeout
            })
            .AddHttpMessageHandler(provider =>
            {
                DataverseClientOptions options = provider.GetRequiredService<IOptionsMonitor<DataverseClientOptions>>().Get(name);
                return new RetryHandler(
                    options.Retry,
                    provider.GetService<TimeProvider>(),
                    provider.GetService<ILoggerFactory>()?.CreateLogger("Koras.Dataverse.Http"));
            })
            .AddHttpMessageHandler(provider =>
            {
                DataverseClientOptions options = provider.GetRequiredService<IOptionsMonitor<DataverseClientOptions>>().Get(name);
                return new AuthenticationHandler(
                    provider.GetRequiredKeyedService<IDataverseTokenProvider>(name),
                    options.EnvironmentUrl!);
            });

        return new DataverseBuilder(services, name, httpBuilder);
    }

    /// <summary>
    /// Registers the <see cref="DataverseHealthCheck"/> (a <c>WhoAmI</c> probe against the default client).
    /// </summary>
    /// <param name="builder">The health checks builder from <c>AddHealthChecks()</c>.</param>
    /// <param name="name">The health check name. Default <c>"dataverse"</c>.</param>
    /// <param name="failureStatus">Status reported on failure. Default <see cref="HealthStatus.Unhealthy"/>.</param>
    /// <param name="tags">Optional tags for filtering probes.</param>
    public static IHealthChecksBuilder AddDataverseHealthCheck(
        this IHealthChecksBuilder builder,
        string name = "dataverse",
        HealthStatus failureStatus = HealthStatus.Unhealthy,
        IEnumerable<string>? tags = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.TryAddSingleton<DataverseHealthCheck>();
        builder.Add(new HealthCheckRegistration(
            name,
            provider => provider.GetRequiredService<DataverseHealthCheck>(),
            failureStatus,
            tags));
        return builder;
    }
}
