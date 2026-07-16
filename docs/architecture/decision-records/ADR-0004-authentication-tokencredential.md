# ADR-0004: TokenCredential-based authentication behind IDataverseTokenProvider

## Status

Accepted — 2026-07-16

## Context

Dataverse authenticates via Microsoft Entra ID bearer tokens with resource scope
`{environmentUrl}/.default`. Consumers need every mainstream flow: client secret,
certificate, managed identity, interactive/developer, `DefaultAzureCredential`, and
occasionally fully custom token acquisition (custom STS, pre-issued tokens in tests,
sovereign-cloud specials). The legacy ServiceClient's connection-string auth model is one of
the pain points this SDK replaces (master plan §1).

`Azure.Core.TokenCredential` (Azure.Identity) is the .NET ecosystem standard for exactly this
set of flows. However, taking it as *the* abstraction would put an Azure dependency into
`Koras.Dataverse.Abstractions`, which must remain dependency-free (master plan §2), and would
couple every alternative transport or test double to Azure.Core.

## Decision

We will define **`IDataverseTokenProvider`** in `Koras.Dataverse.Abstractions`
(namespace `Koras.Dataverse.Authentication`) as the SDK's authentication seam: a small,
dependency-free interface that returns a bearer token (with expiry) for a scope, honoring
cancellation.

The **default implementation**, shipped in `Koras.Dataverse`, adapts an
`Azure.Core.TokenCredential` (Azure.Identity dependency lives only there) with:

- scope `{environmentUrl}/.default`;
- token caching until 5 minutes before expiry;
- thread-safe, single-flight refresh (concurrent callers await one acquisition);
- `TimeProvider`-driven expiry checks for testability.

`DataverseAuthenticationOptions` exposes helpers — `UseClientSecret`, `UseCertificate`,
`UseManagedIdentity`, `UseInteractive`, `UseDefault`, `UseTokenCredential(cred)` — plus
registration of a custom `IDataverseTokenProvider` for everything else (KDV-001).

Tokens are never logged and never appear in exceptions or telemetry (master plan §7).

## Consequences

- `Abstractions` stays at zero dependencies; test doubles and future transports implement one
  tiny interface.
- Consumers get the credential ecosystem they already know (Azure.Identity), including
  environment-driven `DefaultAzureCredential` for dev/prod parity.
- The core package takes the Azure.Identity dependency and its release cadence — accepted per
  the dependency policy assessment ([`../dependency-rules.md`](../dependency-rules.md)).
- `TokenCredential` appears in the core package's options surface (`UseTokenCredential`);
  this deliberate, contained leakage couples that one member to Azure.Core, which is the
  point of the member.
- The AuthenticationHandler sits above the RetryHandler, so retried attempts reuse the cached
  token; a 401 after refresh surfaces as an `Authentication` error rather than triggering
  handler-level auth retry loops. Refinements to 401-triggered refresh behavior are subject
  to implementation review.
- Sovereign clouds are handled via `EnvironmentUrl`-derived scopes and standard credential
  authority configuration; exotic cases fall back to the custom provider seam
  (master plan §8, risk table).

## Alternatives considered

- **`TokenCredential` as the abstraction itself.** Rejected: puts Azure.Core into
  Abstractions, breaking the zero-dependency contract and coupling all mocks and transports
  to Azure types.
- **Connection strings (ServiceClient style).** Rejected: encourages secrets in config
  strings, poor DI/testing ergonomics — an explicit anti-goal (master plan §1, §7).
- **Own credential implementations (raw MSAL or manual OAuth).** Rejected: large
  security-sensitive surface to build and maintain; Azure.Identity already does it well and
  is what consumers expect.
- **Delegate-based auth (`Func<CancellationToken, Task<string>>`).** Rejected: no expiry
  contract, no place for caching semantics, harder to document and mock consistently; the
  interface expresses the contract better and remains trivially implementable.
