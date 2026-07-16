# Feature Planning — KDV-009 Error Model

> Planning-level design document (pre-implementation). Catalog entry:
> [`feature-catalog.md`](feature-catalog.md#kdv-009--strong-error-model).
> Source of truth: [`docs/planning/master-plan.md`](../planning/master-plan.md) §3 (KDV-009),
> §4, §5. Release classification: **MVP**.

## Overview

KDV-009 normalizes every Dataverse Web API failure into a single, strong error contract:
non-success HTTP responses are parsed from their OData error payloads into a `DataverseError`
(category, Dataverse error code, HTTP status, request id, transient flag) and thrown as
`DataverseException`. Mapping happens after retries (KDV-008), so only post-retry failures
surface (master plan §5). The taxonomy turns string-matching error handling into programmatic
control flow and makes Microsoft support escalation concrete via request ids.

## Requirements

**Functional**

1. Error lifecycle (master plan §5): non-success HTTP → parse OData error payload →
   `DataverseError` → throw `DataverseException`.
2. `DataverseError` carries: `DataverseErrorCategory`, the Dataverse error code, HTTP status,
   request id, and a transient flag (master plan §3).
3. `DataverseErrorCategory` provides a small, stable category set. Proposed (subject to
   implementation): `Authentication`, `Authorization`, `NotFound`, `Validation`,
   `Concurrency`, `Throttling`, `Timeout`, `Network`, `Server`, `Unknown` — the final set is
   fixed before `0.1.0` and treated as public contract thereafter.
4. Transient classification is consistent with KDV-008's retry triggers (throttling, 5xx
   service pressure, network/timeouts transient; validation/permission/not-found not).
5. Error-tolerant parsing: empty, non-JSON, truncated, or unfamiliar payloads still produce a
   classified error (category `Unknown` at worst) with HTTP status preserved — never a raw
   serializer exception (master plan §8: error-tolerant parsing).
6. Unknown Dataverse error codes map to the closest category by status code; the original
   code is always preserved verbatim.
7. `OperationCanceledException` is never converted into `DataverseException` (master plan
   §5).

**Nonfunctional.** Zero cost on the success path; exception messages are informative but
contain no secrets/tokens and bound any echoed payload content.

## Proposed public API

Types fixed by master plan §4: `DataverseException`, `DataverseError`,
`DataverseErrorCategory` in `Koras.Dataverse.Errors` (models live in
`Koras.Dataverse.Abstractions`, dependency-free).

Conservative shape, subject to implementation:

```csharp
public sealed record DataverseError(
    DataverseErrorCategory Category,
    string? Code,          // Dataverse error code, preserved verbatim
    int HttpStatus,
    string? RequestId,
    bool Transient,
    string Message);

public sealed class DataverseException : Exception
{
    public DataverseError Error { get; }
}
```

Consumers branch on `Error.Category` / `Error.Transient`; `Code` supports precise handling of
specific platform errors; `RequestId` goes into support tickets.

## Configuration

None. The taxonomy, classification table, and parsing behavior are fixed contracts — a
configurable error model would undermine the whole point (portable error-handling code).

## Error conditions

This feature *is* the error path; its own edge cases:

| Input | Behavior |
|---|---|
| Well-formed OData error payload | Full field extraction (code, message, request id) |
| Empty body / non-JSON body (HTML gateway pages, proxies) | Classified by HTTP status; payload sample bounded in message |
| Unknown Dataverse code | Category by status; code preserved |
| Success-range status | Never produces an error; no false positives |
| Nested/inner error structures | Deepest meaningful message extracted; structure tolerated, not required |

## Security

- Messages exclude tokens, credentials, and headers; any echoed payload fragment is truncated
  to a bounded length.
- Request ids are correlation data, safe to log and share with Microsoft support — this is
  documented explicitly.
- Parsing uses fixed, non-polymorphic deserialization (master plan §7).

## Performance

- The mapping code executes only on failures; the success path performs no error-related
  parsing or allocation.
- Failure-path parsing is single-pass over a bounded payload read.

## Observability

- Each surfaced failure is logged **once** at the client layer (no double-logging from
  handler + client) with category, code, HTTP status, and request id as structured fields
  (KDV-011).
- Metric: error counter tagged by category.
- The operation `Activity` records error status per OpenTelemetry conventions via the
  KDV-011 layer.

## Test plan

**Unit** (fixture-driven; fixtures captured from real Dataverse responses plus synthetic
hostile cases):
- Payload matrix: throttling (429 + Retry-After), validation (400), permission (403),
  not-found (404), concurrency (412), server (500/503), empty body, HTML body, truncated
  JSON, deeply nested inner errors, unknown codes.
- Classification table test: every (status, code-class) pair maps to the documented category
  and transient flag — the table in the docs and the test share a source.
- Request-id extraction from header and payload variants.
- Message hygiene: no token strings, bounded payload echo (asserted).
- Cancellation passthrough: `OperationCanceledException` never wrapped.

**Integration** (env-var gated): provoked real failures — retrieve of a random Guid
(NotFound), create with an invalid attribute (Validation) — carry populated
`DataverseError` fields including a server request id.

## Acceptance criteria

1. Every non-success response, however malformed, yields a `DataverseException` with a fully
   populated `DataverseError` (fields nullable only where the server gave nothing).
2. The category/transient classification table is published in docs and mechanically shared
   with the test suite — they cannot drift.
3. Transient flags agree exactly with KDV-008's retry trigger set.
4. Request id is captured whenever the server supplies one, in integration-verified real
   responses.
5. No cancellation is ever represented as a Dataverse error.
6. The category enum is frozen at `0.1.0` and documented as public contract.
