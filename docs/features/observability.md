# Feature Planning — KDV-011 Observability

> Planning-level design document (pre-implementation). Catalog entry:
> [`feature-catalog.md`](feature-catalog.md#kdv-011--observability).
> Source of truth: [`docs/planning/master-plan.md`](../planning/master-plan.md) §2, §3
> (KDV-011), §5. Release classification: **MVP**.

## Overview

KDV-011 makes Dataverse traffic observable like the rest of a modern .NET system: structured
`ILogger` logging with stable categories, distributed tracing via an `ActivitySource` named
`"Koras.Dataverse"`, and metrics via `Meter` counters/histograms. The core package emits
through BCL primitives only — **no OpenTelemetry dependency in core** (master plan §3); the
companion package `Koras.Dataverse.OpenTelemetry` provides `TracerProviderBuilder` /
`MeterProviderBuilder` extensions to subscribe OTel to the SDK's sources (master plan §2).
Activities are emitted at the client layer, not in a handler, so a single activity wraps all
retry attempts (master plan §5).

## Requirements

**Functional**

1. **Tracing.** `ActivitySource` `"Koras.Dataverse"`; one activity per logical client
   operation (CRUD call, query page, fetch page, batch, metadata call, solution operation,
   WhoAmI), wrapping retries; standard tags: operation kind, entity logical name, client
   name; error status set per failure.
2. **Metrics.** A `Meter` (name proposed as `"Koras.Dataverse"`, subject to implementation)
   with instruments covering: operation count, operation duration histogram, retry count
   (KDV-008), error count tagged by `DataverseErrorCategory` (KDV-009). Instrument names
   follow OpenTelemetry semantic naming conventions; the final instrument list is documented
   as a contract page before `0.1.0`.
3. **Logging.** `ILogger` categories per functional area (e.g., `Koras.Dataverse.Client`,
   `Koras.Dataverse.Authentication` — final category list documented as contract);
   structured, source-generated log messages (proposed); failures logged exactly once.
4. **OTel package.** `Koras.Dataverse.OpenTelemetry` exposes builder extensions that register
   the SDK's ActivitySource/Meter names with `TracerProviderBuilder` and
   `MeterProviderBuilder` (extension names subject to implementation). It references the core
   package for ids only, plus `OpenTelemetry.Api` (master plan §2).

**Nonfunctional.** Near-zero overhead with no listeners attached; instrument names, tags, and
log categories are treated as **public contract** once shipped (renames are breaking);
telemetry can never fail an operation.

## Proposed public API

Core: no public telemetry types — the surface is the emitted names/tags themselves, published
in a telemetry contract reference page.

OTel package (conservative shape, subject to implementation):

```csharp
tracerProviderBuilder.AddDataverseInstrumentation();
meterProviderBuilder.AddDataverseInstrumentation();
```

## Configuration

- Logging verbosity via standard `Logging` configuration per category — no bespoke SDK knobs.
- Tracing/metrics enablement is listener-driven (standard .NET model): nothing emitted
  without a listener; no on/off options in `DataverseClientOptions` planned for MVP.
- Payload-content logging does not exist at information level; any diagnostic body logging is
  a deliberate, documented opt-in decision deferred beyond MVP (subject to implementation).

## Error conditions

- Listener exceptions (badly behaved `ActivityListener`/`MeterListener`) must not propagate
  into client operations — standard BCL behavior, verified by tests where practical.
- Telemetry emission failures are swallowed by design; the operation result is never affected.
- Absence of the OTel package changes nothing in core behavior.

## Security

- **Never in telemetry:** tokens, secrets, credential material, full record payloads,
  attribute values (master plan §7).
- Allowed tags are structural: operation kinds, logical names, client names, status codes,
  error categories, request ids.
- Query filter values are business data: they do not appear in tags; debug-level logging that
  includes query strings is documented as sensitive.

## Performance

- Fast path guarded: tag computation and allocation only when
  `Activity.IsAllDataRequested` / instrument-enabled checks pass.
- Histogram tag cardinality bounded (operation kind × outcome × client name — no per-entity
  explosion beyond logical name, which is bounded per environment).
- Source-generated logging avoids boxing/formatting when the level is off (proposed).
- Overhead with no listeners verified during the hardening milestone benchmarks (master plan
  §8, milestone 8) — no numbers promised in advance.

## Observability

Self-referential requirement: the telemetry itself is documented. A contract page lists every
activity name, tag, instrument, unit, and log category with stability guarantees — consumers
build dashboards against it, so it versions like API.

## Test plan

**Unit** (master plan §6 patterns):
- `ActivityListener`-based tests: activity created per operation type; correct source name;
  tags present and correct; error status on failure; one activity spans a scripted
  multi-retry sequence (fake handler + fake time).
- `MeterListener`-based tests: each instrument emits with expected tags on success/failure/
  retry paths.
- Log assertions: categories, one-failure-one-log rule, no token/secret content (shared
  hygiene assertions with KDV-001/KDV-009).
- No-listener path: operations execute with no telemetry side effects (and, as a smoke-level
  guard, no activity allocations).
- OTel package: `AddDataverseInstrumentation()` registers exactly the SDK source/meter names.

**Integration**: the minimal API sample wired with the OTel package exports spans and metrics
to an in-memory/OTLP test exporter; a traced end-to-end WhoAmI shows the Dataverse span
parented correctly.

**Architecture** (master plan §6): core packages have zero OpenTelemetry references.

## Acceptance criteria

1. Every client operation type produces exactly one activity from source
   `"Koras.Dataverse"`, wrapping its retries.
2. Documented instruments are observable via `MeterListener` with the documented names/tags.
3. Core has no OpenTelemetry package reference (architecture-test enforced); the OTel
   package lights everything up with one call per provider builder.
4. Telemetry contract page exists and matches emitted names mechanically (shared constants).
5. Hygiene tests prove no secrets/tokens/payloads in any telemetry channel.
6. All telemetry is inert (no errors, no listener requirement) when nothing subscribes.
