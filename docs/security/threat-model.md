# Threat Model

> STRIDE-style threat model for the Koras Dataverse SDK, expanding
> [master plan §7](../planning/master-plan.md#7-security-summary). This is a planning
> document: mitigations marked as SDK responsibilities are requirements on the
> implementation and its tests, not claims about existing code. Vulnerability reporting
> is defined in the repository `SECURITY.md` (private disclosure).

## 1. System context

The SDK is a **library** embedded in a consumer application. It holds no data at rest,
runs no server, and opens no listening ports. Its security posture is therefore about:
(a) not weakening the host application, (b) handling credentials and tokens correctly,
(c) constructing requests that cannot be hijacked by hostile input, and (d) failing
safely.

```
┌────────────────────────────┐
│  Consumer application      │
│  ┌──────────────────────┐  │      TB2       ┌──────────────┐
│  │  Koras Dataverse SDK │──┼──── HTTPS ────▶│  Entra ID    │  (token endpoint,
│  │  (in-process)        │  │                └──────────────┘   via Azure.Identity)
│  │                      │  │      TB3       ┌──────────────┐
│  │                      │──┼──── HTTPS ────▶│  Dataverse   │  (Web API v9.2)
│  └──────────▲───────────┘  │                └──────────────┘
│     TB1     │ app code     │
└─────────────┴──────────────┘
```

### 1.1 Assets

| Asset | Where it lives | Sensitivity |
|---|---|---|
| Client secrets / certificates / credential material | Consumer configuration → `DataverseAuthenticationOptions` → `Azure.Identity` credential | Critical — grants Dataverse access as the application user |
| Access tokens | In-memory token cache inside the default `IDataverseTokenProvider`; `Authorization` headers | Critical — bearer tokens; possession = access until expiry |
| Business data (rows) | Request/response payloads in transit; transiently in memory as `Entity`/models | High — routinely contains PII ([data-protection.md](data-protection.md)) |
| Metadata (schema, table/column names, relationships) | `IMetadataClient` responses | Medium — aids attacker reconnaissance; may reveal business structure |
| Environment/tenant identifiers (`EnvironmentUrl`, tenant id, client id) | Options, logs, telemetry | Low-Medium — not secrets, but identify targets |
| SDK supply chain (source, packages, release pipeline) | GitHub, NuGet.org | Critical — a compromised package compromises every consumer |

### 1.2 Trust boundaries

- **TB1 — Application ↔ SDK.** The SDK trusts the host application (same process; no
  in-process defense is possible against a hostile host). But the SDK does *not* trust
  the **data values** the application passes into query builders — those frequently
  originate from end users and are treated as untrusted input.
- **TB2 — SDK ↔ Entra ID.** Token acquisition over HTTPS via `Azure.Identity`. The SDK
  trusts platform TLS validation; it never implements its own token protocol.
- **TB3 — SDK ↔ Dataverse.** All Web API traffic. Responses are untrusted input: error
  payloads, entity JSON, batch multiparts, and paging links are parsed defensively.

## 2. Threats and mitigations (STRIDE)

### 2.1 Spoofing

| Threat | Vector | Mitigation |
|---|---|---|
| SSRF / environment spoofing via `EnvironmentUrl` | Attacker-influenced configuration points the SDK at a hostile host that harvests tokens (the token scope `{environmentUrl}/.default` and the bearer token would both go to the attacker) | **HTTPS-only enforcement**: non-HTTPS `EnvironmentUrl` is rejected at options validation, before any request. `EnvironmentUrl` is the single authority for the request host — resource paths are built relative to it and cannot be redirected to another authority by input values. **No cross-host redirects followed**: the HTTP pipeline does not auto-follow redirects to a different host; a redirect off the configured host surfaces as an error rather than silently re-sending the `Authorization` header elsewhere. Consumers must treat `EnvironmentUrl` as trusted configuration, not user input — documented in [secure-configuration.md](secure-configuration.md). |
| Token audience confusion | Token minted for one resource replayed to another | Scope is derived from the configured `EnvironmentUrl` (`{environmentUrl}/.default`); tokens are cached per client configuration, never shared across differently configured named clients. |
| Spoofed Dataverse responses (MITM) | On-path attacker | TLS via the platform stack (`SocketsHttpHandler`); the SDK never disables certificate validation, never exposes an option to do so, and never downgrades to HTTP. Sovereign/other clouds are reached by configuring their HTTPS `EnvironmentUrl`, not by loosening validation. |

### 2.2 Tampering

| Threat | Vector | Mitigation |
|---|---|---|
| **OData injection** | Hostile values (names, search text) concatenated into `$filter`/`$orderby` could alter query semantics or address other data | `ODataQuery`/`ODataFilterBuilder` treat every value as data: strings are quoted with embedded quotes doubled, GUIDs/dates/numbers/booleans are rendered through strict invariant-culture literal encoders, field names are validated against an identifier pattern. There is no code path where a caller-supplied *value* is emitted unencoded. Verified by the hostile-input corpus tests ([test-strategy.md §4.14](../testing/test-strategy.md)). |
| **FetchXML injection** | Hostile values breaking out of XML attribute/element context (`"`, `<`, `&`, entities) | The `FetchXml` builder generates XML via a proper XML writer (never string concatenation), so all values are XML-escaped by construction; entity/attribute names are validated. Output well-formedness is asserted by re-parsing in tests with DTD processing prohibited. |
| **Raw-string escape hatches** | The SDK accepts caller-built raw FetchXML strings for execution (e.g., `FetchAsync` with pre-built XML) and OData fragments where documented | **Caller responsibility, explicitly documented**: when a raw string bypasses the builders, the SDK cannot guarantee encoding. The XML documentation on these members and the docs carry an injection warning and point to the builders as the safe default. The SDK still refuses to let raw input change the request *authority* (host), and raw FetchXML is transmitted as an encoded query parameter/body value, never interpolated into the URL structure. |
| Malicious server payloads | Hostile JSON/XML in responses (deeply nested, oversized, type-confusing) | No polymorphic deserialization anywhere (master plan §7); `System.Text.Json` with strict depth defaults; error bodies size-capped before parsing; unknown members ignored, never dynamically dispatched. |
| Tampered dependencies | Compromised package versions | Locked, minimal dependency set; CI vulnerability scanning; SBOM; see [dependency-security.md](dependency-security.md). |

### 2.3 Repudiation

| Threat | Vector | Mitigation |
|---|---|---|
| Untraceable SDK-originated operations | Failures with no correlation trail | Every `DataverseError` carries the Dataverse request id; activities (`ActivitySource "Koras.Dataverse"`) carry operation name and status so consumer telemetry can correlate with Dataverse-side audit logs. The SDK never *removes* correlation headers. |
| Misattributed writes | Operations run as the application identity | MVP always acts as the configured application user (impersonation is KDV-013, v1.1). Documentation states clearly that Dataverse audit trails will show the application user; consumers needing per-user attribution must wait for impersonation support or record actor context in their own audit layer. |

### 2.4 Information disclosure

| Threat | Vector | Mitigation |
|---|---|---|
| **Credential leakage via configuration** | Secrets committed in `appsettings.json`, connection strings, source | No connection-string API exists (by design). Secrets enter only through options/credential objects. Guidance mandates user-secrets in dev and Key Vault/managed identity in prod ([secure-configuration.md](secure-configuration.md)); repository secret scanning (push protection) guards the SDK repo itself. |
| **Token/secret logging** | Tokens or secrets in logs, exceptions, traces | Hard rule: the `Authorization` header, token values, and secret values are never logged at any level, never included in exception messages, never added as activity tags or metric dimensions. `DataverseClientOptions`/authentication options do not expose secret values via `ToString()`. Enforced by capture-logger redaction tests on all failure paths ([test-strategy.md §4.14](../testing/test-strategy.md)). |
| Business data in logs/telemetry | Attribute values logged at Information, row data in trace tags | Logging policy: no attribute values at `Information` or above; telemetry tags carry names/counts/ids only, never row data. Full policy in [data-protection.md](data-protection.md). |
| Token exposure in memory dumps | Process dump contains cached token | Accepted residual risk inherent to bearer-token clients in-process; tokens are held only as long as useful (refresh 5 minutes before expiry replaces them) and never written to disk by the SDK. Consumers with dump-hardening requirements must address it at host level. |
| Metadata disclosure | Verbose SDK errors echoing schema to end users | `DataverseException` messages are developer-facing; docs instruct consumers not to surface raw exception messages to end users. |

### 2.5 Denial of service

| Threat | Vector | Mitigation |
|---|---|---|
| **Throttling abuse / self-inflicted DoS** | Unbounded retries amplifying a 429 storm; runaway loops hammering service protection limits | Bounded retry budget (`DataverseRetryOptions`), `Retry-After` always honored (never retried sooner), jittered exponential backoff to prevent thundering herds, no infinite retry mode offered. Batch guard (1000 operations) prevents oversized payload rejection loops. Paging is lazy (`IAsyncEnumerable`) — page N+1 is fetched only on demand. |
| SDK used as an amplification client against Dataverse | Malicious consumer floods a tenant | Out of SDK control; Dataverse service protection limits are the platform's defense. The SDK's contribution is to *never make a polite consumer look like an attacker* (backoff correctness) and to document limits ([performance-guide.md](../performance/performance-guide.md)). |
| Resource exhaustion from hostile responses | Multi-GB response bodies, infinite `@odata.nextLink` chains | Error bodies size-capped before parse; paging follows `nextLink` only on consumer pull, and only on the configured host; per-request timeout bounds every attempt. |
| Startup DoS via misconfiguration | Bad config causing hangs at first use | Options validated at startup (fail fast), not on first request under load. |

### 2.6 Elevation of privilege

| Threat | Vector | Mitigation |
|---|---|---|
| Over-privileged application user | SDK credential holding System Administrator | Guidance-level mitigation: least-privilege application user roles documented as the default posture ([secure-configuration.md](secure-configuration.md)); samples never instruct granting admin roles. |
| Injection-based privilege abuse | Query injection reading rows outside intended scope | Builder encoding (§2.2) plus Dataverse row-level security as the authoritative backstop — the SDK never bypasses platform authorization. |
| **Multi-tenant / multi-environment isolation failures** | One process talking to several environments/tenants leaks tokens or data across them | Named clients are fully isolated: each named registration has its own options, its own `HttpClient` (`"Koras.Dataverse:{name}"`), and its own token cache keyed to its configuration — a token acquired for environment A can never be attached to a request for environment B. `IDataverseClientFactory` never falls back to another name's configuration on a miss (it throws). DI tests pin this isolation. SaaS builders multiplexing many customer tenants remain responsible for tenant-selection correctness *above* the SDK (choosing the right named client per request) — documented explicitly. |
| Dependency-borne elevation | Vulnerable transitive package | Minimal dependency tree, CI scanning, prompt patch policy ([dependency-security.md](dependency-security.md)). |

## 3. Supply chain (summary)

The full policy is in [dependency-security.md](dependency-security.md). Threat-model
highlights: core packages depend only on `Azure.Identity` + `Microsoft.Extensions.*`;
`Abstractions` and `FetchXml` have zero third-party dependencies (nothing to compromise);
CI runs CodeQL, dependency review, and `dotnet list package --vulnerable
--include-transitive`; releases produce an SBOM; author signing is planned at 1.0; the
publish path is protected by a GitHub environment ([nuget-publishing.md](../release/nuget-publishing.md)).

## 4. Residual risks (accepted, documented)

1. In-process attackers (hostile code in the consumer application) can read tokens and
   secrets — no library can defend its own process.
2. Bearer tokens in memory appear in process dumps (see §2.4).
3. Raw FetchXML/OData escape hatches shift encoding responsibility to the caller —
   accepted for expressiveness, mitigated by documentation and by making builders the
   prominent, easiest path.
4. Compromise of Entra ID or Dataverse themselves is out of scope.

## 5. Review cadence

This model is reviewed at every milestone-8 hardening pass, before every minor release,
and whenever a feature crosses a trust boundary in a new way (file columns KDV-014 and
impersonation KDV-013 both require a threat-model update before implementation).
