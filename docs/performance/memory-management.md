# Memory Management

> How the Koras Dataverse SDK manages memory, and how consumers should use it to keep
> working sets flat. Planning document; these are binding design rules for the
> implementation, verified by the allocation-focused benchmarks
> ([benchmarks.md](benchmarks.md)) and the streaming tests in the test matrix.

## 1. Design rules

1. **Memory proportional to one unit of work.** One page of a query, one batch payload,
   one response — never "the whole logical result set" unless the caller explicitly
   materializes it.
2. **Hot paths allocate only what they return.** The cached-token request path, the
   no-listener telemetry path, and per-row deserialization must not allocate incidental
   garbage (verified by `MemoryDiagnoser` baselines, enforced by the strict allocation
   regression gate in
   [performance-testing.md §4](../testing/performance-testing.md)).
3. **No hidden caches of business data.** The only long-lived SDK state is the token
   cache and pooled connections
   ([data-protection.md §5](../security/data-protection.md)).
4. **Clarity beats cleverness.** Pooling and `Span`-level tricks are applied only where
   a benchmark proves a win on a hot path (§5).

## 2. Streaming

### 2.1 Paging via `IAsyncEnumerable` (KDV-003 / KDV-004)

`QueryAllAsync` and the FetchXML fetch-all path stream rows page by page:

- Page N+1 is requested only when enumeration crosses the page boundary; nothing
  prefetches the full set.
- Each page's entities are yielded and then unreferenced by the SDK — once the consumer
  releases a row, it is collectible. Steady-state managed memory for a million-row scan
  is one page plus the consumer's own retention.
- Enumerator disposal (early `break`, exception, cancellation) releases the in-flight
  response and requests nothing further.
- Consequence for consumers: `await foreach` + process + discard keeps working sets in
  the single-page range; `ToList`-style buffering is a consumer decision with consumer
  costs ([performance-guide.md §3](performance-guide.md)).

### 2.2 Response handling

- Query and CRUD responses are deserialized from the response stream
  (`HttpCompletionOption.ResponseHeadersRead` + `System.Text.Json` async stream APIs)
  rather than via read-string-then-parse, avoiding a duplicate full-body `string` on
  top of the parsed object model. **No full-response buffering where avoidable** is the
  default posture; the known exceptions are listed in §4.
- Error responses are size-capped before parsing (threat model: hostile payloads), so a
  failure can never balloon memory.

### 2.3 File and image columns (future, KDV-014 — v1.1)

Recorded here as a design constraint so MVP decisions don't paint us into a corner:
file column upload/download will be **chunked streaming** end to end (per master plan
§3) — `Stream` in, `Stream` out, bounded chunk buffers, no whole-file `byte[]` on
either path. MVP code must not introduce abstractions that assume in-memory bodies.

## 3. Avoiding the Large Object Heap

Objects ≥ 85,000 bytes land on the LOH, where collection is expensive and fragmentation
hurts long-running services. SDK rules and consumer guidance:

- **Batch payload assembly (KDV-005)** writes multipart content via streaming content
  serialization instead of concatenating one giant payload `string` — the dominant LOH
  risk in this SDK. Per-operation JSON bodies are small; the multipart writer streams
  them out sequentially.
- **Batch size guidance:** the 1,000-operation ceiling is a Dataverse rule, not a
  target. Hundreds of typical operations per batch keeps individual payload buffers
  comfortably sub-LOH and makes retries cheaper
  ([performance-guide.md §2](performance-guide.md)). Consumers with very large
  per-record payloads should scale batch size down accordingly.
- **Paging:** default page sizes (service default 5,000 rows max per page; SDK honors
  `Prefer: odata.maxpagesize` configuration) with wide rows can produce large page
  buffers. Consumers scanning wide tables should combine streaming with a smaller page
  size and tight `Select` column lists — fewer columns is the strongest lever.
- Solution export (KDV-007) returns solution archives that are legitimately large; see
  §4.

## 4. Known full-buffer cases (honest list)

| Case | Why buffered | Bound |
|---|---|---|
| Batch **response** parsing | Multipart response parsing requires assembling per-item bodies; items are parsed sequentially, one item buffer at a time | One item at a time; total items ≤ 1,000 |
| Solution export payload (KDV-007) | The Web API returns the archive in the response body; MVP surfaces it as bytes | Consumer-controlled (size of their solution); streamed-to-disk API considered for v1.1 alongside file columns |
| Request bodies for single CRUD ops | Single-entity JSON is small; buffering is simpler and enables retries without re-serialization surprises | Row size |
| Error bodies | Read for `DataverseError` parsing | Hard size cap |

Retries interact with buffering: a request body must be repeatable for the retry
handler, so request content is either buffered (small CRUD bodies) or reconstructible
(batch assembly re-runs serialization). No design may make a retried request silently
send a half-consumed stream.

## 5. Pooled buffers policy

- Default: **no custom pooling.** `HttpClient`, `SocketsHttpHandler`, and
  `System.Text.Json` already pool internally; naive `ArrayPool` layering on top adds
  bug surface (use-after-return, double-return) for little gain.
- Pooling (`ArrayPool<byte>`, `PooledByteBufferWriter`-style writers) is permitted only
  when a benchmark shows a hot path with meaningful per-op array allocation —
  the expected candidates are batch multipart assembly and FetchXML/OData string
  building at large parameter sizes.
- Every pooled-buffer use must: rent/return in a single method or clearly owned scope,
  return in `finally`, never expose rented arrays beyond the scope, and zero buffers
  that held secrets (tokens never enter pooled buffers by design — auth headers are
  set as header objects, not serialized through SDK-owned buffers).
- `stackalloc` limited to small constant sizes (≤ 256 bytes) on proven hot paths.

## 6. Consumer checklist

- [ ] Stream with `await foreach` (`QueryAllAsync`); don't materialize big result sets.
- [ ] Narrow `Select`/`Attributes` lists — column width drives page buffer size.
- [ ] Moderate batch sizes (hundreds, not automatically 1,000) with small payloads per
      op.
- [ ] Reuse singleton clients; per-operation clients defeat connection and buffer
      reuse.
- [ ] Long-running workers: watch Gen2/LOH counters alongside the SDK's meters; if LOH
      grows, suspect page width or batch payload size first.
