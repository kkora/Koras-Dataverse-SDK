# Data Protection

> Policy for how the Koras Dataverse SDK handles the data that flows through it —
> logging, telemetry, transit, and memory. Consistent with
> [master plan §7](../planning/master-plan.md#7-security-summary) and the
> [threat model](threat-model.md). These are binding requirements on the implementation
> and are enforced by the redaction/telemetry tests defined in
> [test-strategy.md](../testing/test-strategy.md).

## 1. Assume PII everywhere

Dataverse is a business-data platform: contacts, emails, phone numbers, addresses,
financial figures, health data in some industries. The SDK cannot know which attributes
are sensitive, so the policy is categorical: **every attribute value is treated as
potentially personal data.** Only identifiers and shape (table logical names, attribute
*names*, counts, ids, request ids, HTTP status) are considered safe diagnostic metadata.

The SDK stores nothing at rest — no cache files, no offline queues, no temp files. Data
exists only in transit and transiently in memory. That makes logging and telemetry the
two places where the SDK could accidentally create a persistent copy of PII, and this
policy exists to prevent exactly that.

## 2. Logging policy

Logger categories follow the documented `Koras.Dataverse.*` category naming (KDV-011) so
consumers can filter precisely.

### 2.1 Level rules

| Level | May contain | Must never contain |
|---|---|---|
| `Information` and above | Operation name, table logical name, row id (GUID), duration, HTTP status, retry count, Dataverse request id, error category/code | **Attribute values**, filter *values*, raw request/response bodies, tokens, secrets, `Authorization` header |
| `Debug` | Additionally: query structure (encoded query string / FetchXML with **values redacted**), page counts, batch composition (operation kinds and counts) | Tokens, secrets, `Authorization` header, full bodies |
| `Trace` | Additionally: request/response bodies **only** when the consumer explicitly opts in via a clearly named option (planned as a diagnostics option on `DataverseClientOptions`; exact name fixed in the API design doc) — default off | Tokens, secrets, `Authorization` header — under no circumstances, at no level |

Rationale for the hard line at `Information`: production systems commonly ship
`Information` logs to shared sinks with broad access and long retention; attribute
values there would silently build a shadow copy of business data outside Dataverse's
security model.

### 2.2 Structured logging guidance

- All SDK log statements use structured message templates with named placeholders —
  never string interpolation — so consumers can route/filter/redact on properties:
  `"Dataverse {Operation} on {Table} completed in {ElapsedMs} ms (status {StatusCode}, request {RequestId})"`.
- Property names are stable and documented; they form part of the observability contract.
- Row GUIDs are logged as ids (they are references, not content). Alternate **key
  values**, by contrast, can be business data (e.g., an email used as a key) — they are
  treated as attribute values and follow the value rules above.
- Exception logging: `DataverseException`/`DataverseError` messages are built to be
  loggable — they contain category, code, HTTP status, request id, and the server's
  error message, but the SDK never embeds request payloads into exception messages.

### 2.3 Token and secret redaction

- The `Authorization` header value, access tokens, client secrets, and certificate
  private material are never written to any log level, any exception, any activity tag,
  any metric dimension. There is no diagnostic switch that changes this.
- If the opt-in body tracing (§2.1 `Trace`) is enabled, headers are still emitted with
  `Authorization` redacted to a fixed literal (e.g., `Bearer [REDACTED]`).
- Options types do not leak secrets through `ToString()` or debugger displays.
- Enforced by capture-logger tests over success, failure, retry, and cancellation paths
  ([test-strategy.md §4.14](../testing/test-strategy.md)).

## 3. Telemetry tag policy (ActivitySource / Meter)

Instrumentation (KDV-011): `ActivitySource` `"Koras.Dataverse"` and the SDK `Meter`.
Telemetry pipelines are typically exported to third-party backends, so the bar is even
stricter than logs:

**Allowed tags/dimensions:** operation name, table logical name, HTTP status code, error
category (taxonomy value, not message), retry attempt count, page index, batch size
(count), environment host (the configured host name — configuration, not data), request
id.

**Prohibited — no row data in traces, ever:** attribute values, filter/condition values,
raw query strings or FetchXML containing values, entity payloads, alternate key values,
error message free text (may echo submitted values back), tokens/secrets/headers.

Additional rules:

- Metric dimensions must be **low-cardinality by design**: table name and operation are
  acceptable; row ids and per-request values are not (both a cost and a privacy rule).
- Activity display names are structural (`dataverse.create account`), never
  value-bearing.
- The `Koras.Dataverse.OpenTelemetry` package only *subscribes* exporters/providers to
  these sources; it adds no tags of its own and cannot widen this policy.

## 4. Data in transit

- TLS for everything: HTTPS-only `EnvironmentUrl` enforced at validation; no option to
  disable certificate validation; TLS version/cipher policy is deliberately delegated to
  the platform (`SocketsHttpHandler` + OS), which receives security updates faster than
  any library pin could. See [threat model §2.1](threat-model.md).
- No SDK-level transport compression tricks that could interact badly with secrets
  (no custom compression of authenticated requests beyond platform defaults).
- Proxies are honored via standard `HttpClient` environment behavior; corporate
  TLS-inspection proxies must be trusted at the OS level, not by weakening validation
  ([secure-configuration.md §6](secure-configuration.md)).

## 5. Caching considerations

The SDK's caches are deliberately minimal, and each is examined for data-protection
impact:

| Cache | Contents | Policy |
|---|---|---|
| Token cache (per named client) | Access token + expiry | In-memory only, never persisted, replaced on proactive refresh (5 minutes before expiry), isolated per named client configuration ([threat model §2.6](threat-model.md)) |
| `HttpClient`/handler pooling | Connections, not data | Standard `IHttpClientFactory` behavior; no response caching enabled |
| Response data | — | **The SDK does not cache business data or metadata in the MVP.** No `IMemoryCache` integration, no hidden metadata cache. If future versions add opt-in metadata caching, it must be in-memory, off by default, and covered by an update to this document *before* implementation. |

Consumers who cache SDK results themselves (e.g., caching query results in Redis) take on
the corresponding responsibilities — retention limits, encryption at rest, tenant
isolation — and the docs say so explicitly rather than pretending SDK guarantees extend
into consumer caches.

## 6. Data minimization features

The SDK actively helps consumers move less data (privacy and performance align here —
see [performance-guide.md](../performance/performance-guide.md)):

- `ColumnSet`/`Select(...)` make column-limited reads the natural path; docs and samples
  never model `SELECT *`-style retrieval as the default.
- `IAsyncEnumerable` paging (`QueryAllAsync`) streams pages instead of materializing full
  result sets in memory.
- Batch operations reduce request metadata overhead but do not duplicate payloads.

## 7. Compliance posture (honest scope)

The SDK is a transport library: it is neither a data controller nor a processor, and it
makes no GDPR/HIPAA/etc. compliance claims on behalf of consumers. What it guarantees is
narrower and testable — no persistence, no value-bearing logs at `Information`+, no row
data in telemetry, redacted secrets everywhere. Consumers build their compliance story on
top of Dataverse's own capabilities and their application design; this document defines
exactly which risks the SDK does and does not remove.
