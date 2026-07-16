# ADR-0003: Package layout and DI registration placement

## Status

Accepted — 2026-07-16

## Context

The SDK needs a package layout that lets (a) application/domain code and tests depend on
contracts without the implementation, (b) plugin projects use the FetchXML builder with zero
baggage, (c) OpenTelemetry users wire up instrumentation without forcing OTel packages on
everyone, and (d) typical applications install one obvious package.

A recurring .NET ecosystem question is whether DI registration extensions
(`IServiceCollection`) belong in the main package or a separate `*.DependencyInjection`
package. Historically, separate DI packages existed to keep `IServiceCollection` off
libraries that might be used without DI; since `Microsoft.Extensions.DependencyInjection.Abstractions`
became the universal, tiny, dependency-light norm, mainstream libraries register in the main
package.

## Decision

We will ship four MVP packages with these boundaries (master plan §2, detailed in
[`../package-boundaries.md`](../package-boundaries.md)):

- `Koras.Dataverse.Abstractions` — interfaces, models, errors, options; zero dependencies.
- `Koras.Dataverse.FetchXml` — standalone FetchXML builder; zero dependencies;
  netstandard2.0-compatible.
- `Koras.Dataverse` — the Web API implementation and the package consumers install.
- `Koras.Dataverse.OpenTelemetry` — OTel registration helpers only.

DI registration (`AddDataverse`, `AddDataverseHealthCheck`, named clients,
`IDataverseClientFactory` implementation) lives **in the main `Koras.Dataverse` package**, in
the `Microsoft.Extensions.DependencyInjection` namespace per platform convention. We will
**not** create a `Koras.Dataverse.DependencyInjection` package.

## Consequences

- One-package install experience: `dotnet add package Koras.Dataverse` gives a working,
  DI-registered client; Abstractions and FetchXml arrive transitively.
- Domain libraries and test projects reference `Abstractions` only and stay free of
  Azure.Identity/HTTP dependencies; mocking `IDataverseClient` requires nothing else.
- The main package carries `Microsoft.Extensions.DependencyInjection.Abstractions` and
  related abstractions packages — acceptable, they are effectively part of the platform.
- Consumers not using Microsoft DI can still construct the client via
  `IDataverseClientFactory`-independent paths only insofar as the public API allows; the
  supported path is DI-first. This is a deliberate trade aligned with the product positioning.
- Four packages to version and release together; release tooling must keep versions in
  lockstep for the SDK family.

## Alternatives considered

- **Separate `Koras.Dataverse.DependencyInjection` package.** Rejected: adds a discovery and
  install step for every consumer to avoid a dependency that is universal in modern .NET;
  the abstractions-only DI reference is not a meaningful burden (master plan §2 calls this
  out explicitly).
- **Single monolithic package.** Rejected: forces Azure.Identity and HTTP dependencies on
  domain/test projects and makes the plugin-safe FetchXML story impossible.
- **DI extensions in `Abstractions`.** Rejected: registration must construct implementation
  types, which would invert the dependency direction or require reflection hacks.
- **FetchXml folded into Abstractions.** Rejected: Abstractions targets net8.0+ while the
  builder needs netstandard2.0; merging would either down-level Abstractions or fork the
  builder.
