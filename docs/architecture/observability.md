# Observability

> Elaborates KDV-011 and §5 of [`docs/planning/master-plan.md`](../planning/master-plan.md).
> If this document and the master plan disagree, the master plan wins. The zero-OTel-dependency
> design is recorded in [ADR-0008](decision-records/ADR-0008-telemetry-activitysource.md).

The core package instruments itself with BCL primitives only — `ILogger` (via
`Microsoft.Extensions.Logging.Abstractions`), `System.Diagnostics.ActivitySource`, and
`System.Diagnostics.Metrics.Meter`. The `Koras.Dataverse.OpenTelemetry` package is a thin
registration helper; it adds no instrumentation of its own.

A single privacy rule governs everything below: **no row data, no attribute values, no query
literal values, and no tokens ever appear in logs, span tags, or metric tags.** Identifiers
that are safe by design — table logical names, operation names, HTTP status codes, Dataverse
request ids, error categories — are the only cardinality the SDK emits.

## 1. Logging

### Categories

| Category | Content |
|---|---|
| `Koras.Dataverse` | Client-level operations: operation start/stop outcomes, error mapping results, token cache refresh events, batch composition summaries, health check results |
| `Koras.Dataverse.Http` | Pipeline-level events: request attempts, retry decisions (status, delay, attempt number), throttle signals, `Retry-After` values |

Consumers tune them independently, e.g. `"Koras.Dataverse": "Information"` with
`"Koras.Dataverse.Http": "Warning"` in production.

### Event structure

All events are structured (message templates + named placeholders, `LoggerMessage`-style
source-generated where beneficial) with a stable `EventId` per event. Standard placeholder
names align with the telemetry tag names (`Operation`, `TableName`, `StatusCode`,
`RequestId`, `Attempt`, `DelayMs`, `ErrorCategory`) so log/trace/metric correlation works by
name. Exact event ids and message templates are implementation detail, **not** public
contract (see [`../api/backward-compatibility.md`](../api/backward-compatibility.md)) —
consumers must not parse log text.

### Level policy

| Level | Used for | Payload rule |
|---|---|---|
| Trace | Request/response line-level diagnostics (URLs with query structure, header names) | Never response bodies or attribute values; still no tokens or secrets at any level |
| Debug | Retry timing decisions, token cache hits/refreshes, paging continuation | No payload data |
| Information | One event per logical operation completion: operation, table, duration, status, request id | **No payload data at Information** — identifiers and measurements only |
| Warning | Retries after throttling, transient failures being retried, approaching configured limits | No payload data |
| Error | Operation failed after retries; error category, Dataverse code, request id | Normalized safe message only |
| Critical | Not used by the SDK | — |

## 2. Tracing (`ActivitySource`)

- **Source name:** `Koras.Dataverse` (also the source *version* follows the package version).
- **Span:** `dataverse.execute` — one span per logical client operation, started in the client
  layer so it encloses token acquisition and **all retry attempts** (master plan §5:
  telemetry is emitted by the client layer, not a handler).
- **Kind:** `Client`.

### Tags

| Tag | Example | Notes |
|---|---|---|
| `dataverse.operation` | `create`, `retrieve`, `update`, `delete`, `upsert`, `query`, `fetch`, `batch`, `whoami`, `metadata.get`, `solution.import` | Low-cardinality operation name; exact value list finalized at implementation review |
| `dataverse.table` | `account` | Table logical name; omitted for non-table operations |
| `http.response.status_code` | `200` | Final status after retries; omitted when no response was received |
| `dataverse.request_id` | `c0f3…` | Dataverse service request id from the final attempt |

No row data: no attribute names/values, no filter values, no record ids beyond the service
request id. Span status is set to error (with the error category as the status description)
when the operation throws.

Individual HTTP attempts may additionally appear as child spans if the consumer enables the
runtime's standard `HttpClient` instrumentation; the SDK does not duplicate that layer.

## 3. Metrics (`Meter`)

- **Meter name:** `Koras.Dataverse` (version = package version).

| Instrument | Type | Unit | Tags |
|---|---|---|---|
| `koras.dataverse.client.operations` | Counter | operations | `operation`, `table`, `outcome` |
| `koras.dataverse.client.operation.duration` | Histogram | seconds | `operation`, `table`, `outcome` |
| `koras.dataverse.client.retries` | Counter | retries | `operation`, `table` |
| `koras.dataverse.client.throttles` | Counter | throttle signals | `operation`, `table` |

Semantics:

- `operation` / `table` use the same values as the span tags above.
- `outcome` is `success` or an error category name in lowercase (`throttling`, `validation`,
  …) — never a free-form message.
- `…operations` and `…operation.duration` record once per logical operation (after all
  retries), measuring end-to-end duration in seconds including backoff waits.
- `…retries` increments once per retry attempt (attempt 2 onward).
- `…throttles` increments once per throttling signal observed (429, or 403/503 with a
  service-protection code), whether or not the retry ultimately succeeded.

Tag sets are intentionally minimal and low-cardinality; the instrument names and the tags
listed here are public contract once shipped. Any additional tags introduced later will be
documented and, until stabilized, explicitly marked experimental.

## 4. OpenTelemetry integration (`Koras.Dataverse.OpenTelemetry`)

The core package emits into `ActivitySource`/`Meter` unconditionally (near-zero cost with no
listener attached). OpenTelemetry users subscribe by name; the helper package removes the
magic strings:

```csharp
services.AddOpenTelemetry()
    .WithTracing(t => t.AddDataverseInstrumentation())   // ≈ AddSource("Koras.Dataverse")
    .WithMetrics(m => m.AddDataverseInstrumentation());  // ≈ AddMeter("Koras.Dataverse")
```

- Dependencies: `OpenTelemetry.Api` only — no SDK, no exporters (see
  [`package-boundaries.md`](package-boundaries.md)).
- The extensions register the source and meter names; they add no processors, no enrichment,
  and never read payloads. Exact extension method names are subject to implementation review;
  the registration-only role is fixed by ADR-0008.
- Non-OTel consumers can subscribe the same way with `ActivityListener` / `MeterListener`, or
  with `dotnet-counters`/`dotnet-trace` out of process.

## 5. Correlation

- The `dataverse.request_id` span tag, the `RequestId` log placeholder, and
  `DataverseError.RequestId` carry the same value, so a support case can be correlated across
  traces, logs, exceptions, and Microsoft-side diagnostics.
- Spans flow trace context as usual; the SDK does not create its own correlation scheme.
