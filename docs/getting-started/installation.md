# Installation

> The SDK is in preview and not yet published to NuGet.org. The commands below show the
> intended install experience; until first publication, consume the packages from a local feed
> produced by `dotnet pack`.

## Install the main package

Most applications need only the main package. It brings in `Koras.Dataverse.Abstractions` and
`Koras.Dataverse.FetchXml` transitively.

```bash
dotnet add package Koras.Dataverse
```

Or with a `PackageReference`:

```xml
<ItemGroup>
  <PackageReference Include="Koras.Dataverse" Version="0.1.0-preview.1" />
</ItemGroup>
```

## Target framework support

| Package | net8.0 | net9.0 | net10.0 | netstandard2.0 |
|---|---|---|---|---|
| `Koras.Dataverse` | ✔ | ✔ | ✔ | — |
| `Koras.Dataverse.Abstractions` | ✔ | ✔ | ✔ | — |
| `Koras.Dataverse.FetchXml` | ✔ | ✔ | ✔ | ✔ |
| `Koras.Dataverse.OpenTelemetry` | ✔ | ✔ | ✔ | — |

net8.0 is the LTS floor. `Koras.Dataverse.FetchXml` additionally targets netstandard2.0 so the
FetchXML builder can be used from Dataverse plugin assemblies (.NET Framework 4.6.2+).

On-premises Dataverse/Dynamics (pre-9.x, WCF Organization Service) is not supported.

## Which package to reference

| Your project | Reference |
|---|---|
| Application that calls Dataverse (web app, worker, console) | `Koras.Dataverse` |
| Class library that *uses* `IDataverseClient` but should not carry the implementation | `Koras.Dataverse.Abstractions` |
| Unit-test project mocking `IDataverseClient` | `Koras.Dataverse.Abstractions` |
| Dataverse plugin or other netstandard2.0 code that only builds FetchXML | `Koras.Dataverse.FetchXml` |
| Application wiring the SDK into OpenTelemetry | `Koras.Dataverse.OpenTelemetry` (in addition to `Koras.Dataverse`) |

The dependency direction is one-way: `Koras.Dataverse` depends on `Abstractions` and `FetchXml`;
nothing depends on the implementation package. Libraries programmed against
`Koras.Dataverse.Abstractions` stay free of HTTP and Azure.Identity dependencies.

## Next steps

- [Quick start](quick-start.md) — first query in five minutes
- [Your first application](first-application.md) — a complete console walkthrough
