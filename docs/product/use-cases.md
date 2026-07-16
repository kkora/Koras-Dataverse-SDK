# Primary Use Cases — Koras Dataverse SDK

> Feature IDs reference [`../features/feature-catalog.md`](../features/feature-catalog.md) and
> §3 of [`docs/planning/master-plan.md`](../planning/master-plan.md).

## UC-1: Dataverse integration services

**Scenario.** A .NET backend (ASP.NET Core API or worker service) synchronizes business data
between Dataverse and other systems — an ERP pushes invoices in, a commerce platform reads
accounts and contacts out — running continuously in production with real traffic and real
throttling.

**Actors.** Backend developers, integration architects, SRE/operations.

**Flow.**
1. Register the client at startup with `services.AddDataverse(...)` and managed identity or
   client-secret authentication.
2. Inject `IDataverseClient` into application services.
3. Read with OData queries and `IAsyncEnumerable` auto-paging; write with create/update/upsert,
   using alternate keys for idempotent synchronization.
4. Rely on built-in retry/throttling handling during load spikes; surface classified
   `DataverseException` errors to the job's dead-letter or compensation logic.
5. Monitor via logs, traces, and metrics exported through OpenTelemetry; expose a health check
   for the orchestrator.

**SDK features used.** KDV-001 (auth), KDV-002 (CRUD/upsert/alternate keys), KDV-003 (OData
queries + paging), KDV-008 (resilience), KDV-009 (error model), KDV-010 (DI + options), KDV-011
(observability), KDV-012 (health checks).

## UC-2: Metadata automation

**Scenario.** A platform team maintains tooling that inspects Dataverse schema — generating
documentation, validating naming conventions, checking that required columns and choices exist
before a deployment proceeds.

**Actors.** Platform engineers, Power Platform administrators, CI pipelines.

**Flow.**
1. Authenticate with a service principal (client secret or certificate).
2. Use `IMetadataClient` to enumerate tables, columns, relationships, and local/global choices
   as typed lightweight models (`TableMetadata`, `ColumnMetadata`, `RelationshipMetadata`,
   `ChoiceOption`).
3. Evaluate rules against the metadata and emit a report or fail the pipeline.

**SDK features used.** KDV-001, KDV-006 (metadata), KDV-008, KDV-009, KDV-010. Future: KDV-018
(metadata snapshot export + environment comparison, v1.2) extends this use case.

## UC-3: Solution deployment automation

**Scenario.** A DevOps pipeline exports a solution from a development environment, imports it
into test and production environments, waits for the asynchronous import job to finish, and
publishes customizations — without shelling out to external tools.

**Actors.** DevOps engineers, release managers, CI/CD pipelines.

**Flow.**
1. Authenticate per environment (service principal per target).
2. `ISolutionClient` exports the solution from the source environment.
3. Import into the target with `SolutionImportOptions`; the SDK polls the async import job to
   completion with cancellation support.
4. Publish all customizations; query installed solutions to verify the resulting version.
5. Pipeline logs carry request IDs and structured errors for failed imports.

**SDK features used.** KDV-001, KDV-007 (solutions), KDV-008, KDV-009, KDV-010, KDV-011.
Future: KDV-019 (solution dependency analysis, v2.0) and KDV-021 (ALM pipeline helpers, v2.0).

## UC-4: Data migration jobs

**Scenario.** A one-off or recurring job moves large data volumes into or out of Dataverse — an
initial CRM load, a periodic archive extract, a cleanup job. Throughput matters, and service
protection limits are the dominant constraint. (The SDK provides the primitives; migration
orchestration engines are explicitly out of scope.)

**Actors.** Data engineers, consultants running migrations.

**Flow.**
1. Read source data and map it to late-bound `Entity` instances with plain CLR values.
2. Group writes into `$batch` requests with atomic change sets where consistency is required,
   or continue-on-error batches with per-item results (`BatchItemResult`) for bulk loads;
   the 1000-operation guard prevents invalid payloads.
3. Use alternate-key upsert for re-runnable, idempotent loads.
4. Built-in throttling awareness (Retry-After, jittered backoff) keeps the job inside service
   protection limits; extract phases stream results via `IAsyncEnumerable` paging or FetchXML
   paging cookies without unbounded memory.
5. Per-item failures are classified via the error model and routed to a retry/reject file.

**SDK features used.** KDV-002, KDV-003, KDV-004 (FetchXML paging), KDV-005 (batch), KDV-008,
KDV-009, KDV-011.

## UC-5: Power Pages backend services

**Scenario.** A Power Pages site is backed by a companion .NET API (for example, an ASP.NET
Core minimal API) that performs operations the portal cannot do safely client-side: privileged
lookups, server-side validation, and writes executed under a service identity.

**Actors.** Power Pages developers, backend developers.

**Flow.**
1. The companion API registers `AddDataverse` with a service principal or managed identity.
2. Portal calls hit the API; the API queries Dataverse with injection-safe OData or FetchXML
   builders (user-supplied values are encoded, never concatenated).
3. Writes are validated server-side and performed via `IDataverseClient`; errors are mapped
   from `DataverseErrorCategory` to appropriate HTTP responses for the portal.
4. Health checks and telemetry make the companion API operable like any other service.

**SDK features used.** KDV-001, KDV-002, KDV-003, KDV-004, KDV-008, KDV-009, KDV-010, KDV-011,
KDV-012. Future: KDV-020 (Power Pages Web API helpers, v2.0) targets the portal-specific Web
API surface directly.

## UC-6: Administrative tools

**Scenario.** An internal console utility or scheduled task performs administrative chores:
reassigning records, auditing configuration data, verifying environment connectivity, producing
operational reports.

**Actors.** Power Platform administrators, support engineers, consultants.

**Flow.**
1. For interactive tools, authenticate with interactive/developer credentials or
   `DefaultAzureCredential`; scheduled variants use a service principal.
2. Verify connectivity with the `WhoAmI` probe.
3. Query records with FetchXML (often ported from existing Advanced Find queries) or OData;
   apply fixes via CRUD or batch operations.
4. Output includes request IDs from the error model when operations fail, making support
   escalation concrete.

**SDK features used.** KDV-001 (interactive + `DefaultAzureCredential` flows), KDV-002,
KDV-003, KDV-004, KDV-005, KDV-009, KDV-012 (`WhoAmI`).
