# Guide: Logging

The SDK logs through `Microsoft.Extensions.Logging` on two categories. Under DI the logger
factory is picked up automatically; for `DataverseClient.Create`, pass an `ILoggerFactory` as
the second argument (otherwise the SDK stays silent).

## Categories and what they emit

| Category | Source | Level | Event |
|---|---|---|---|
| `Koras.Dataverse` | Client layer | `Error` | An operation failed after retries — includes operation, table, error category, HTTP status, Dataverse error code, and request id |
| `Koras.Dataverse.Http` | Retry handler | `Warning` | A transient failure (429/502/503/504 or network error) is about to be retried — includes method, path, reason, delay, and attempt count |

Example lines:

```text
warn: Koras.Dataverse.Http
      Dataverse request GET /api/data/v9.2/accounts failed with HTTP 429; retrying in 00:00:02 (attempt 1/3).
fail: Koras.Dataverse
      Dataverse retrieve on 'account' failed: NotFound (HTTP 404, code 0x80040217, request 4c8d…).
```

A healthy application logs nothing from the SDK: no news is good news. Warnings signal
throttling/instability being absorbed; errors signal failures that reached your code (each one
pairs with a thrown `DataverseException`).

## Recommended appsettings

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Koras.Dataverse": "Warning",
      "Koras.Dataverse.Http": "Warning"
    }
  }
}
```

- Production: `Warning` on both — you see retries and failures, nothing else.
- Quiet mode: raise `Koras.Dataverse.Http` to `Error` to silence retry noise during a known
  throttling period (you keep the `koras.dataverse.client.retries` metric regardless).
- There is currently no verbose per-request debug logging below these events; for
  request-level inspection add your own `DelegatingHandler` (see
  [advanced scenarios](../recipes/advanced-scenarios.md)) or use
  [tracing](telemetry.md), which records every operation as a span.

## What is never logged

By design, at any level:

- **Access tokens / credentials** — never logged, never included in exception messages.
- **Row data** — attribute values, filter values, and payloads never appear; log lines carry
  only operation names, table logical names, status codes, error codes, and request ids.
- Full request URLs with query strings are not logged by the client layer; the retry handler
  logs the request *path* only (no query string, which could embed filter literals).

The same guarantee covers telemetry tags. If your custom handlers log request/response bodies,
that guarantee is yours to keep.

## Correlating logs with traces and support tickets

The failure log line includes the Dataverse **request id** (`x-ms-service-request-id`) — the
same value available at `DataverseException.Error.RequestId` and on the failed span as
`dataverse.request_id`. Grep for it to line up your logs, your traces, and a Microsoft support
case. See [diagnostics](../troubleshooting/diagnostics.md).

## Related

- [Troubleshooting: log filtering](../troubleshooting/logging.md) — more filter examples
- [Telemetry guide](telemetry.md)
