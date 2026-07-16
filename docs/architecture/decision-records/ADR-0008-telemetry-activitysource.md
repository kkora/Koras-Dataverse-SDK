# ADR-0008: Telemetry via ActivitySource/Meter with zero OTel dependency

## Status

Accepted — 2026-07-16

## Context

First-class observability is a headline differentiator (master plan §1, KDV-011): traces for
each logical operation, metrics for operations/retries/throttles, structured logging. The
question is how to emit telemetry without imposing an observability stack on consumers who
have none, and without version-coupling consumers who do use OpenTelemetry to the OTel
package train.

Since .NET 5+, the BCL's `System.Diagnostics.ActivitySource` and (since .NET 6)
`System.Diagnostics.Metrics.Meter` are the platform-native instrumentation APIs. The
OpenTelemetry .NET SDK consumes them by *name subscription* (`AddSource`, `AddMeter`) — a
library does not need any OTel reference to be fully OTel-compatible. Unlistened sources and
meters are near-zero cost.

## Decision

The core `Koras.Dataverse` package will instrument itself using **BCL primitives only**:

- `ActivitySource` named `"Koras.Dataverse"` — one `dataverse.execute` span per logical
  operation, started in the client layer so it encloses token acquisition and all retry
  attempts (master plan §5), with the tag set defined in
  [`../observability.md`](../observability.md).
- `Meter` named `"Koras.Dataverse"` — instruments `koras.dataverse.client.operations`,
  `koras.dataverse.client.operation.duration` (seconds), `koras.dataverse.client.retries`,
  `koras.dataverse.client.throttles`.
- `ILogger` via `Microsoft.Extensions.Logging.Abstractions` under categories
  `Koras.Dataverse` and `Koras.Dataverse.Http`.

The core package will have **zero OpenTelemetry dependencies**. A separate helper package,
`Koras.Dataverse.OpenTelemetry`, will provide `TracerProviderBuilder` /
`MeterProviderBuilder` extensions that register the source/meter names; it depends only on
`OpenTelemetry.Api` (plus the core package for the name constants) and contains no
instrumentation logic of its own.

Privacy is part of the decision: spans, metrics, and logs carry identifiers and measurements
only — never row data, attribute values, query literals, or tokens.

## Consequences

- Consumers without observability pay effectively nothing; consumers with OTel add one
  helper package (or just call `AddSource`/`AddMeter` with the documented names themselves).
- The SDK works with any `ActivityListener`/`MeterListener` consumer —
  `dotnet-counters`, `dotnet-trace`, Application Insights auto-collection — not only OTel.
- No OTel version coupling in the core: OTel API/SDK major-version churn affects only the
  small helper package.
- Source/meter/instrument names become public contract
  ([`../../api/backward-compatibility.md`](../../api/backward-compatibility.md)); renaming
  them is a breaking change, so they were chosen to be stable and conventional up front.
- We forgo OTel-specific enrichment APIs (processors, samplers) inside the SDK — correctly
  so; those belong to the consumer's pipeline.
- The helper package must resist scope creep: registration only, no processors, no payload
  inspection ([`../package-boundaries.md`](../package-boundaries.md)).

## Alternatives considered

- **OpenTelemetry.Api dependency in the core package.** Rejected: unnecessary — the BCL
  primitives are the interoperable layer; would impose OTel versioning on every consumer for
  zero functional gain.
- **No telemetry in core; instrumentation implemented inside the OTel package.** Rejected:
  instrumentation needs internal visibility (retry counts, request ids) that would force an
  awkward internal-hooks API; non-OTel consumers would get nothing.
- **Telemetry in a DelegatingHandler.** Rejected: a handler-level span would wrap each HTTP
  attempt separately, fragmenting one logical operation across retries; the master plan (§5)
  requires activities to wrap retries, hence client-layer emission.
- **`DiagnosticSource`/EventCounters (older primitives).** Rejected: superseded by
  ActivitySource/Meter, weaker typing, poorer OTel mapping.
