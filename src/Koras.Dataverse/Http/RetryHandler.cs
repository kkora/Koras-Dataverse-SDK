using System.Net;
using Koras.Dataverse.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Koras.Dataverse.Http;

/// <summary>
/// Retries transient Dataverse failures: HTTP 429 (service protection), 502/503/504, and network
/// errors. Uses exponential backoff with jitter, honors <c>Retry-After</c>, and never retries
/// after cancellation. Total time is still bounded by the operation timeout enforced above this
/// handler.
/// </summary>
internal sealed class RetryHandler : DelegatingHandler
{
    private readonly DataverseRetryOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;

    public RetryHandler(DataverseRetryOptions options, TimeProvider? timeProvider = null, ILogger? logger = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _logger = logger ?? NullLogger.Instance;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        byte[]? bufferedContent = null;
        if (request.Content is not null)
        {
            bufferedContent = await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        }

        for (int attempt = 0; ; attempt++)
        {
            HttpRequestMessage attemptRequest = attempt == 0 ? request : Clone(request, bufferedContent);

            HttpResponseMessage? response = null;
            Exception? networkFailure = null;
            try
            {
                response = await base.SendAsync(attemptRequest, cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException exception)
            {
                networkFailure = exception;
            }

            if (response is not null)
            {
                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    DataverseTelemetry.Throttles.Add(1);
                }

                if (!IsRetryable(response.StatusCode) || attempt >= _options.MaxRetries)
                {
                    return response;
                }
            }
            else if (attempt >= _options.MaxRetries)
            {
                throw networkFailure!;
            }

            TimeSpan delay = ComputeDelay(attempt, response);
            _logger.LogWarning(
                "Dataverse request {Method} {Path} failed with {Reason}; retrying in {Delay} (attempt {Attempt}/{MaxRetries}).",
                request.Method,
                request.RequestUri?.AbsolutePath,
                response is not null ? $"HTTP {(int)response.StatusCode}" : networkFailure!.GetType().Name,
                delay,
                attempt + 1,
                _options.MaxRetries);

            DataverseTelemetry.Retries.Add(1);
            response?.Dispose();
            await Delay(delay, cancellationToken).ConfigureAwait(false);
        }
    }

    private static bool IsRetryable(HttpStatusCode statusCode) => statusCode is
        HttpStatusCode.TooManyRequests or
        HttpStatusCode.BadGateway or
        HttpStatusCode.ServiceUnavailable or
        HttpStatusCode.GatewayTimeout;

    private TimeSpan ComputeDelay(int attempt, HttpResponseMessage? response)
    {
        if (_options.RespectRetryAfter && response?.Headers.RetryAfter is { } retryAfter)
        {
            if (retryAfter.Delta is TimeSpan delta && delta > TimeSpan.Zero)
            {
                return delta;
            }

            if (retryAfter.Date is DateTimeOffset date)
            {
                TimeSpan until = date - _timeProvider.GetUtcNow();
                if (until > TimeSpan.Zero)
                {
                    return until;
                }
            }
        }

        double exponential = _options.BaseDelay.TotalMilliseconds * Math.Pow(2, attempt);
        double capped = Math.Min(exponential, _options.MaxDelay.TotalMilliseconds);
        double jitter = Random.Shared.NextDouble() * 0.25 * capped;
        return TimeSpan.FromMilliseconds(capped + jitter);
    }

    private Task Delay(TimeSpan delay, CancellationToken cancellationToken) =>
        Task.Delay(delay, _timeProvider, cancellationToken);

    private static HttpRequestMessage Clone(HttpRequestMessage original, byte[]? bufferedContent)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri)
        {
            Version = original.Version,
            VersionPolicy = original.VersionPolicy,
        };

        foreach (KeyValuePair<string, IEnumerable<string>> header in original.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (bufferedContent is not null && original.Content is not null)
        {
            var content = new ByteArrayContent(bufferedContent);
            foreach (KeyValuePair<string, IEnumerable<string>> header in original.Content.Headers)
            {
                content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            clone.Content = content;
        }

        return clone;
    }
}
