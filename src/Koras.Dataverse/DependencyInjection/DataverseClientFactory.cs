using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Koras.Dataverse.DependencyInjection;

/// <summary>Marks a registered Dataverse client name. One instance per <c>AddDataverse</c> call.</summary>
internal sealed record DataverseClientRegistration(string Name);

/// <summary>Creates and caches one <see cref="DataverseClient"/> per registered name.</summary>
internal sealed class DataverseClientFactory : IDataverseClientFactory
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<DataverseClientOptions> _options;
    private readonly ILoggerFactory? _loggerFactory;
    private readonly TimeProvider _timeProvider;
    private readonly string[] _registeredNames;
    private readonly ConcurrentDictionary<string, DataverseClient> _clients = new(StringComparer.Ordinal);

    public DataverseClientFactory(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<DataverseClientOptions> options,
        IEnumerable<DataverseClientRegistration> registrations,
        TimeProvider timeProvider,
        ILoggerFactory? loggerFactory = null)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _loggerFactory = loggerFactory;
        _timeProvider = timeProvider;
        _registeredNames = registrations.Select(r => r.Name).Distinct(StringComparer.Ordinal).ToArray();
    }

    public IDataverseClient GetClient(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!_registeredNames.Contains(name, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"No Dataverse client named '{name}' is registered. Register it with services.AddDataverse(\"{name}\", …). " +
                $"Registered names: {string.Join(", ", _registeredNames)}.");
        }

        return _clients.GetOrAdd(name, n =>
        {
            DataverseClientOptions options = _options.Get(n);
            HttpClient httpClient = _httpClientFactory.CreateClient(HttpClientName(n));
            return new DataverseClient(httpClient, options, _loggerFactory, _timeProvider, ownsHttpClient: true);
        });
    }

    /// <summary>Resolves the client backing the unnamed <see cref="IDataverseClient"/> registration.</summary>
    public IDataverseClient GetDefaultClient()
    {
        if (_registeredNames.Contains(DataverseServiceCollectionExtensions.DefaultClientName, StringComparer.Ordinal))
        {
            return GetClient(DataverseServiceCollectionExtensions.DefaultClientName);
        }

        if (_registeredNames.Length == 1)
        {
            return GetClient(_registeredNames[0]);
        }

        throw new InvalidOperationException(
            "Multiple named Dataverse clients are registered; inject IDataverseClientFactory and call GetClient(name) instead of IDataverseClient. " +
            $"Registered names: {string.Join(", ", _registeredNames)}.");
    }

    internal static string HttpClientName(string name) => "Koras.Dataverse:" + name;
}
