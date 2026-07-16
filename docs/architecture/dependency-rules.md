# Dependency Rules

> Elaborates §2 and §5 of [`docs/planning/master-plan.md`](../planning/master-plan.md). If this
> document and the master plan disagree, the master plan wins.

## 1. Dependency direction rules

The dependency graph is a strict DAG pointing from implementation toward contracts:

```text
Koras.Dataverse.OpenTelemetry ──► Koras.Dataverse ──► Koras.Dataverse.Abstractions
                                        │
                                        └───────────► Koras.Dataverse.FetchXml

Koras.Dataverse.OrganizationService (v1.1) ────────► Koras.Dataverse.Abstractions
```

Rules:

1. **Contracts at the bottom.** `Koras.Dataverse.Abstractions` and `Koras.Dataverse.FetchXml`
   depend on nothing (not even on each other).
2. **Implementations depend on contracts, never the reverse.** `Koras.Dataverse` references
   `Abstractions` and `FetchXml`. No package ever references `Koras.Dataverse` except
   `Koras.Dataverse.OpenTelemetry`, which references it only for the instrumentation name
   constants (ADR-0008).
3. **No cycles, no lateral references.** Sibling implementation packages
   (`OrganizationService`, future feature packages) reference `Abstractions` only, never each
   other and never the core package.
4. **Consumers choose their depth.** Application composition roots reference `Koras.Dataverse`
   (and optionally `Koras.Dataverse.OpenTelemetry`); domain/service libraries and test projects
   should reference only `Abstractions`; plugin projects may reference only `FetchXml`.
5. **Namespace direction mirrors package direction.** Code in `Koras.Dataverse.*`
   implementation namespaces may use `Abstractions` types freely; nothing in the
   `Abstractions` assembly may name an implementation namespace.

## 2. Third-party dependency policy

Default answer: **no**. Every dependency the SDK takes is imposed transitively on every
consumer, with its own release cadence, vulnerability surface, and version-conflict potential.
The MVP dependency set is deliberately closed:

| Package | Where | Why it clears the bar |
|---|---|---|
| Azure.Identity | `Koras.Dataverse` | The de-facto standard for Entra ID auth in .NET; writing credential flows by hand would be a security liability (ADR-0004) |
| Microsoft.Extensions.{Http, Options, Options.DataAnnotations, Logging.Abstractions, DependencyInjection.Abstractions, Diagnostics.HealthChecks.Abstractions} | `Koras.Dataverse` | Platform-level abstractions required for the DI-native positioning; pinned per-TFM (ADR-0002/0009) |
| OpenTelemetry.Api | `Koras.Dataverse.OpenTelemetry` | The whole point of the helper package; isolated so the core stays OTel-free (ADR-0008) |
| Microsoft.PowerPlatform.Dataverse.Client | `Koras.Dataverse.OrganizationService` (v1.1) | The reason that package exists; quarantined there (ADR-0001) |

Explicitly rejected so far: Polly / Microsoft.Extensions.Http.Resilience (ADR-0007),
FluentAssertions ≥ v8 for tests (licensing — master plan §6), any JSON library beyond the
in-box `System.Text.Json`.

### Required assessment for adding a dependency

A PR that adds any package reference (including to test and build tooling for shipped
packages) must include this assessment in its description, and the reviewer must check each
point:

1. **Need.** What concrete, user-visible capability requires it? Why can it not be implemented
   in a reasonable amount of first-party code?
2. **Alternatives.** What in-box BCL/Microsoft.Extensions option exists? What would a minimal
   in-house implementation cost? Which competing packages were considered and why were they
   rejected?
3. **License.** Must be MIT-compatible for redistribution as a dependency of an MIT library
   (MIT, Apache-2.0, BSD-family). Copyleft or commercial-tier licenses are disqualifying.
4. **Maintenance.** Release history, responsiveness to issues, bus factor, ownership (prefer
   foundation/vendor-backed). Would we be able to fork or replace it if abandoned?
5. **Security.** Known CVE history, dependency depth it brings along, whether it performs
   dynamic code loading/reflection-heavy behavior that affects trimming and AOT analysis.
6. **Size.** Package size and transitive closure added to a consumer's output; impact on
   restore graph and version conflict likelihood.
7. **Public-API leakage.** Does any type from the dependency appear in our public signatures?
   Leakage couples our SemVer to theirs and is only acceptable when the type *is* the contract
   (e.g., `TokenCredential` in the core package's auth options, ADR-0004). Leakage into
   `Abstractions` or `FetchXml` is never acceptable.

A new dependency in `Abstractions` or `FetchXml` cannot be approved by this checklist at all;
it requires a superseding ADR, because "zero dependencies" is itself a published contract of
those packages.

## 3. Version pinning and central management

- All package versions are managed centrally via `Directory.Packages.props` (CPM, ADR-0009);
  no `Version=` attributes in individual project files.
- Microsoft.Extensions.* versions are conditioned per target framework: 8.0.x on net8.0, 9.0.x
  on net9.0, 10.0.x on net10.0, so net8.0 consumers are never forced onto newer runtime
  package bands (ADR-0002).
- Floating versions (`*`, ranges) are not used. Dependabot proposes upgrades; CI runs
  `dotnet list package --vulnerable` (master plan §7).

## 4. Rules enforced by architecture tests

`tests/Koras.Dataverse.ArchitectureTests` (NetArchTest, master plan §6) encodes the rules above
so violations fail CI rather than waiting for review:

1. `Koras.Dataverse.Abstractions` has no references to any other SDK assembly and no
   third-party assembly references.
2. `Koras.Dataverse.FetchXml` has no references to any other SDK assembly and no third-party
   assembly references.
3. No assembly except `Koras.Dataverse.OpenTelemetry` references `Koras.Dataverse`.
4. Types in the `Abstractions` assembly do not reference types from `System.Net.Http`.
5. No type in `Koras.Dataverse.Abstractions` or `Koras.Dataverse.FetchXml` public surface
   exposes a type from a foreign assembly other than the BCL.
6. Namespace mapping: public types live in the namespaces assigned to them by master plan §4
   (the assignment is per type, not strictly per package — e.g., `Koras.Dataverse`-namespace
   contracts ship in the Abstractions assembly).
7. Public types are `sealed` or `abstract` (sealed-by-default rule, master plan §4), with an
   explicit allowlist for deliberate exceptions.
8. Public async methods returning `Task`/`Task<T>`/`ValueTask`/`ValueTask<T>`/
   `IAsyncEnumerable<T>` carry the `Async` suffix (except `IAsyncEnumerable`-returning query
   composition methods explicitly allowlisted, e.g. patterns like `QueryAllAsync` keep the
   suffix; see [`../api/naming-guidelines.md`](../api/naming-guidelines.md)).
9. No public static members outside approved builder entry points (`FetchXml.For`,
   `ODataQuery.For`, DI extension classes).

The exact test names and allowlist mechanics are subject to implementation review; the rule
set itself is the contract.
