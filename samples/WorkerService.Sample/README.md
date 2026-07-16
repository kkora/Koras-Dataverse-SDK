# Worker Service sample

A background job that streams all active accounts without a description
(`QueryAllAsync`, server paging) and back-fills them in atomic batches of 100
(`ExecuteBatchAsync`). Shows shutdown-safe cancellation and throttling-friendly settings.

## Setup

1. Set `Dataverse:EnvironmentUrl` in `appsettings.json`.
2. Authenticate via `az login` locally (DefaultAzureCredential) or a managed identity in Azure.

> ⚠️ This sample **writes** to the `description` column of active accounts. Run it against a
> developer environment, not production.

## Run

```bash
dotnet run --project samples/WorkerService.Sample
```

## Expected output

```text
info: Koras.Dataverse.Samples.WorkerService.AccountSweepWorker[0]
      Updated 100 accounts so far.
info: …
      Sweep complete: 217 accounts updated.
```

## Error scenarios

Service-protection limits (HTTP 429) are retried automatically with `Retry-After`; the sample
raises `Retry.MaxRetries` to 5 for long-running batch work. Hard failures are logged with the
normalized category and Dataverse request id.

Docs: [worker service guide](../../docs/guides/worker-service.md) ·
[batch operations](../../docs/features/batch-operations.md)

To consume released packages instead of the local projects, replace the `ProjectReference`
with `<PackageReference Include="Koras.Dataverse" Version="*" />`.
