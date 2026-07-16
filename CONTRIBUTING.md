# Contributing to the Koras Dataverse SDK

Thank you for considering a contribution! This project is MIT-licensed and welcomes issues,
docs improvements, bug fixes, and features aligned with the roadmap.

## Before you start

- Check the [roadmap](ROADMAP.md) and [feature catalog](docs/features/feature-catalog.md); open
  an issue to discuss substantial changes before writing code.
- Out-of-scope items (UI, migration engines, plugin runtimes, on-prem <9.x) will be declined.

## Development setup

1. Install the .NET 10 SDK (`global.json` pins the version; `rollForward: latestFeature`).
2. `dotnet restore Koras.Dataverse.slnx`
3. `dotnet build Koras.Dataverse.slnx -c Release` — the build treats warnings as errors.
4. `dotnet test Koras.Dataverse.slnx -c Release` — integration tests skip without
   `KORAS_DATAVERSE_*` environment variables (see `docs/testing/integration-testing.md`).

## Pull requests

- One logical change per PR; include tests and docs with the code.
- Follow the engineering rules in [CLAUDE.md](CLAUDE.md) (they apply to humans too) and the
  [public API checklist](docs/api/public-api-review-checklist.md) for surface changes.
- Update `CHANGELOG.md` under `[Unreleased]`.
- New dependencies require the assessment in `docs/architecture/dependency-rules.md`.
- CI must be green: build, tests, format (`dotnet format --verify-no-changes`), architecture
  tests, package validation.

## Commit style

Conventional-style subjects: `feat:`, `fix:`, `docs:`, `test:`, `build:`, `perf:`, `refactor:`.

## Code of conduct

This project follows the [Contributor Covenant](CODE_OF_CONDUCT.md). Report unacceptable
behavior to opensource@korastech.com.

## Security issues

Never open a public issue for a vulnerability — see [SECURITY.md](SECURITY.md).
