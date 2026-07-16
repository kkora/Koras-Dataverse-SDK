# CLAUDE.md — Koras Dataverse SDK

Guidance for AI-assisted development in this repository. Humans: this is also a good summary of
our engineering rules.

## 1. Mission

A modern, fluent, resilient .NET SDK for Microsoft Dataverse (Web API first): auth, CRUD,
OData/FetchXML queries with paging, batching, metadata, solutions, resilience, DI, health
checks, and OpenTelemetry-ready diagnostics. Canonical plan: `docs/planning/master-plan.md`.

## 2. Scope

In scope: everything in the MVP feature list (KDV-001…KDV-012) and the roadmap in
`docs/features/`. Out of scope: UI, migration engines, plugin execution runtimes, on-premises
(<9.x), Power Automate connectors. Do not add out-of-scope features.

## 3. Architecture rules

- Dependency direction: `Koras.Dataverse` → `Koras.Dataverse.Abstractions` → `Koras.Dataverse.FetchXml`. Never reversed.
- `Koras.Dataverse.Abstractions` and `Koras.Dataverse.FetchXml` must have **zero** third-party dependencies.
- `Koras.Dataverse.FetchXml` must stay netstandard2.0-compatible (no modern-BCL-only APIs without `#if`).
- All Web API calls funnel through `DataverseClient.SendAsync` (timeout, telemetry, error normalization).
- Errors: always throw `DataverseException` carrying a `DataverseError`; never leak raw `HttpRequestException` from public APIs (except `OperationCanceledException` for cancellation, never wrapped).
- Architecture tests in `tests/Koras.Dataverse.ArchitectureTests` enforce these — keep them green.

## 4. Coding conventions

- Modern C#: file-scoped namespaces, nullable enabled, `async`/`await` with `ConfigureAwait(false)` in library code, `sealed` by default.
- No `.Result`, `.Wait()`, `Thread.Sleep`, global mutable state, swallowed exceptions, unbounded retries.
- Use `TimeProvider` for anything time-dependent; never `DateTime.Now` in library code.
- Every public member needs XML docs (build fails otherwise: warnings are errors).
- Logging: categories `Koras.Dataverse` / `Koras.Dataverse.Http`; never log tokens, secrets, or attribute values at Information or above.

## 5. Naming

- `Async` suffix on all async methods; `tableName` for logical names; `Options` suffix for options types; builder verbs `For/Select/Where/OrderBy/Top/Build`. Details: `docs/api/naming-guidelines.md`.

## 6. Dependencies

Adding ANY dependency requires the assessment in `docs/architecture/dependency-rules.md`
(need, alternatives, license, maintenance, security, size, public-API leakage) recorded in the
PR description. Core package allows only Azure.Identity/Azure.Core + Microsoft.Extensions.*.

## 7. Public API rules

- Design doc first: `docs/api/public-api-design.md`; checklist: `docs/api/public-api-review-checklist.md`.
- No third-party types in `Abstractions`. No breaking changes outside a major version after 1.0.
- PublicAPI analyzers are installed (relaxed to suggestions until 1.0 — ADR-0010).

## 8. Tests

- Every feature: happy path, invalid input, boundary, cancellation, failure path, and (where relevant) thread safety.
- Unit tests use the fake `HttpMessageHandler`/`TimeProvider` in `tests/Koras.Dataverse.UnitTests/TestInfrastructure`.
- Integration tests must skip cleanly without `KORAS_DATAVERSE_*` env vars.
- xUnit built-in assertions only (no FluentAssertions — v8 licensing).

## 9. Documentation

- User docs live in `docs/`; update the relevant page in the same PR as a behavior change.
- Every new feature needs: feature doc, options reference update, changelog entry under `[Unreleased]`.

## 10. Security

- Never commit secrets, real environment URLs, or tenant ids. Samples read secrets from user-secrets/env vars only.
- Query builders must encode all values; raw escape hatches (`WhereRaw`, `Raw`) must carry doc warnings.
- HTTPS-only environment URLs are enforced in options validation — do not weaken.

## 11. Performance

- No sync-over-async, no full-buffering of pageable results, prefer streaming (`IAsyncEnumerable`).
- Hot paths (builders, serialization) have benchmarks in `benchmarks/`; do not regress allocations knowingly.

## 12–14. Git, PRs, releases

- Branches: `feature/…`, `fix/…`, `docs/…`. Conventional-style commit subjects (`feat:`, `fix:`, `docs:`, `test:`, `build:`).
- PR checklist: build green, tests added/passing, docs updated, changelog updated, API review checklist for public-surface changes, no new analyzer suppressions without justification.
- Releases: tag `v{version}` → `release.yml` publishes. Process: `docs/release/release-process.md`.

## 15. Definition of done

`docs/planning/definition-of-done.md`. Short form: compiles with zero warnings, tests pass on
all TFMs, docs + changelog updated, no TODO/placeholder code, no secrets.

## 16. Commands

```bash
dotnet restore Koras.Dataverse.slnx
dotnet build Koras.Dataverse.slnx --configuration Release --no-restore
dotnet test Koras.Dataverse.slnx --configuration Release --no-build
dotnet pack Koras.Dataverse.slnx --configuration Release --no-build --output artifacts/packages
dotnet format Koras.Dataverse.slnx --verify-no-changes
dotnet list Koras.Dataverse.slnx package --vulnerable --include-transitive
```

## 17. Files not to modify without justification

`LICENSE`, `Directory.Build.props`, `Directory.Packages.props`, `global.json`, `.editorconfig`,
`.github/workflows/release.yml`, anything under `docs/architecture/decision-records/`
(append new ADRs instead of editing accepted ones).

## 18–19. Dependency & breaking-change rules

New dependency → §6 assessment + maintainer approval. Breaking change → new ADR + major version
+ migration notes in `docs/migration/breaking-changes.md`; `[Obsolete]` one minor first.

## 20. Required validation before completing any task

1. `dotnet build` (Release) — zero warnings.
2. `dotnet test` — all green (integration tests may skip).
3. Docs/changelog updated if behavior changed.
4. `git status` — no stray files, no secrets.
