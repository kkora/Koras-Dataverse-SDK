# Package consumption tests

Consumer projects that restore the **packed** `Koras.Dataverse.*` packages from the local
pack output (`artifacts/packages`, see [nuget.config](nuget.config)) and compile/run the
way a real consumer would — plain SDK defaults, no repository build props, no central
package management.

| Project | Proves |
|---|---|
| `Consumer.FetchXml.Net48` | The `netstandard2.0` asset of `Koras.Dataverse.FetchXml` works from .NET Framework 4.8 with C# 7.3 (Dataverse plug-in scenario). Compiles cross-platform via reference assemblies. |
| `Consumer.Core` | `Koras.Dataverse` + `Koras.Dataverse.OpenTelemetry` and their dependency graph restore, compile, and boot DI on net8.0 and net10.0. |

## Running locally

```bash
dotnet pack Koras.Dataverse.slnx --configuration Release --output artifacts/packages
dotnet build tests/Koras.Dataverse.PackageTests/Consumer.FetchXml.Net48
dotnet run --project tests/Koras.Dataverse.PackageTests/Consumer.Core --framework net10.0
```

`KorasPackageVersion` defaults to the floating `0.1.0-*` (highest version in the local
feed); CI pins it to the exact packed version. These projects are intentionally **not**
part of `Koras.Dataverse.slnx` — they only restore after a pack. CI runs them in the
`package.yml` workflow.
