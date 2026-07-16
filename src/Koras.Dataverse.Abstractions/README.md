# Koras.Dataverse.Abstractions

Interfaces and models for the [Koras Dataverse SDK](https://github.com/korastechnologies/koras-dataverse):
`IDataverseClient`, `Entity`, `EntityReference`, OData query builders, batch models, metadata and
solution models, and the normalized `DataverseError` model.

Reference this package from application and library code that consumes `IDataverseClient` but
should not depend on the SDK implementation (clean architecture, test doubles, shared contracts).
The implementation lives in the `Koras.Dataverse` package.

- **License:** MIT · **Publisher:** Koras Technologies
