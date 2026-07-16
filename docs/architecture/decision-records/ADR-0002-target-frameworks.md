# ADR-0002: Target frameworks and per-TFM dependency pinning

## Status

Accepted — 2026-07-16

## Context

The SDK must choose target frameworks (TFMs) balancing reach, maintenance cost, and access to
modern APIs (`TimeProvider`, `IAsyncEnumerable` ergonomics, `System.Diagnostics.Metrics`,
nullable reference types across the BCL). As of mid-2026, .NET 8 is the established LTS
baseline in enterprise use, .NET 9 is the current STS, and .NET 10 is the current LTS.
Separately, Dataverse plugin assemblies still execute in a sandbox that consumes
.NET Framework 4.6.2+-compatible assemblies, which only `netstandard2.0` reaches — relevant
solely to the FetchXML builder (future KDV-022), not to the HTTP client.

Microsoft.Extensions.* packages ship in version bands aligned with runtime versions; a single
unconditioned version would force net8.0 consumers to pull 10.0.x packages.

## Decision

We will target **net8.0, net9.0, and net10.0** for `Koras.Dataverse.Abstractions`,
`Koras.Dataverse`, and `Koras.Dataverse.OpenTelemetry`.

`Koras.Dataverse.FetchXml` will additionally target **netstandard2.0**, because the standalone
builder is genuinely valuable inside Dataverse plugin assemblies; it is the only package where
netstandard2.0 is offered, and its netstandard2.0 build must be complete, not a stub.

Microsoft.Extensions.* dependencies will be **pinned per TFM** via conditional versions in
Central Package Management (ADR-0009): 8.0.x for net8.0, 9.0.x for net9.0, 10.0.x for
net10.0, so consumers on older runtimes are not forced to upgrade their platform packages.

TFM lifecycle: TFMs are added/dropped following .NET support lifecycle; dropping a TFM is a
breaking change released only in a major version
(see [`../../api/backward-compatibility.md`](../../api/backward-compatibility.md)).

## Consequences

- Modern language/BCL features are usable everywhere except the FetchXml netstandard2.0
  compilation, which constrains that package to APIs available on netstandard2.0 (enforced in
  CI by actually building that target).
- The build/test matrix covers three (four for FetchXml) TFMs; CI cost is accepted.
- Per-TFM pinning means three conditional version rows per Microsoft.Extensions package in
  `Directory.Packages.props` — more bookkeeping, dramatically better consumer experience.
- No netstandard2.0 for the client rules out .NET Framework consumers of the HTTP client;
  they are served, if ever, by the v1.1 OrganizationService package direction, not by
  down-leveling the core.
- FetchXml must avoid multi-target feature drift: public API must be identical across its
  TFMs (verified by PublicAPI files per TFM, ADR-0010).

## Alternatives considered

- **netstandard2.0 for all packages.** Rejected: loses `TimeProvider`, modern metrics, and
  async ergonomics; drags in dozens of compatibility packages; contradicts the
  "HttpClientFactory-era" positioning.
- **net8.0 only.** Rejected: single-TFM simplicity, but net9.0/net10.0 consumers would get
  8.0.x Microsoft.Extensions bindings and miss runtime-specific optimizations; roll-forward
  friction in mixed solutions.
- **Latest TFM only (net10.0).** Rejected: excludes the large net8.0 LTS install base at
  exactly the moment the SDK needs adoption.
- **Unconditioned single Microsoft.Extensions version (e.g., always 8.0.x).** Rejected: works
  but pins net10.0 apps to old abstractions packages and causes NU1608-style conflicts in
  solutions already on newer bands.
