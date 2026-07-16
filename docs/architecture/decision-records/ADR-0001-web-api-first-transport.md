# ADR-0001: Web API–first transport

## Status

Accepted — 2026-07-16

## Context

Dataverse exposes two programmatic surfaces: the OData v4 Web API (`/api/data/v9.2/`) and the
legacy SOAP/WCF Organization Service, most commonly consumed through
`Microsoft.PowerPlatform.Dataverse.Client` (ServiceClient) and its `IOrganizationService`
model. The SDK's positioning (master plan §1) is "the HttpClientFactory-era Dataverse SDK":
modern async, DI-native, observable, testable. The transport choice determines the dependency
graph, the async model, the error surface, and how much of the HTTP ecosystem
(IHttpClientFactory, DelegatingHandlers, standard HttpClient instrumentation) we can reuse.

Some enterprise customers nevertheless standardize on `IOrganizationService` semantics
(existing plugin-shaped code, organization policy, message coverage expectations), so a path
for them cannot be closed off entirely.

## Decision

We will build the core package (`Koras.Dataverse`) exclusively on the Dataverse **Web API
v9.2** over `HttpClient`/`IHttpClientFactory`. The core package will have no reference to
`Microsoft.PowerPlatform.Dataverse.Client`, `Microsoft.Xrm.Sdk`, or any WCF component.

Organization Service support will be offered later (target v1.1, feature KDV-015) as a
separate, optional package `Koras.Dataverse.OrganizationService` that adapts the official
ServiceClient behind the `Koras.Dataverse.Abstractions` interfaces. To keep that possible, the
`Abstractions` package contains no HTTP types on any signature.

We pin the `v9.2` route segment and treat Web API contract drift as a tested risk
(integration test matrix, error-tolerant parsing — master plan §8).

## Consequences

- The core dependency graph stays small (Azure.Identity + Microsoft.Extensions.* only); the
  heavy ServiceClient graph never reaches mainstream consumers.
- We get the full HttpClient ecosystem for free: factory-managed handler lifetimes, custom
  DelegatingHandlers as an extension point, standard runtime HTTP instrumentation.
- Async is genuinely async end to end; no lock-based sync paths inherited from ServiceClient.
- We must implement Web API plumbing ourselves: OData encoding, `$batch` multipart handling,
  error payload normalization, service-protection retry semantics — this is the SDK's core
  value and is covered by KDV-002…KDV-009.
- Features that exist only as Organization Service messages are out of core scope until an
  equivalent Web API path exists or the v1.1 adapter ships.
- The `Abstractions` surface must remain transport-neutral forever; every API addition is
  reviewed against "could the OrganizationService adapter honor this?".

## Alternatives considered

- **Wrap `Microsoft.PowerPlatform.Dataverse.Client`.** Rejected: inherits the legacy
  programming model, heavyweight dependencies, weaker async/DI/telemetry story — the exact
  gaps this SDK exists to fill (master plan §1 competitive table).
- **Support both transports in the core package.** Rejected: forces the heavy dependency graph
  and a lowest-common-denominator API onto all consumers; doubles the test matrix for the MVP.
- **Web API only, forever (no adapter package).** Rejected: closes the door on enterprises
  that mandate `IOrganizationService` semantics; keeping a quarantined optional package costs
  the core design only the "no HTTP types in Abstractions" rule, which is good hygiene anyway.
