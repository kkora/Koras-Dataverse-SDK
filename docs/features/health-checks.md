# Feature Planning — KDV-012 Health Checks

> Planning-level design document (pre-implementation). Catalog entry:
> [`feature-catalog.md`](feature-catalog.md#kdv-012--health-checks).
> Source of truth: [`docs/planning/master-plan.md`](../planning/master-plan.md) §2, §3
> (KDV-012), §4. Release classification: **MVP**.

## Overview

KDV-012 provides a standard ASP.NET Core health check for the Dataverse dependency:
`AddDataverseHealthCheck()` registers a probe that executes `WhoAmI` against the configured
client and maps the outcome to health-check results. `WhoAmI` is the canonical cheap
round-trip — it exercises DNS, TLS, authentication, and authorization in one minimal call.
The check plugs into `Microsoft.Extensions.Diagnostics.HealthChecks` abstractions (master
plan §2), so orchestrators (Kubernetes probes, App Service health, load balancers) and
dashboards consume it with zero extra work.

## Requirements

**Functional**

1. `AddDataverseHealthCheck()` extension in `Microsoft.Extensions.DependencyInjection`
   (master plan §4), registering an `IHealthCheck` that calls `WhoAmI` and returns
   `WhoAmIResponse`-derived status.
2. Result mapping: successful `WhoAmI` → `Healthy`; failures → `Unhealthy` by default, with
   the standard health-check `failureStatus` override available (`Degraded` opt-in).
3. Standard registration options pass through: check name (default proposed:
   `"dataverse"`), tags, timeout, failure status (per
   `Microsoft.Extensions.Diagnostics.HealthChecks` conventions).
4. Named-client selection: the check can target a specific named client registration
   (KDV-010) so multi-environment hosts can probe each environment separately (shape subject
   to implementation).
5. The check result `Description`/data includes the KDV-009 error category on failure and
   the user id from `WhoAmIResponse` on success (proposed, subject to implementation) —
   never raw exception dumps.

**Nonfunctional.** The probe never throws out of `CheckHealthAsync` (failures become
results); honors the health-check context cancellation/timeout; adds no load beyond the
host's configured probe frequency.

## Proposed public API

Fixed by master plan §4: `AddDataverseHealthCheck` in
`Microsoft.Extensions.DependencyInjection`; `WhoAmIResponse` in `Koras.Dataverse`.

Conservative proposal, subject to implementation:

```csharp
services.AddDataverse(o => { /* ... */ });
services.AddHealthChecks().AddDataverseHealthCheck();          // default client
// named client + custom failure status:
// services.AddHealthChecks().AddDataverseHealthCheck("dataverse-prod", clientName: "prod",
//     failureStatus: HealthStatus.Degraded, tags: ["ready"]);
```

`WhoAmIResponse` (per master plan §4 type list) carries the caller identity returned by the
platform (user id, business unit id, organization id — exact members subject to
implementation).

## Configuration

- Registration-time: check name, tags, failure status, timeout — standard health-check
  parameters, not SDK-specific options.
- Named-client selection parameter (subject to implementation).
- No result caching in MVP: probe frequency is the host's responsibility; a caching layer
  would add complexity without demonstrated need (see
  [`../product/problem-statement.md`](../product/problem-statement.md)). Revisit only on
  evidence of probe-induced throttling.

## Error conditions

| Underlying failure | Health outcome |
|---|---|
| Authentication failure (KDV-001) | Unhealthy (or configured status); description carries `Authentication` category |
| Authorization failure | Unhealthy; `Authorization` category |
| Throttling (429 after KDV-008 retries) | Unhealthy; `Throttling` category — signals pressure, not outage; docs discuss `Degraded` mapping for this case |
| Network/DNS/TLS failure | Unhealthy; `Network` category |
| Probe timeout / context cancellation | Unhealthy with timeout indication; `OperationCanceledException` from the host context handled per health-check conventions |
| Unexpected exception | Caught and converted to Unhealthy — never thrown out of the check |

## Security

- Check output contains no tokens, secrets, connection details, or stack traces — categories
  and ids only.
- Health endpoints themselves are host concerns; docs remind users to protect detailed
  health endpoints (the SDK does not expose endpoints, only the check).
- The probe uses the same credential path as normal traffic — no separate or weaker
  credential mode.

## Performance

- `WhoAmI` is the smallest meaningful round-trip; cost per probe is one authenticated GET.
- Probe frequency is host-configured; documentation recommends conservative intervals and
  notes that probes count toward service protection limits.
- The check reuses the singleton client — no per-probe client or handler construction.

## Observability

- Probe executions flow through the standard SDK telemetry (KDV-011): a `WhoAmI` activity
  and operation metrics, tagged like any client call — health traffic is therefore visible
  and attributable in traces/dashboards.
- Host-side health-check logging applies as configured; the SDK adds no duplicate logging
  beyond its normal operation logs.

## Test plan

**Unit** (fake `HttpMessageHandler`):
- Mapping matrix: success → Healthy; each failure class in the table above → documented
  status and category in the result description/data.
- `failureStatus` override respected; tags and name registration verified.
- Timeout/cancellation: canceled context produces a result (not an unhandled exception)
  within the deadline.
- Named-client selection routes the probe to the correct base address (asserted on the fake
  handler).
- Never-throws guarantee: injected pathological failures still yield a result.

**Integration** (env-var gated, master plan §6 covers WhoAmI): probe against a real
environment reports Healthy; probe with deliberately broken credentials reports Unhealthy
with the authentication category; result appears correctly in a hosted
`/health` endpoint in the minimal API sample.

## Acceptance criteria

1. `AddDataverseHealthCheck()` composes with standard `AddHealthChecks()` registration and
   appears in the health report under its configured name.
2. Healthy on a reachable, authorized environment (integration-verified).
3. Each simulated failure class maps to the documented status with its KDV-009 category
   surfaced in the result — no raw exception leakage.
4. The check never throws out of `CheckHealthAsync` and always honors the context timeout.
5. Named-client probing works in a two-environment host.
6. The minimal API sample demonstrates the wired check end to end.
