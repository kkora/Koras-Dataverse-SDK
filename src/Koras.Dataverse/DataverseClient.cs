using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Koras.Dataverse.Authentication;
using Koras.Dataverse.Diagnostics;
using Koras.Dataverse.Errors;
using Koras.Dataverse.Http;
using Koras.Dataverse.Metadata;
using Koras.Dataverse.Serialization;
using Koras.Dataverse.Solutions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Koras.Dataverse;

/// <summary>
/// The default <see cref="IDataverseClient"/> implementation over the Dataverse Web API.
/// Register it with <c>services.AddDataverse(…)</c>, or use <see cref="Create"/> outside
/// dependency injection.
/// </summary>
/// <remarks>Thread-safe; intended to live as a singleton per environment.</remarks>
public sealed partial class DataverseClient : IDataverseClient, IDisposable
{
    private readonly HttpClient _http;
    private readonly DataverseClientOptions _options;
    private readonly ILogger _logger;
    private readonly TimeProvider _timeProvider;
    private readonly EntitySetNameResolver _entitySets;
    private readonly EntityJsonSerializer _serializer;
    private readonly bool _ownsHttpClient;

    /// <summary>
    /// Creates a client over a caller-supplied <see cref="HttpClient"/>. The HttpClient's pipeline
    /// must already authenticate requests (as configured by <c>AddDataverse</c>); most applications
    /// should use dependency injection or <see cref="Create"/> instead of this constructor.
    /// </summary>
    public DataverseClient(HttpClient httpClient, DataverseClientOptions options, ILoggerFactory? loggerFactory = null, TimeProvider? timeProvider = null)
        : this(httpClient, options, loggerFactory, timeProvider, ownsHttpClient: false)
    {
    }

    internal DataverseClient(HttpClient httpClient, DataverseClientOptions options, ILoggerFactory? loggerFactory, TimeProvider? timeProvider, bool ownsHttpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        _http = httpClient;
        _options = options;
        _logger = loggerFactory?.CreateLogger("Koras.Dataverse") ?? NullLogger.Instance;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _entitySets = new EntitySetNameResolver(options.EntitySetNameOverrides);
        _serializer = new EntityJsonSerializer(_entitySets);
        _ownsHttpClient = ownsHttpClient;

        _http.BaseAddress ??= ApiBaseAddress(options);
        _http.Timeout = System.Threading.Timeout.InfiniteTimeSpan;
        _http.DefaultRequestHeaders.Remove("OData-MaxVersion");
        _http.DefaultRequestHeaders.Remove("OData-Version");
        _http.DefaultRequestHeaders.Add("OData-MaxVersion", "4.0");
        _http.DefaultRequestHeaders.Add("OData-Version", "4.0");

        Metadata = new MetadataClient(this);
        Solutions = new SolutionClient(this);
    }

    /// <inheritdoc />
    public IMetadataClient Metadata { get; }

    /// <inheritdoc />
    public ISolutionClient Solutions { get; }

    /// <summary>
    /// Creates a self-contained client outside dependency injection (console tools, scripts).
    /// The returned client owns its HTTP resources; dispose it when done.
    /// </summary>
    public static DataverseClient Create(DataverseClientOptions options, ILoggerFactory? loggerFactory = null, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        IDataverseTokenProvider tokenProvider = options.Authentication.TokenProvider
            ?? new TokenCredentialTokenProvider(options.Authentication.BuildCredential(), timeProvider);

        var authentication = new AuthenticationHandler(tokenProvider, options.EnvironmentUrl!)
        {
            InnerHandler = new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(15) },
        };
        var retry = new RetryHandler(options.Retry, timeProvider, loggerFactory?.CreateLogger("Koras.Dataverse.Http"))
        {
            InnerHandler = authentication,
        };

        var httpClient = new HttpClient(retry, disposeHandler: true) { BaseAddress = ApiBaseAddress(options) };
        return new DataverseClient(httpClient, options, loggerFactory, timeProvider, ownsHttpClient: true);
    }

    /// <summary>Releases the underlying HTTP resources when this client owns them.</summary>
    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _http.Dispose();
        }
    }

    /// <inheritdoc />
    public async Task<WhoAmIResponse> WhoAmIAsync(CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = CreateRequest(HttpMethod.Get, "WhoAmI");
        using HttpResponseMessage response = await SendAsync(request, "whoami", null, cancellationToken).ConfigureAwait(false);
        using JsonDocument document = await ReadJsonAsync(response, cancellationToken).ConfigureAwait(false);
        JsonElement root = document.RootElement;
        return new WhoAmIResponse(
            root.GetProperty("UserId").GetGuid(),
            root.GetProperty("BusinessUnitId").GetGuid(),
            root.GetProperty("OrganizationId").GetGuid());
    }

    private static Uri ApiBaseAddress(DataverseClientOptions options) =>
        new(options.EnvironmentUrl!, $"/api/data/{options.ApiVersion}/");

    internal static HttpRequestMessage CreateRequest(HttpMethod method, string relativeUrl, string? jsonPayload = null, params string[] prefer)
    {
        var request = new HttpRequestMessage(method, new Uri(relativeUrl, UriKind.Relative));
        if (jsonPayload is not null)
        {
            request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
        }

        if (prefer.Length > 0)
        {
            request.Headers.TryAddWithoutValidation("Prefer", string.Join(",", prefer));
        }

        return request;
    }

    internal string AnnotationsPreference() => _options.IncludeAnnotations
        ? "odata.include-annotations=\"*\""
        : "odata.include-annotations=\"OData.Community.Display.V1.FormattedValue\"";

    internal string EntitySet(string tableName) => _entitySets.Resolve(tableName);

    internal Uri AbsoluteUrl(string relative) => new(_http.BaseAddress!, relative);

    internal EntityJsonSerializer Serializer => _serializer;

    internal ILogger Logger => _logger;

    /// <summary>
    /// Sends a request with per-operation timeout, telemetry, and error normalization.
    /// Every Web API call in the SDK funnels through this method.
    /// </summary>
    internal async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, string operation, string? tableName, CancellationToken cancellationToken)
    {
        using Activity? activity = DataverseTelemetry.ActivitySource.StartActivity($"dataverse.{operation}", ActivityKind.Client);
        if (activity is not null)
        {
            activity.SetTag("dataverse.operation", operation);
            if (tableName is not null)
            {
                activity.SetTag("dataverse.table", tableName);
            }
        }

        long started = _timeProvider.GetTimestamp();
        string outcome = "success";
        try
        {
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(_options.Timeout);

            HttpResponseMessage response;
            try
            {
                response = await _http.SendAsync(request, HttpCompletionOption.ResponseContentRead, timeoutSource.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                outcome = "canceled";
                activity?.SetStatus(ActivityStatusCode.Error, "canceled");
                throw;
            }
            catch (OperationCanceledException exception)
            {
                outcome = "timeout";
                activity?.SetStatus(ActivityStatusCode.Error, "timeout");
                throw new DataverseException(
                    new DataverseError
                    {
                        Category = DataverseErrorCategory.Timeout,
                        Message = $"The Dataverse '{operation}' operation did not complete within {_options.Timeout} (including retries).",
                        IsTransient = true,
                    },
                    exception);
            }
            catch (HttpRequestException exception)
            {
                outcome = "network";
                activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
                throw new DataverseException(
                    new DataverseError
                    {
                        Category = DataverseErrorCategory.Network,
                        Message = $"The Dataverse '{operation}' operation failed before an HTTP response was received: {exception.Message}",
                        IsTransient = true,
                    },
                    exception);
            }

            activity?.SetTag("http.response.status_code", (int)response.StatusCode);

            if (!response.IsSuccessStatusCode)
            {
                DataverseError error = await DataverseErrorParser.FromResponseAsync(response, cancellationToken).ConfigureAwait(false);
                response.Dispose();
                outcome = "error";
                activity?.SetStatus(ActivityStatusCode.Error, error.Message);
                activity?.SetTag("dataverse.error.category", error.Category.ToString());
                if (error.RequestId is not null)
                {
                    activity?.SetTag("dataverse.request_id", error.RequestId);
                }

                _logger.LogError(
                    "Dataverse {Operation} on '{Table}' failed: {Category} (HTTP {Status}, code {Code}, request {RequestId}).",
                    operation, tableName ?? "-", error.Category, error.HttpStatusCode, error.ErrorCode ?? "-", error.RequestId ?? "-");
                throw new DataverseException(error);
            }

            return response;
        }
        finally
        {
            var tags = new TagList
            {
                { "dataverse.operation", operation },
                { "dataverse.table", tableName ?? string.Empty },
                { "outcome", outcome },
            };
            DataverseTelemetry.Operations.Add(1, tags);
            DataverseTelemetry.OperationDuration.Record(_timeProvider.GetElapsedTime(started).TotalSeconds, tags);
        }
    }

    internal static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
