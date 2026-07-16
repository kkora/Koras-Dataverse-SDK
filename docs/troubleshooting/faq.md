# FAQ

Honest answers to the questions we get most.

### Is there early-bound (generated, strongly typed) entity support?

Not yet as code generation. Today you get attribute-based POCO mapping —
`[DataverseTable]`/`[DataverseColumn]` with `EntityMapper.ToEntity` and `entity.ToObject<T>()` —
which covers typed reads/writes without tooling. Roslyn **source-generated** early-bound models
from a metadata snapshot are planned (v1.2 on the roadmap), with LINQ-style typed queries
building on them.

### Does the SDK support IOrganizationService / the Organization Service?

No. The core SDK is Web API-only by design. An optional `Koras.Dataverse.OrganizationService`
transport package (adapting `Microsoft.PowerPlatform.Dataverse.Client`) is planned for v1.1
for organizations that need `IOrganizationService` semantics — it will be a separate, heavier
package so the core stays lean.

### Does it work with on-premises Dynamics/Dataverse?

No, and there are no plans for it. On-premises (pre-9.x, WCF-based) is explicitly out of
scope. The SDK targets cloud Dataverse environments over the Web API with Entra ID
authentication.

### Can I use it inside Dataverse plugins?

Only the FetchXML builder. `Koras.Dataverse.FetchXml` targets netstandard2.0 precisely so
plugin assemblies can build injection-safe FetchXML. The client itself (HTTP, Azure.Identity)
is not designed for the plugin sandbox — inside plugins, execute queries through the
`IOrganizationService` the runtime gives you.

### Why plain CLR values instead of OptionSetValue/Money like the official SDK?

Because the wrappers are ceremony the Web API doesn't have: a choice is an `int`, money is a
`decimal` on the wire. Plain values make entities trivially constructible in tests and
readable in code. Display labels are still available via `Entity.FormattedValues`, and lookups
keep a dedicated type (`EntityReference`) because they genuinely carry two pieces of data.

### How do I get choice labels then?

Two ways: per row via `entity.FormattedValues["column"]` (populated when annotations are on,
which is the default), or per column via metadata:
`await client.Metadata.GetChoicesAsync(table, column, ct)` for all options with values, labels,
and colors.

### Is the client thread-safe? What lifetime should it have?

The client is thread-safe and intended to be a singleton per environment — that is exactly
what `AddDataverse` registers. Builders (`ODataQuery`, FetchXML builders, `BatchRequest`) and
`Entity` instances are not thread-safe; build/use them per operation. See
[thread safety](../concepts/thread-safety.md).

### Does the SDK handle Dataverse throttling for me?

Mostly. HTTP 429 (and 502/503/504 and network errors) are retried with `Retry-After` honored,
up to `Retry.MaxRetries` within the operation `Timeout`. What it deliberately does not do is
reshape your workload — if you sustainably exceed service-protection limits you'll still see
`Throttling` exceptions and should reduce parallelism or batch. See
[provider errors](provider-errors.md).

### How do I authenticate without any secret?

`UseManagedIdentity()` in Azure hosting, or `UseDefault()` which finds managed identity,
workload identity, or your local `az login` automatically. The identity must be registered as
an application user in the Dataverse environment. For everything else there is
`UseCertificate`, and `UseTokenCredential`/`UseTokenProvider` as escape hatches.

### Why do I get 404 for a table that exists?

Almost always the entity set name: the client pluralizes logical names by Dataverse's standard
rules, and a few customized tables deviate. Fix with
`options.EntitySetNameOverrides["logicalname"] = "actualsetname"`; discover the actual name via
`client.Metadata.GetEntitySetNameAsync`. See [common errors](common-errors.md).

### Can I run multiple environments (prod/UAT/dev) from one application?

Yes — one `AddDataverse("name", …)` per environment, resolved via
`IDataverseClientFactory.GetClient("name")`. Each named client has independent options, token
cache, and HTTP pipeline. See [DI patterns](../guides/dependency-injection.md).

### How do I mock this in tests?

Everything is interface-first: substitute `IDataverseClient` (NSubstitute/Moq) for unit tests,
or run a real `DataverseClient` over a fake `HttpMessageHandler` plus a fake
`IDataverseTokenProvider` for integration-style tests — the same approach the SDK's own test
suite uses. See the [testing guide](../guides/testing.md) and
[testing recipes](../recipes/testing-recipes.md).

### Is there impersonation (act on behalf of another user)?

Not in the current preview. Impersonation via `CallerObjectId` (per client and per request) is
planned for v1.1.

### What about file/image columns?

Chunked file and image column upload/download is planned for v1.1. In the current preview,
small binary values can be written as `byte[]` attribute values where the column type accepts
base64 content, but there is no dedicated streaming API yet.

### Is the API stable?

It is a 0.x preview: breaking changes may still occur between minor versions, and are called
out in the changelog. From 1.0 the public API freezes under SemVer discipline with tracked
public-API files and a deprecation window. See the
[versioning policy](../migration/versioning-policy.md).

### Which .NET versions are supported?

net8.0, net9.0, and net10.0 for all packages; `Koras.Dataverse.FetchXml` additionally targets
netstandard2.0. There is no .NET Framework support for the client packages.
