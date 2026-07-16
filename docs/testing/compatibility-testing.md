# Compatibility Testing

> Planning document. Covers target-framework compatibility, package-consumption testing,
> and public API surface control. Consistent with
> [master plan §2 (TFM strategy / ADR-0002)](../planning/master-plan.md#2-packages-and-boundaries)
> and [§4 (compatibility strategy)](../planning/master-plan.md#4-public-api-direction-summary).

## 1. TFM matrix

| Package | net8.0 | net9.0 | net10.0 | netstandard2.0 |
|---|---|---|---|---|
| `Koras.Dataverse.Abstractions` | ✔ | ✔ | ✔ | — |
| `Koras.Dataverse.FetchXml` | ✔ | ✔ | ✔ | ✔ |
| `Koras.Dataverse` | ✔ | ✔ | ✔ | — |
| `Koras.Dataverse.OpenTelemetry` | ✔ | ✔ | ✔ | — |

Notes:

- net8.0 is the LTS floor; net10.0 is the current LTS. TFMs are dropped only in major
  versions after the runtime reaches end of life
  ([versioning.md](../release/versioning.md)).
- `Koras.Dataverse.FetchXml` targets netstandard2.0 specifically so the builder is usable
  from Dataverse plugin assemblies (.NET Framework 4.6.2+). The netstandard2.0 build must
  therefore avoid APIs unavailable there (no `Span`-first-only APIs without fallback, no
  nullable-attribute dependencies beyond what the compiler polyfills, `#if` kept minimal).
- Microsoft.Extensions.* dependencies in `Koras.Dataverse` are pinned per-TFM (8.0.x for
  net8.0, 9.0.x for net9.0, 10.0.x for net10.0) so net8.0 consumers are not dragged
  forward. Compatibility tests verify the produced `.nuspec` dependency groups reflect
  this exactly.

### Test execution matrix

Unit/component tests run on **all** TFMs of the packages under test (test projects
multi-target net8.0/net9.0/net10.0). FetchXml's netstandard2.0 build is validated two
ways: (a) it compiles as part of the normal build; (b) it is *consumed* by a
.NET Framework 4.6.2 (Windows CI leg) and a net8.0 consumer in the package-consumption
suite, proving the ns2.0 asset actually loads and runs — compiling alone is not enough.

CI runs the full test matrix on Linux and Windows (path handling, culture defaults,
crypto stack differences); macOS is not a gate.

## 2. Package-consumption test

Project: `tests/Koras.Dataverse.PackageTests`. This is the "eat the actual .nupkg" layer —
it catches packaging mistakes ProjectReference-based tests can never see (missing assets,
wrong dependency groups, README/icon omissions, analyzer leaks, missing XML docs).

Pipeline (scripted, runs in PR CI on Linux, plus a Windows/.NET Framework leg for the
FetchXml ns2.0 consumer):

1. `dotnet pack` all shipped projects with a CI-unique prerelease version
   (e.g., `0.1.0-ci.{run-number}`) into `artifacts/packages/`.
2. Stand up a **local feed** (folder feed via a generated `nuget.config` that lists only
   the local folder plus nuget.org for transitive deps, with package source mapping so
   `Koras.*` can only come from the local feed).
3. For each consumer scenario, restore, build, and **run**:

| Consumer project | TFM | References | Verifies |
|---|---|---|---|
| `Consumer.Net8` | net8.0 | `Koras.Dataverse` | resolves 8.0.x Microsoft.Extensions group; `AddDataverse` + client compile and execute against an in-process fake handler |
| `Consumer.Net9` | net9.0 | `Koras.Dataverse` | 9.0.x dependency group |
| `Consumer.Net10` | net10.0 | `Koras.Dataverse` | 10.0.x dependency group |
| `Consumer.FetchXml.NetFx` | net462 (Windows leg) | `Koras.Dataverse.FetchXml` | ns2.0 asset selected; builder produces expected XML at run time |
| `Consumer.FetchXml.Ns20Lib` | netstandard2.0 class library | `Koras.Dataverse.FetchXml` | ns2.0 surface compiles for library authors |
| `Consumer.OpenTelemetry` | net8.0 | `Koras.Dataverse.OpenTelemetry` | OTel helper extensions wire up; core has no OTel dependency (asserted from the resolved graph) |

4. Each console consumer runs a smoke `Main`: build an `ODataQuery` and a FetchXML query,
   register `AddDataverse` with a fake primary handler, execute one fake CRUD round trip,
   and exit 0. Nonzero exit fails CI.
5. Metadata assertions on the packed output (per master plan §6 "pack output validated"):
   README.md embedded, icon embedded, license expression `MIT`, symbols package (`.snupkg`)
   produced, SourceLink metadata present, deterministic build flag set, XML documentation
   file included per TFM, no unintended dependencies in any group.

## 3. Public API surface: PublicAPI analyzers

`Microsoft.CodeAnalysis.PublicApiAnalyzers` is enabled on every shipped project:

- `PublicAPI.Shipped.txt` — the frozen, released surface (empty until 0.1.0-preview.1
  ships; populated at each release).
- `PublicAPI.Unshipped.txt` — surface added/changed since the last release.
- Any new/removed/changed public member fails the build unless the txt files are updated,
  which makes every API change an explicit, reviewable diff line in the PR. Reviewers
  treat `PublicAPI.*.txt` changes as API review triggers
  ([definition-of-done.md](../planning/definition-of-done.md)).
- On each release, `Unshipped` content is promoted to `Shipped` as part of the release
  process ([release-process.md](../release/release-process.md)).
- Removals require the documented deprecation path: `[Obsolete]` one minor before removal,
  removal only in a major ([versioning.md](../release/versioning.md)).

Per-TFM differences in public surface are not allowed except where netstandard2.0
genuinely cannot express a member; any such divergence needs an explicit justification
comment and shows up in the per-TFM PublicAPI files.

## 4. Package validation: `EnablePackageValidation`

All shipped projects set `<EnablePackageValidation>true</EnablePackageValidation>` from
milestone 0:

- **Pre-1.0:** validates intra-package consistency — every TFM in the package exposes a
  compatible surface (catches "works on net9.0, missing member on net8.0" and ns2.0
  drift in FetchXml) at `dotnet pack` time in every CI run.
- **From 1.0:** add `<PackageValidationBaselineVersion>` pinned to the latest released
  stable version, so pack fails on any binary/source breaking change against the
  baseline. The baseline is bumped as part of each release. Suppressions
  (`CompatibilitySuppressions.xml`) are only acceptable with an ADR-linked justification
  and are audited at every release ([release-checklist.md](../release/release-checklist.md)).
- PublicAPI analyzers and package validation are complementary and both required: the
  analyzer reviews *source-level intent* per PR; package validation checks the *packed
  binary reality* per TFM.

## 5. Runtime compatibility rules under test

- **Roll-forward reality check:** the net8.0 asset is what a net8.0 app gets; consumers on
  net9.0/net10.0 get their exact TFM asset. The consumption matrix (§2) proves each asset
  works on its own runtime — no reliance on "highest asset probably works".
- **No breaking dependency floors within a major:** raising a Microsoft.Extensions or
  Azure.Identity minimum version within a minor release requires justification (security
  fix) and a changelog entry; the consumption tests pin the expected floors so an
  accidental bump fails CI.
- **Trimming/AOT posture (documented honesty):** the MVP does not claim trimming or
  Native AOT compatibility. `IsTrimmable`/AOT analyzers may be enabled exploratively, but
  no compatibility promise is made until a release explicitly declares it; nothing in the
  docs may imply otherwise.
