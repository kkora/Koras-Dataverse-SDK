# Operation Lifecycle

What actually happens when you call, say, `await dataverse.CreateAsync(entity, ct)`.

## 0. Options validation (before any operation)

Configuration is validated *before* the first request can happen:

- Under DI, `AddDataverse` registers the options with `ValidateOnStart()` — the host fails to
  start on invalid options.
- `DataverseClient.Create` and the `DataverseClient` constructor call the same validation
  immediately.

So by the time an operation runs, `EnvironmentUrl` is a valid HTTPS URL, the retry settings are
sane, and the authentication kind is complete. See [validation](../configuration/validation.md).

## 1. Request build

The client validates arguments (`ArgumentNullException`/`ArgumentException` for programming
errors — these are not `DataverseException`s), resolves the table's entity set name
(override → cached pluralization), serializes the payload (lookups become `@odata.bind`
references), and sets `Prefer` headers (annotations, `return=representation`, page size) as the
operation requires.

## 2. Telemetry span starts

An `Activity` named `dataverse.<operation>` (for example `dataverse.create`) starts from the
`Koras.Dataverse` activity source, tagged with `dataverse.operation` and `dataverse.table`.
The span covers everything below — including all retries.

## 3. Timeout arming

The caller's `CancellationToken` is linked with a new token source armed with
`options.Timeout` (default 100 seconds). This single budget covers **all** retry attempts, not
each attempt separately. `HttpClient.Timeout` is infinite; the SDK's linked token is the only
timeout mechanism.

## 4. Retry loop (RetryHandler)

The request enters the HTTP pipeline. The retry handler:

- retries **HTTP 429** (service protection), **502/503/504**, and network-level
  `HttpRequestException`s — up to `Retry.MaxRetries` times (default 3),
- waits the server's `Retry-After` when present (and `RespectRetryAfter` is true) — a longer
  server hint beats the local cap; otherwise exponential backoff from `BaseDelay` doubling per
  attempt, capped at `MaxDelay`, plus up to 25 % jitter,
- counts every 429 in the `koras.dataverse.client.throttles` metric and every retry in
  `koras.dataverse.client.retries`, logging a warning per retry on the
  `Koras.Dataverse.Http` category,
- never retries after cancellation, and never retries non-transient statuses (400, 401, 403,
  404, 409, 500, …).

## 5. Token acquisition (AuthenticationHandler)

Inside the retry loop — so each attempt is freshly authenticated — the authentication handler
asks the `IDataverseTokenProvider` for a bearer token. The default provider:

- caches the token per environment,
- refreshes it when now ≥ expiry − **5 minutes**,
- refreshes **single-flight**: concurrent callers wait on one refresh instead of stampeding
  the identity provider,
- surfaces acquisition failures as `DataverseException` with category `Authentication`.

## 6. Response handling and error normalization

Back in the client layer:

- **Success** → the response is parsed (row id from the `OData-EntityId` header, JSON body into
  `Entity`/`DataverseQueryResult`, …) and returned.
- **Non-success HTTP** (post-retry) → the OData error payload is parsed and normalized into a
  `DataverseError` — category from the status code, Dataverse error code (for example
  `0x80040217`), the `x-ms-service-request-id` header, `Retry-After`, and the transient flag —
  then thrown as `DataverseException`. An error is logged on the `Koras.Dataverse` category.
- **Timeout** (the linked token fired, caller's token did not) → `DataverseException` with
  category `Timeout`, `IsTransient = true`, inner `OperationCanceledException`.
- **Caller cancellation** → the original `OperationCanceledException` propagates unwrapped.
- **Network failure** (no HTTP response after retries) → `DataverseException` with category
  `Network`, `IsTransient = true`.

## 7. Telemetry emission

In every outcome — success, error, timeout, cancellation — the span is completed (with error
status and `dataverse.error.category`/`dataverse.request_id` tags on failure) and two metrics
are recorded, tagged with `dataverse.operation`, `dataverse.table`, and `outcome`
(`success` / `error` / `timeout` / `canceled` / `network`):

- `koras.dataverse.client.operations` (counter)
- `koras.dataverse.client.operation.duration` (histogram, seconds)

See the [telemetry guide](../guides/telemetry.md) for wiring this into OpenTelemetry.
