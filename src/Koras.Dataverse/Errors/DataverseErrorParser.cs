using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Koras.Dataverse.Errors;

/// <summary>Normalizes Dataverse Web API failure responses into <see cref="DataverseError"/>.</summary>
internal static class DataverseErrorParser
{
    private const string RequestIdHeader = "x-ms-service-request-id";

    public static async Task<DataverseError> FromResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        string? code = null;
        string? message = null;

        try
        {
            string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(body))
            {
                (code, message) = ParseBody(body);
            }
        }
        catch (Exception exception) when (exception is IOException or HttpRequestException or JsonException)
        {
            // The error payload could not be read or parsed; classification proceeds from the status code.
        }

        return Create((int)response.StatusCode, code, message, RequestId(response), RetryAfter(response));
    }

    public static (string? Code, string? Message) ParseBody(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("error", out JsonElement error) &&
                error.ValueKind == JsonValueKind.Object)
            {
                string? code = error.TryGetProperty("code", out JsonElement c) && c.ValueKind == JsonValueKind.String ? c.GetString() : null;
                string? message = error.TryGetProperty("message", out JsonElement m) && m.ValueKind == JsonValueKind.String ? m.GetString() : null;
                return (code, message);
            }
        }
        catch (JsonException)
        {
            // Not JSON — fall through.
        }

        return (null, null);
    }

    public static DataverseError Create(int statusCode, string? code, string? message, string? requestId, TimeSpan? retryAfter)
    {
        DataverseErrorCategory category = statusCode switch
        {
            (int)HttpStatusCode.BadRequest => DataverseErrorCategory.Validation,
            (int)HttpStatusCode.Unauthorized => DataverseErrorCategory.Authentication,
            (int)HttpStatusCode.Forbidden => DataverseErrorCategory.Authorization,
            (int)HttpStatusCode.NotFound => DataverseErrorCategory.NotFound,
            (int)HttpStatusCode.RequestTimeout => DataverseErrorCategory.Timeout,
            (int)HttpStatusCode.Conflict or (int)HttpStatusCode.PreconditionFailed => DataverseErrorCategory.Concurrency,
            (int)HttpStatusCode.TooManyRequests => DataverseErrorCategory.Throttling,
            >= 500 => DataverseErrorCategory.Server,
            _ => DataverseErrorCategory.Unknown,
        };

        bool transient = category is DataverseErrorCategory.Throttling or DataverseErrorCategory.Timeout
            || statusCode is (int)HttpStatusCode.BadGateway or (int)HttpStatusCode.ServiceUnavailable or (int)HttpStatusCode.GatewayTimeout;

        return new DataverseError
        {
            Category = category,
            Message = string.IsNullOrWhiteSpace(message)
                ? $"Dataverse returned HTTP {statusCode}."
                : message!,
            HttpStatusCode = statusCode,
            ErrorCode = code,
            RequestId = requestId,
            RetryAfter = retryAfter,
            IsTransient = transient,
        };
    }

    private static string? RequestId(HttpResponseMessage response) =>
        response.Headers.TryGetValues(RequestIdHeader, out IEnumerable<string>? values) ? values.FirstOrDefault() : null;

    private static TimeSpan? RetryAfter(HttpResponseMessage response)
    {
        RetryConditionHeaderValue? header = response.Headers.RetryAfter;
        if (header is null)
        {
            return null;
        }

        if (header.Delta is TimeSpan delta)
        {
            return delta;
        }

        if (header.Date is DateTimeOffset date)
        {
            TimeSpan until = date - DateTimeOffset.UtcNow;
            return until > TimeSpan.Zero ? until : TimeSpan.Zero;
        }

        return null;
    }
}
