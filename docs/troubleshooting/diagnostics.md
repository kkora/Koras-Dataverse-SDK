# Troubleshooting: Diagnostics

How to see what the SDK is doing, capture the evidence, and hand Microsoft support something
they can act on.

## Step 1: turn up logging

Enable both SDK categories at `Debug`/`Information` while diagnosing (the SDK's own events are
at `Warning`/`Error`, so `Warning` already shows everything it emits — lowering the level
mainly admits surrounding framework noise you may also want):

```json
{
  "Logging": {
    "LogLevel": {
      "Koras.Dataverse": "Debug",
      "Koras.Dataverse.Http": "Debug"
    }
  }
}
```

You will see:

- one **warning per retry** (`Koras.Dataverse.Http`): method, path, reason (HTTP status or
  exception type), computed delay, attempt counter — this is how you *see* throttling being
  absorbed;
- one **error per failed operation** (`Koras.Dataverse`): operation, table, category, HTTP
  status, Dataverse error code, and the **request id**.

In a console tool, pass a logger factory or nothing appears:

```csharp
using ILoggerFactory loggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Debug));
using var client = DataverseClient.Create(options, loggerFactory);
```

For request-level detail beyond these events (headers, timing per attempt), add a diagnostic
`DelegatingHandler` — recipe in [advanced scenarios](../recipes/advanced-scenarios.md) — or use
tracing, which records every operation as a `dataverse.*` span
([telemetry guide](../guides/telemetry.md)).

## Step 2: capture request ids

Every failure carries Dataverse's service request id
(`x-ms-service-request-id`). It is available in three places, always the same value:

1. `DataverseException.Error.RequestId` — log it in your own error handling:

   ```csharp
   catch (DataverseException exception)
   {
       logger.LogError(exception,
           "Dataverse {Category} failure. RequestId={RequestId} Code={Code} Http={Http}",
           exception.Category, exception.Error.RequestId,
           exception.Error.ErrorCode, exception.Error.HttpStatusCode);
       throw;
   }
   ```

2. The SDK's own failure log line on `Koras.Dataverse`.
3. The `dataverse.request_id` tag on the failed telemetry span.

Also record the **UTC timestamp**, the **environment URL**, the **operation and table**, and
`Error.ErrorCode` — together with the request id this is the complete correlation packet.

## Step 3: reproduce minimally

`WhoAmIAsync` is the cheapest full-stack probe — configuration, DNS/TLS, token acquisition,
and authorization in one call:

```csharp
try
{
    WhoAmIResponse who = await client.WhoAmIAsync(ct);
    Console.WriteLine($"OK: user {who.UserId}, org {who.OrganizationId}");
}
catch (DataverseException exception)
{
    Console.WriteLine($"FAILED: {exception.Error}"); // one-line summary incl. category/status/code
}
```

If `WhoAmI` succeeds but a specific operation fails, the problem is privileges, data, or the
request itself — not connectivity or credentials.

## Step 4: correlating with Microsoft support

When opening a Dataverse support case, provide:

- the **request id(s)** and exact **UTC timestamps** of failing calls,
- the **environment URL** and organization id (from `WhoAmIAsync`),
- the Dataverse **error code** (e.g. `0x80040217`) and message,
- whether failures are constant or intermittent, and whether retries were involved (your
  `Koras.Dataverse.Http` warnings show this).

The request id lets support find the server-side trace of your exact request — without it,
they can only search by time window. Note that request ids are only as useful as your clock
context: always report times in UTC.

## Quick reference: what signal lives where

| Question | Where to look |
|---|---|
| Is the SDK retrying? How often? | `Koras.Dataverse.Http` warnings; `koras.dataverse.client.retries` metric |
| Are we being throttled? | `koras.dataverse.client.throttles` metric; 429 retry warnings |
| Which operations fail, on which tables? | `koras.dataverse.client.operations` metric (`outcome`, `dataverse.operation`, `dataverse.table` tags) |
| How long do operations take, retries included? | `koras.dataverse.client.operation.duration` histogram; span durations |
| Why did this one call fail? | The exception's `Error`; the failed span's tags; the `Koras.Dataverse` error log line |
