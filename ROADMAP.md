# Roadmap

The authoritative, feature-level roadmap lives in
[docs/product/product-roadmap.md](docs/product/product-roadmap.md) and
[docs/features/future-roadmap.md](docs/features/future-roadmap.md). Summary:

| Release | Focus |
|---|---|
| **0.1.0-preview.1** | MVP feature-complete preview (KDV-001…KDV-012): auth, CRUD, OData + FetchXML with paging, batch, metadata, solutions, resilience, DI, health checks, telemetry. API feedback window. |
| **0.1.0** | Stabilized MVP after preview feedback; docs complete; NuGet.org publication. |
| **0.5.0** | Impersonation (KDV-013), file/image columns (KDV-014), `Koras.Dataverse.OrganizationService` optional transport (KDV-015). |
| **1.0.0** | Public API freeze, PublicAPI analyzers enforced as errors, package signing, LTS support policy in effect. |
| **1.1 / 1.2** | Source-generated early-bound models (KDV-016), strongly typed fluent queries (KDV-017), metadata snapshots & environment comparison (KDV-018). |
| **2.0** | Solution dependency analysis (KDV-019), Power Pages helpers (KDV-020), ALM pipeline helpers (KDV-021), plugin development helpers (KDV-022). |

Out of scope (permanently, unless the community changes our minds with a great RFC): UI
components, data-migration engines, plugin execution runtime, on-premises (<9.x), Power
Automate connector runtimes.
