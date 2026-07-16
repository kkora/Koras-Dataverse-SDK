# Changelog

All notable changes to the Koras Dataverse SDK are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Public API surface tracking: `PublicAPI.Shipped.txt`/`PublicAPI.Unshipped.txt` populated for
  all shipped packages (ADR-0010).
- Package consumption tests (`tests/Koras.Dataverse.PackageTests`), including a .NET
  Framework 4.8 consumer proving the `Koras.Dataverse.FetchXml` netstandard2.0 asset.
- CI coverage gate (≥ 80 % line / ≥ 70 % branch on `Koras.Dataverse` + `Koras.Dataverse.FetchXml`)
  and a nightly live-integration workflow (skips cleanly until environment secrets exist).

## [0.1.0-preview.2] — 2026-07-16

### Changed

- New package icon (all packages).

## [0.1.0-preview.1] — 2026-07-16

First public preview.

### Added

- `Koras.Dataverse.Abstractions`: `IDataverseClient`, late-bound `Entity` model with plain CLR
  values, `EntityReference`, `ColumnSet`, `DataverseQueryResult`, batch models
  (`BatchRequest`/`BatchResponse`), metadata models, solution models, normalized error model
  (`DataverseException`, `DataverseError`, `DataverseErrorCategory`), `IDataverseTokenProvider`,
  `IDataverseClientFactory`, injection-safe `ODataQuery`/`ODataFilterBuilder`, and
  attribute-based POCO mapping (`EntityMapper`, `[DataverseTable]`, `[DataverseColumn]`). (KDV-002, KDV-003, KDV-009)
- `Koras.Dataverse.FetchXml`: dependency-free fluent FetchXML builder with strict encoding,
  link-entities, nested filters, paging support; netstandard2.0-compatible. (KDV-004)
- `Koras.Dataverse`: Web API client with CRUD + upsert (id and alternate key), associate /
  disassociate, OData and FetchXML execution with `IAsyncEnumerable` auto-paging, atomic and
  continue-on-error `$batch`, `WhoAmI`; metadata client (tables, columns, relationships,
  choices, entity-set names); solution client (export, import, publish-all, find);
  authentication via Azure.Identity (client secret, certificate, managed identity, interactive,
  `DefaultAzureCredential`, custom credential/provider) with cached single-flight token refresh;
  retry handler for 429/502/503/504 honoring `Retry-After` with jittered exponential backoff;
  per-operation timeout; `ActivitySource`/`Meter` telemetry; health check;
  `AddDataverse` DI registration with named clients and startup options validation. (KDV-001, KDV-002, KDV-005–KDV-012)
- `Koras.Dataverse.OpenTelemetry`: `AddKorasDataverseInstrumentation()` for tracing and metrics. (KDV-011)
- Repository foundation: planning/architecture/API/testing/security/performance/release docs,
  ADRs 0001–0010, samples (console, minimal API, worker service), benchmarks, unit /
  architecture / opt-in integration tests, GitHub Actions CI/CD.

### Fixed

- `ExecuteBatchAsync`: parsing a change-set (nested multipart) response no longer recurses
  infinitely — atomic batch responses previously crashed the process with a stack overflow. (KDV-005)
