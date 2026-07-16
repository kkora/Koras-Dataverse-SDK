# Feature Planning — KDV-007 Solutions

> Planning-level design document (pre-implementation). Catalog entry:
> [`feature-catalog.md`](feature-catalog.md#kdv-007--solutions).
> Source of truth: [`docs/planning/master-plan.md`](../planning/master-plan.md) §3 (KDV-007),
> §4, §5, §7. Release classification: **MVP**.

## Overview

KDV-007 provides solution lifecycle operations for deployment automation (UC-3):
export, import with asynchronous job polling, publish-all-customizations, and querying
installed solutions — via `ISolutionClient`. It brings pipeline-grade solution handling into
.NET code without shelling out to external tools (replacing pac CLI remains a non-goal).

## Requirements

**Functional**

1. **Export:** export a solution by unique name, managed or unmanaged, returning the solution
   archive bytes/stream.
2. **Import:** import a solution archive with `SolutionImportOptions`; imports run as
   asynchronous server jobs — the client polls the job to a terminal state and returns only
   then (master plan §3: "async job polling for import").
3. **Publish-all:** trigger publish-all-customizations and await completion.
4. **Query installed:** list installed solutions (unique name, version, managed flag,
   publisher — exact members of `SolutionInfo` subject to implementation).
5. All operations cancellable end-to-end, including mid-poll.
6. `ISolutionClient` injectable and mockable, registered by `AddDataverse` (KDV-010).

**Nonfunctional.** Polling cadence driven by injected `TimeProvider` (deterministic tests,
master plan §5); long operations respect a bounded overall wait with a classified timeout;
archives handled as opaque binary.

## Proposed public API

Types fixed by master plan §4: `ISolutionClient`, `SolutionInfo`, `SolutionImportOptions` in
`Koras.Dataverse.Solutions`.

Conservative proposal, subject to implementation:

```csharp
public interface ISolutionClient
{
    Task<Stream> ExportAsync(string solutionName, bool managed,
        CancellationToken cancellationToken = default);
    Task ImportAsync(Stream solutionArchive, SolutionImportOptions options,
        CancellationToken cancellationToken = default);
    Task PublishAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SolutionInfo>> GetInstalledSolutionsAsync(
        CancellationToken cancellationToken = default);
}
```

`SolutionImportOptions` carries import behavior flags (e.g., overwrite unmanaged
customizations, publish workflows — member set subject to implementation) plus polling bounds.
Whether export offers byte-array convenience overloads is an implementation detail.

## Configuration

- `SolutionImportOptions`: behavior flags as above; polling interval and overall timeout with
  documented defaults (proposed: interval seconds-scale, overall timeout generous — exact
  values subject to implementation and tuned against real import durations).
- No global solution options; per-call options keep pipeline scripts explicit.

## Error conditions

| Condition | Behavior |
|---|---|
| Export of unknown solution | `DataverseException`, not-found category |
| Import job ends failed/canceled server-side | Classified `DataverseException` carrying job diagnostic detail (message/data from the import job record) |
| Poll timeout exceeded | Classified timeout error distinguishing "job still running" from failure — the job id is included so callers can resume observation |
| Caller cancellation mid-poll | `OperationCanceledException` unwrapped; the server job continues (documented) |
| Invalid/corrupt archive rejected by server | Classified via KDV-009 with request id |
| Throttling on any call | KDV-008 handles; polling requests included |

## Security

- Solution archives are treated as **opaque bytes** — never extracted, never deserialized by
  the SDK (master plan §7: no polymorphic deserialization; zip handling is a known attack
  surface the SDK refuses to own).
- Deployment credentials guidance: dedicated service principal per target environment
  (documented in the pipeline recipe).
- Job diagnostic text is passed through as data; docs note it may contain customization
  names.

## Performance

- Export and import stream payloads where the API allows; multi-hundred-MB solutions must not
  demand proportional heap.
- Polling uses async delays (no thread blocking) and modest intervals to stay friendly to
  service protection limits.
- Publish-all can be long; same polling discipline applies where the platform exposes it as
  async.

## Observability

- One `Activity` spanning each full logical operation — for import, the activity covers
  upload plus the entire polling phase (KDV-011); poll iterations are events/logs, not child
  activities, to avoid trace noise (subject to implementation).
- Logs: job id at information on start; job status transitions at debug; terminal outcome at
  information (success) or error (failure) with diagnostics.
- Metrics: standard operation counters/durations; import duration lands in the histogram's
  long tail — documented for dashboard bucket configuration.

## Test plan

**Unit** (fake `HttpMessageHandler` + fake `TimeProvider`):
- Request shapes: export parameters, import payload framing, publish-all call, installed
  query.
- Polling state machine: pending → running → success/failure/cancel transitions; interval
  scheduling with fake time; overall-timeout enforcement; cancellation mid-poll.
- Failure mapping: job failure diagnostics surfaced; unknown-solution 404.
- `SolutionInfo` mapping fixtures.

**Integration** (env-var gated): export a small known solution → import it into the same or a
scratch environment → publish-all → verify via installed-solution query (version visible).
Deliberately corrupt archive import produces a classified failure.

## Acceptance criteria

1. `ImportAsync` returns only after the server job reaches a terminal state; success and
   failure both verified against a real environment.
2. Import failures carry job diagnostic detail sufficient to act on (not just "failed").
3. Export → import → publish → query round-trip passes in integration.
4. Mid-poll cancellation is prompt and leaves a documented, non-corrupting state.
5. Polling behavior is fully deterministic under fake time in unit tests.
6. Archives are never parsed/extracted by SDK code (verified by code review + architecture
   conventions).
