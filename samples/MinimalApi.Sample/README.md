# Minimal API sample

Dataverse-backed HTTP endpoints: list/get/create accounts, a readiness health check
(`WhoAmI` probe), and SDK error normalization into JSON problem responses.

## Setup

1. Set the environment URL in `appsettings.json` (`Dataverse:EnvironmentUrl`).
2. Provide credentials via **user secrets** (never in appsettings):

   ```bash
   cd samples/MinimalApi.Sample
   dotnet user-secrets set "Dataverse:TenantId" "<tenant-guid>"
   dotnet user-secrets set "Dataverse:ClientId" "<app-client-id>"
   dotnet user-secrets set "Dataverse:ClientSecret" "<secret>"
   ```

   Omit the secrets entirely to fall back to `DefaultAzureCredential` (`az login`).

## Run

```bash
dotnet run --project samples/MinimalApi.Sample
```

Then:

```bash
curl http://localhost:5000/accounts?top=5
curl http://localhost:5000/health/ready
curl -X POST http://localhost:5000/accounts -H "Content-Type: application/json" -d '{"name":"Contoso"}'
```

## Expected output

`GET /accounts` returns a JSON array of `{ id, name, revenue }`;
`/health/ready` returns `Healthy` when the environment is reachable.

## Error scenarios

Failures surface as JSON problems with the normalized category, e.g. a deleted row returns
`404` with `"title": "Dataverse error: NotFound"` and the Dataverse request id for support.

Docs: [ASP.NET Core guide](../../docs/guides/aspnet-core.md) ·
[health checks](../../docs/guides/health-checks.md)

To consume released packages instead of the local projects, replace the `ProjectReference`
with `<PackageReference Include="Koras.Dataverse" Version="*" />`.
