# Performance Guide

> How to get the best throughput and latency from the Koras Dataverse SDK, and what the
> SDK does for you. Consistent with [master plan §1/§5](../planning/master-plan.md).
> Companion documents: [benchmarks.md](benchmarks.md) (measurement) and
> [memory-management.md](memory-management.md) (allocation behavior).

## 1. Performance philosophy: the service sets the ceiling

Dataverse throughput is bounded by **service protection limits**, enforced per user per
web server over a 5-minute sliding window — approximately **6,000 requests per 5 minutes
per user**, an execution-time budget, and roughly **20 concurrent requests**. Exceeding
them yields 429 responses with `Retry-After`. (Exact numbers are Microsoft's and can
change; treat them as the order of magnitude, and treat 429 handling — not limit
avoidance tricks — as the contract.)

Consequences for design:

- No amount of client-side micro-optimization buys throughput beyond those limits. The
  SDK's job is therefore: **(1) spend the request budget efficiently (batching, paging,
  column selection), (2) never waste budget (correct retry/backoff, no duplicate
  requests), and (3) add near-zero client-side overhead (minimal allocation, no
  contention)** so the SDK never becomes the bottleneck below the service ceiling.
- Latency per call is dominated by network + service time (typically tens to hundreds
  of milliseconds); SDK overhead must stay in the noise (microseconds on hot paths —
  the benchmark suites keep this honest).

What the SDK already does for you (KDV-008): honors `Retry-After` exactly, retries only
transient statuses (429/503/504 and network faults) with jittered exponential backoff,
bounds retries, and surfaces throttling as `DataverseErrorCategory` so you can build
back-pressure. Do not wrap SDK calls in your own retry loops — stacked retries multiply
load and worsen throttling for everyone.

## 2. Use `$batch` for multi-operation work

Every HTTP round trip costs latency and one unit of request budget. `$batch` (KDV-005)
packs up to 1,000 operations into one request:

```csharp
var batch = new BatchRequest();
foreach (var account in accountsToCreate)
    batch.Add(BatchOperation.Create(account));
var response = await dataverse.ExecuteBatchAsync(batch, ct);
// response.Items: per-item results, in order
```

Guidance:

- Prefer batches of a few hundred operations over maximal 1,000-op payloads: large
  payloads increase per-request execution time (which is itself protected by a limit)
  and make failures more expensive to retry. Start around 100–500 and measure.
  Payload-size implications are covered in
  [memory-management.md](memory-management.md).
- Use **change sets** only when you need atomicity — atomic sets are all-or-nothing,
  so one bad row rolls back the set. Use continue-on-error mode for bulk loads and
  inspect `BatchItemResult` per item.
- Batching reduces *request count*, not *service work*: 6,000 requests of 500 ops is
  not free throughput; the execution-time budget still applies.

## 3. Stream with `QueryAllAsync` — do not buffer result sets

`QueryAllAsync` (KDV-003) returns `IAsyncEnumerable<Entity>` and fetches pages lazily:

```csharp
await foreach (var account in dataverse.QueryAllAsync(query, ct))
    await ProcessAsync(account, ct);
```

- Memory stays proportional to **one page**, regardless of total rows
  ([memory-management.md](memory-management.md)).
- Early exit (`break`, or an unmatched `Where` in your pipeline ending enumeration)
  stops fetching further pages — budget is only spent on pages you actually consume.
- Avoid `ToListAsync`-style materialization of large sets; if you truly need a list,
  you probably also need a `Top(...)` bound.
- FetchXML paging (KDV-004) uses paging cookies under the hood via the corresponding
  fetch-all path — same guidance applies.

## 4. Select only the columns you need

Retrieving all columns is the single most common Dataverse performance mistake — wider
rows mean more service work, larger payloads, slower deserialization, and (for some
column types) extra lookups service-side.

```csharp
ODataQuery.For("account").Select("name", "revenue")   // yes
// vs. no Select(...)  → all columns                  // avoid
```

The SDK makes column selection the visually natural first call in both builders
(`Select(...)` / `.Attributes(...)`); samples and docs always model it. This is also a
data-minimization win ([data-protection.md](../security/data-protection.md)).

## 5. Singleton clients and `HttpClient` reuse

The SDK is built for the `IHttpClientFactory` era (master plan §5):

- `AddDataverse` registers clients as **singletons**; `IDataverseClient` is
  thread-safe. Resolve it once per application (or per named configuration) and share
  it. **Never** create clients per request or per operation.
- The underlying `HttpClient` comes from `IHttpClientFactory` (named client
  `"Koras.Dataverse:{name}"`): connection pooling, handler lifetime rotation, and
  DNS-change handling are correct by construction. Creating ad-hoc clients outside DI
  forfeits pooling and risks socket exhaustion.
- The token cache is per named client with single-flight refresh — under load,
  token acquisition costs one request per expiry window, not one per caller.
- Multiple environments: use **named clients** via `IDataverseClientFactory` rather
  than constructing throwaway configurations.

## 6. Concurrency: parallelize modestly, below the service cap

- The service allows roughly 20 concurrent requests per user; beyond that you buy 429s,
  not throughput. Keep client-side parallelism well below the cap (e.g., 4–8 concurrent
  operations) and prefer batching over parallel single requests.
- The SDK does not impose a global throttle in the MVP; it exposes throttling errors
  with `IsTransient` and honors `Retry-After`, and `DataverseRetryOptions` lets you
  tune the policy. Building a semaphore/back-pressure layer on top is straightforward
  and recommended for high-volume workers.

## 7. Quick checklist

- [ ] One singleton client per environment (DI-registered), never per-operation clients.
- [ ] `$batch` for any multi-row write path; continue-on-error for bulk loads.
- [ ] `QueryAllAsync` streaming instead of buffering; `Top(...)` when you don't need
      everything.
- [ ] `Select`/`Attributes` on every query — never all columns by default.
- [ ] No custom retry loops around SDK calls; tune `DataverseRetryOptions` instead.
- [ ] Client-side parallelism bounded (stay under ~20 concurrent; batch first).
- [ ] Watch the SDK's meters (request counts, durations, retries — KDV-011) to see
      throttling before users do.
