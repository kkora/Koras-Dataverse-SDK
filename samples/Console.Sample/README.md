# Console sample

Connects to Dataverse **without dependency injection** (`DataverseClient.Create`), prints the
caller identity (`WhoAmI`), and lists the top five accounts.

## Setup

1. You need a Dataverse environment and a signed-in Azure identity that has a Dataverse user.
2. Sign in locally: `az login` (DefaultAzureCredential picks it up). Alternatively edit
   `Program.cs` to use `options.Authentication.UseInteractive()` for a browser prompt.
3. Set the environment URL (never commit real URLs or secrets):

   ```bash
   export DATAVERSE_URL="https://<yourorg>.crm.dynamics.com"
   ```

## Run

```bash
dotnet run --project samples/Console.Sample
```

## Expected output

```text
Connected as user 8f4d…  in organization 3c2a….
Top 5 accounts by name:
  Adventure Works  (revenue: $1,500,000.00)
  …
```

## Error scenarios

| Symptom | Meaning | Fix |
|---|---|---|
| `Authentication — Failed to acquire a Dataverse access token` | No usable credential | `az login`, or configure client secret auth |
| `Authorization — …` (HTTP 403) | Identity has no Dataverse application user/role | Create an application user and assign a role |
| `Network — …` | Wrong URL or no connectivity | Check `DATAVERSE_URL` |

Docs: [getting started](../../docs/getting-started/quick-start.md) ·
[error handling](../../docs/concepts/error-handling.md)

During development this sample references the local projects. To consume released packages
instead, replace the `ProjectReference` in the `.csproj` with
`<PackageReference Include="Koras.Dataverse" Version="*" />`.
