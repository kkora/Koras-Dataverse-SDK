# Troubleshooting: Provider Errors

Failures that originate outside your code: Microsoft Entra ID (token acquisition) and
Dataverse service protection (throttling).

## Entra ID (AADSTS) errors

Token acquisition failures surface as `DataverseException` with category `Authentication`,
wrapping the underlying Azure.Identity exception (see `InnerException`). The message contains
the AADSTS code — the fastest route to the cause:

| AADSTS code | Meaning | Fix |
|---|---|---|
| `AADSTS7000215` | Invalid client secret | The secret value is wrong — commonly the secret **id** was copied instead of the **value**, or the secret expired. Create a new secret and copy its value at creation time |
| `AADSTS7000222` | Client secret expired | Create a new secret; plan rotation (or move to certificate/managed identity) |
| `AADSTS700016` | Application not found in tenant | `ClientId` is wrong, or `TenantId` points at a tenant where the app isn't registered |
| `AADSTS90002` | Tenant not found | Fix `TenantId` (GUID or `contoso.onmicrosoft.com` form) |
| `AADSTS500011` | Resource principal not found | The token was requested for a resource the tenant doesn't know — usually a wrong `EnvironmentUrl` (the SDK derives the scope `{environment authority}/.default` from it) |
| `AADSTS50126` | Invalid username or password | Interactive/user flows only — credentials wrong |
| `AADSTS50076` / `AADSTS50079` | MFA required | Interactive flows: complete MFA. Service flows: switch to an app identity (client secret/certificate/managed identity), which is not subject to MFA |
| `AADSTS53003` / `AADSTS530003` | Blocked by conditional access | Ask your identity admin to exempt or accommodate the app/host; service identities with CA-compliant hosting usually resolve this |
| `AADSTS65001` | Consent not granted | Grant admin consent for the app in the tenant |
| `AADSTS700027` | Certificate validation failed | Wrong certificate uploaded to the app registration, or expired; re-upload the current public key |

Two reminders:

- An AADSTS-clean token can still yield **403** from Dataverse: Entra ID authenticates the
  *application*; Dataverse authorizes the *application user*. The app must be added as an
  application user with a security role in each environment.
- With `UseDefault()`, failures may list several credential attempts (environment → workload
  identity → managed identity → CLI …). Read the chain output to see which sources were tried;
  in production prefer the explicit `UseManagedIdentity()` so failures are unambiguous.

## Dataverse service protection (throttling)

Dataverse enforces service-protection limits **per user, per web server, over a sliding
5-minute window**, on three axes: number of requests, total execution time, and concurrent
requests. Exceeding any returns **HTTP 429** with a `Retry-After` header.

### What the SDK does automatically

- Retries 429 up to `Retry.MaxRetries` times (default 3), **honoring `Retry-After`** — the
  server's hint wins even over `Retry.MaxDelay`.
- Counts each 429 in the `koras.dataverse.client.throttles` metric and each retry in
  `koras.dataverse.client.retries`, and logs a warning per retry on `Koras.Dataverse.Http`.
- All retrying stays within the operation's `Timeout`; if the pushback outlasts both the retry
  and time budgets, you get a `DataverseException` with category `Throttling`,
  `IsTransient = true`, and `Error.RetryAfter` populated when the server sent it.

### What is yours to do

The limits are capacity signals, not bugs; retrying harder is counterproductive. When
`Throttling` exceptions reach your code:

- **Reduce parallelism.** Concurrency is one of the limited axes — a semaphore around bulk
  SDK usage is often the single most effective change.
- **Batch writes.** One `$batch` of 500 operations consumes one request slot against the
  request-count limit (execution time still accrues per operation).
- **Trim payloads.** Explicit `Select`/`ColumnSet`, `IncludeAnnotations = false` on hot paths.
- **Honor `Error.RetryAfter` at the workload level** — delay the *job*, not just the request.
- **Spread identity load.** Limits are per user: distinct workloads using distinct application
  users don't share a budget.
- For background bulk work, raise `Retry.MaxRetries`/`MaxDelay` so the SDK absorbs longer
  pushback (see the [worker service guide](../guides/worker-service.md)).

### 502/503/504

Gateway/unavailable/timeout statuses are also retried automatically (they are transient
service conditions, distinct from service protection). Plain **500** is *not* retried — it is
a deterministic server fault (often a plugin or flow failing synchronously) and repeats until
the cause is fixed; check the environment's plugin trace log.

## Related

- [Common errors table](common-errors.md)
- [Diagnostics](diagnostics.md) — capturing request ids for support
- [Error handling concepts](../concepts/error-handling.md)
