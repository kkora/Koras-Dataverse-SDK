# Integration Testing (Live Dataverse Environment)

> Planning document, consistent with [master plan §6](../planning/master-plan.md). The
> integration suite validates the assumptions the unit/component tests encode: that our
> wire contract, error taxonomy, throttling handling, and auth flow match a **real**
> Dataverse environment. It is opt-in, skipped by default, and safe to run in CI without
> configuration (everything skips cleanly).

## 1. Scope

Project: `tests/Koras.Dataverse.IntegrationTests`.

Covered against a live environment (per master plan §6): CRUD round-trip, OData query +
`IAsyncEnumerable` paging, FetchXML execution + paging cookies, `$batch` (atomic and
continue-on-error), metadata reads, solution queries, WhoAmI/health probe, authentication
(client secret flow), and observation of real error payloads (e.g., not-found, invalid
query) to confirm the error mapper's assumptions.

Explicitly **not** covered live: destructive org-level operations (solution import/publish
against shared environments is gated behind an additional opt-in variable, see §6),
deliberate throttling induction (see §7), and anything requiring admin-level privileges
beyond the test application user's roles.

## 2. Configuration via environment variables

| Variable | Meaning | Required |
|---|---|---|
| `KORAS_DATAVERSE_URL` | Environment URL, e.g. `https://korastest.crm.dynamics.com` (HTTPS only) | Yes |
| `KORAS_DATAVERSE_TENANT_ID` | Entra ID tenant (directory) id | Yes |
| `KORAS_DATAVERSE_CLIENT_ID` | App registration (application/client) id | Yes |
| `KORAS_DATAVERSE_CLIENT_SECRET` | Client secret for the app registration | Yes |

Rules:

- If **any** required variable is missing, the entire suite is skipped (not failed) with a
  single clear skip reason. Partial configuration is treated as unconfigured.
- No fallback to files, connection strings, or hard-coded defaults. Local developers may
  use their shell profile or a git-ignored launch profile; secrets never enter the repo
  (see [secure-configuration.md](../security/secure-configuration.md)).
- The tests build configuration exclusively through the public API
  (`AddDataverse` + `UseClientSecret(tenantId, clientId, secret)`), so the integration
  suite doubles as an end-to-end check of the documented registration path.

The credential must be a dedicated **application user** in a dedicated **test
environment**, holding a least-privilege custom security role scoped to the test tables —
never a production environment, never System Administrator (see
[secure-configuration.md](../security/secure-configuration.md)).

## 3. Skippable-by-default xUnit pattern

Standard `[Fact]`/`[Theory]` cannot skip dynamically at run time in a clean way, so the
suite uses a small project-owned attribute pair:

- `LiveFactAttribute` / `LiveTheoryAttribute`: derive from `FactAttribute` /
  `TheoryAttribute`; the constructor checks the four environment variables and, when any
  is absent, sets the skip reason to
  `"Live Dataverse environment not configured (KORAS_DATAVERSE_URL et al.)."`.
- A shared `DataverseFixture` (xUnit collection fixture) builds one `ServiceProvider` via
  `AddDataverse`, resolves the singleton `IDataverseClient`/`IMetadataClient`/
  `ISolutionClient` once, verifies connectivity with a single WhoAmI call, and exposes the
  run-unique prefix (§4). All live tests join this collection so the suite makes exactly
  one auth handshake and shares one HTTP pipeline — mirroring production usage and
  limiting request volume.

This keeps the default developer experience exact: `dotnet test` with no configuration
runs everything else and reports the live tests as skipped, never failed.

## 4. Data isolation: create/cleanup per run with unique prefixes

- Each run generates a **run id**: `kdvit{yyyyMMddHHmmss}{4-char random}` (lowercase,
  collision-safe, sortable).
- Every row the suite creates carries the run id as a prefix in its primary name column
  (e.g., account name `kdvit20260716103015ab3f-paging-007`). Tests only ever query,
  assert on, and delete rows matching their own run prefix — concurrent runs (two
  developers, or CI plus a developer) cannot interfere.
- Tests use standard tables (`account`, `contact`) with no custom schema requirement for
  the MVP suite, so any trial/developer environment works out of the box.
- **Cleanup:**
  - Each test class deletes what it created in the fixture's async disposal, using
    `$batch` deletes keyed on the run prefix.
  - A final **sweeper** step in the fixture disposal queries for any leftover rows with
    the `kdvit` prefix older than 24 hours and deletes them — self-healing against
    crashed previous runs.
  - Cleanup failures are logged loudly but do not fail the run (the sweeper catches
    stragglers next time).
- No test ever modifies or deletes rows it did not create; no test truncates tables.

## 5. Throttling etiquette

The suite is a guest in a shared service and must never trip Dataverse service protection
limits (~6000 requests / 5 minutes / user, 20 concurrent — see
[performance-guide.md](../performance/performance-guide.md)):

- **Budget:** the whole suite targets well under 500 requests per run; batch operations
  are used wherever multiple rows are needed.
- **Concurrency cap:** live tests run in a single xUnit collection (serialized); no test
  spawns more than 4 concurrent requests, and only in the tests that specifically verify
  concurrent client safety.
- **No throttling-induction tests.** We do not hammer the service to provoke a real 429.
  Retry/throttling behavior is fully covered by unit tests with fake handlers; the live
  suite merely honors `Retry-After` if the service happens to throttle (which is itself a
  passive validation of KDV-008).
- Page sizes are kept small (e.g., page size 5 across 3 pages) to test paging mechanics
  with minimal data volume.
- If the suite receives a 429, the SDK's own retry policy handles it; tests do not add
  competing retry loops.

## 6. What runs in CI vs. locally

| Context | What runs | Credentials |
|---|---|---|
| PR CI (default) | Nothing live — all `LiveFact` tests skip. PRs from forks never see secrets. | none |
| Nightly scheduled workflow + manual `workflow_dispatch` | Full live suite against the dedicated test environment | GitHub Actions repository secrets (`KORAS_DATAVERSE_*`) exposed only to a protected environment, only on non-fork refs |
| Release pipeline | Full live suite is a required gate before publish (see [release-process.md](../release/release-process.md)) | same protected environment |
| Local (maintainers) | Full suite via `dotnet test tests/Koras.Dataverse.IntegrationTests` with env vars set | developer-held secret for the shared test app user, or a personal dev environment |
| Local (external contributors) | Suite skips unless they point it at **their own** dev environment | their own |

Additional opt-in gate: solution import/publish tests (KDV-007 write paths) also require
`KORAS_DATAVERSE_ALLOW_SOLUTION_TESTS=true`, because they mutate org-level state and are
slow (async job polling). Nightly CI sets it; the default local run does not. This
variable gates test selection only — it is not SDK configuration and adds no API surface.

## 7. Determinism and flake policy

- Live tests assert **contract**, not timing: no assertions on wall-clock latency, no
  sleeps except documented Dataverse consistency waits (and those use bounded polling
  with the SDK's own paging/retry rather than fixed delays).
- A live test that fails intermittently is quarantined (trait `Category=Quarantine`,
  excluded from the nightly gate) within one working day and tracked with an issue; the
  quarantine list must be empty before any release (see
  [release-checklist.md](../release/release-checklist.md)).
- Live failures never block PR merges (they cannot — PRs don't run them); they block
  releases and page the maintainers via the nightly workflow's failure notification.

## 8. Diagnostics

- The suite enables the SDK's `ILogger` output at `Debug` to the test console **with the
  same redaction guarantees as production** — the logging-redaction unit tests are the
  proof; the live suite must not add ad-hoc logging of raw requests.
- Each failure message includes the Dataverse request id from `DataverseError` so service-
  side correlation is possible without logging payloads.
