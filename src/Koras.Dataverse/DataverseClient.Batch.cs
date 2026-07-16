using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using Koras.Dataverse.Batches;
using Koras.Dataverse.Errors;

namespace Koras.Dataverse;

/// <content>OData <c>$batch</c> execution: payload assembly and multipart response parsing.</content>
public sealed partial class DataverseClient
{
    /// <inheritdoc />
    public async Task<BatchResponse> ExecuteBatchAsync(BatchRequest batch, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batch);
        if (batch.Operations.Count == 0)
        {
            throw new ArgumentException("The batch contains no operations.", nameof(batch));
        }

        string batchBoundary = "batch_" + Guid.NewGuid().ToString("N");
        string payload = BuildBatchPayload(batch, batchBoundary);

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("$batch", UriKind.Relative));
        request.Content = new StringContent(payload, Encoding.UTF8);
        request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse($"multipart/mixed; boundary={batchBoundary}");
        if (!batch.Atomic)
        {
            request.Headers.TryAddWithoutValidation("Prefer", "odata.continue-on-error");
        }

        using HttpResponseMessage response = await SendAsync(request, "batch", null, cancellationToken).ConfigureAwait(false);

        string responseBoundary = response.Content.Headers.ContentType?.Parameters
            .FirstOrDefault(p => p.Name.Equals("boundary", StringComparison.OrdinalIgnoreCase))?.Value?.Trim('"')
            ?? throw new DataverseException(new DataverseError
            {
                Category = DataverseErrorCategory.Unknown,
                Message = "The batch response did not declare a multipart boundary.",
                HttpStatusCode = (int)response.StatusCode,
            });

        string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var results = new List<BatchItemResult>(batch.Operations.Count);
        ParseMultipart(body, responseBoundary, results);

        if (batch.Atomic && results.Any(r => !r.Succeeded))
        {
            // The change set rolled back as a unit; surface the first error as the batch failure.
            BatchItemResult failed = results.First(r => !r.Succeeded);
            throw new DataverseException(failed.Error!);
        }

        return new BatchResponse(results);
    }

    private string BuildBatchPayload(BatchRequest batch, string batchBoundary)
    {
        var payload = new StringBuilder(batch.Operations.Count * 256);

        if (batch.Atomic)
        {
            string changeSetBoundary = "changeset_" + Guid.NewGuid().ToString("N");
            payload.Append("--").Append(batchBoundary).Append("\r\n");
            payload.Append("Content-Type: multipart/mixed; boundary=").Append(changeSetBoundary).Append("\r\n\r\n");

            for (int i = 0; i < batch.Operations.Count; i++)
            {
                AppendOperation(payload, changeSetBoundary, batch.Operations[i], contentId: i + 1);
            }

            payload.Append("--").Append(changeSetBoundary).Append("--\r\n");
            payload.Append("--").Append(batchBoundary).Append("--\r\n");
        }
        else
        {
            foreach (BatchOperation operation in batch.Operations)
            {
                AppendOperation(payload, batchBoundary, operation, contentId: null);
            }

            payload.Append("--").Append(batchBoundary).Append("--\r\n");
        }

        return payload.ToString();
    }

    private void AppendOperation(StringBuilder payload, string boundary, BatchOperation operation, int? contentId)
    {
        payload.Append("--").Append(boundary).Append("\r\n");
        payload.Append("Content-Type: application/http\r\n");
        payload.Append("Content-Transfer-Encoding: binary\r\n");
        if (contentId.HasValue)
        {
            payload.Append("Content-ID: ").Append(contentId.Value.ToString(CultureInfo.InvariantCulture)).Append("\r\n");
        }

        payload.Append("\r\n");

        switch (operation.Type)
        {
            case BatchOperationType.Create:
                AppendHttpRequest(payload, "POST", EntitySet(operation.Entity!.TableName), _serializer.WritePayload(operation.Entity), null);
                break;
            case BatchOperationType.Update:
                AppendHttpRequest(
                    payload,
                    "PATCH",
                    $"{EntitySet(operation.Entity!.TableName)}({operation.Entity.Id:D})",
                    _serializer.WritePayload(operation.Entity),
                    "If-Match: *");
                break;
            case BatchOperationType.Upsert:
                AppendHttpRequest(
                    payload,
                    "PATCH",
                    $"{EntitySet(operation.Entity!.TableName)}({operation.Entity.Id:D})",
                    _serializer.WritePayload(operation.Entity),
                    null);
                break;
            case BatchOperationType.Delete:
                AppendHttpRequest(payload, "DELETE", $"{EntitySet(operation.Reference!.TableName)}({operation.Reference.Id:D})", null, null);
                break;
            default:
                throw new InvalidOperationException($"Unsupported batch operation type '{operation.Type}'.");
        }
    }

    private void AppendHttpRequest(StringBuilder payload, string method, string relativeUrl, string? json, string? extraHeader)
    {
        payload.Append(method).Append(' ').Append(AbsoluteUrl(relativeUrl)).Append(" HTTP/1.1\r\n");
        if (extraHeader is not null)
        {
            payload.Append(extraHeader).Append("\r\n");
        }

        if (json is not null)
        {
            payload.Append("Content-Type: application/json; type=entry\r\n\r\n");
            payload.Append(json).Append("\r\n");
        }
        else
        {
            payload.Append("\r\n");
        }
    }

    private static void ParseMultipart(string body, string boundary, List<BatchItemResult> results)
    {
        string marker = "--" + boundary;
        string[] parts = body.Split(marker, StringSplitOptions.None);

        foreach (string rawPart in parts)
        {
            string part = rawPart.TrimStart('\r', '\n');
            if (part.Length == 0 || part.StartsWith("--", StringComparison.Ordinal))
            {
                continue; // preamble or terminator
            }

            int nestedIndex = part.IndexOf("boundary=", StringComparison.OrdinalIgnoreCase);
            if (part.StartsWith("Content-Type: multipart/mixed", StringComparison.OrdinalIgnoreCase) && nestedIndex > 0)
            {
                int lineEnd = part.IndexOf("\r\n", nestedIndex, StringComparison.Ordinal);
                string nestedBoundary = (lineEnd > 0 ? part[(nestedIndex + 9)..lineEnd] : part[(nestedIndex + 9)..]).Trim().Trim('"');

                // Recurse only into the content after the nested header block; passing the whole
                // part would re-match this header on the preamble segment and never terminate.
                int nestedBodyStart = part.IndexOf("\r\n\r\n", StringComparison.Ordinal);
                if (nestedBodyStart > 0)
                {
                    ParseMultipart(part[(nestedBodyStart + 4)..], nestedBoundary, results);
                }

                continue;
            }

            int statusIndex = part.IndexOf("HTTP/1.1 ", StringComparison.Ordinal);
            if (statusIndex < 0)
            {
                continue;
            }

            results.Add(ParseItem(part, statusIndex, results.Count));
        }
    }

    private static BatchItemResult ParseItem(string part, int statusIndex, int index)
    {
        int statusCode = 0;
        string statusText = part[(statusIndex + 9)..];
        int space = statusText.IndexOf(' ', StringComparison.Ordinal);
        int lineBreak = statusText.IndexOf("\r\n", StringComparison.Ordinal);
        int end = space >= 0 && (lineBreak < 0 || space < lineBreak) ? space : lineBreak;
        if (end > 0)
        {
            _ = int.TryParse(statusText[..end], NumberStyles.Integer, CultureInfo.InvariantCulture, out statusCode);
        }

        Guid? createdId = null;
        int idIndex = part.IndexOf("OData-EntityId:", StringComparison.OrdinalIgnoreCase);
        if (idIndex >= 0)
        {
            int idLineEnd = part.IndexOf("\r\n", idIndex, StringComparison.Ordinal);
            string idLine = idLineEnd > 0 ? part[idIndex..idLineEnd] : part[idIndex..];
            int open = idLine.LastIndexOf('(');
            int close = idLine.LastIndexOf(')');
            if (open >= 0 && close > open && Guid.TryParse(idLine[(open + 1)..close], out Guid parsed))
            {
                createdId = parsed;
            }
        }

        DataverseError? error = null;
        if (statusCode >= 400)
        {
            int jsonStart = part.IndexOf('{', StringComparison.Ordinal);
            (string? code, string? message) = jsonStart >= 0
                ? DataverseErrorParser.ParseBody(part[jsonStart..].Trim())
                : (null, null);
            error = DataverseErrorParser.Create(statusCode, code, message, requestId: null, retryAfter: null);
        }

        return new BatchItemResult(index, statusCode, createdId, error);
    }
}
