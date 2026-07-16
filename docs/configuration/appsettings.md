# Configuration Reference: appsettings.json

The SDK does not bind configuration itself — you read `IConfiguration` inside the
`AddDataverse` lambda. This page collects the standard patterns.

## Basic layout

`appsettings.json` — **non-secret** values only:

```json
{
  "Dataverse": {
    "EnvironmentUrl": "https://contoso.crm.dynamics.com",
    "TenantId": "11111111-1111-1111-1111-111111111111",
    "ClientId": "22222222-2222-2222-2222-222222222222",
    "Timeout": "00:00:30",
    "IncludeAnnotations": true,
    "Retry": {
      "MaxRetries": 3,
      "BaseDelay": "00:00:01",
      "MaxDelay": "00:00:30",
      "RespectRetryAfter": true
    }
  }
}
```

Binding:

```csharp
IConfigurationSection config = builder.Configuration.GetSection("Dataverse");

builder.Services.AddDataverse(options =>
{
    options.EnvironmentUrl = new Uri(config["EnvironmentUrl"]!);
    options.Timeout = config.GetValue("Timeout", TimeSpan.FromSeconds(100));
    options.IncludeAnnotations = config.GetValue("IncludeAnnotations", true);

    options.Retry.MaxRetries = config.GetValue("Retry:MaxRetries", 3);
    options.Retry.BaseDelay = config.GetValue("Retry:BaseDelay", TimeSpan.FromSeconds(1));
    options.Retry.MaxDelay = config.GetValue("Retry:MaxDelay", TimeSpan.FromSeconds(30));
    options.Retry.RespectRetryAfter = config.GetValue("Retry:RespectRetryAfter", true);

    options.Authentication.UseClientSecret(
        config["TenantId"]!,
        config["ClientId"]!,
        config["ClientSecret"]!);
});
```

Explicit binding (rather than `section.Bind(options)`) is deliberate: authentication is
selected through methods, not settable properties, so a bind call cannot configure it —
and explicit code makes the configuration surface visible and reviewable. `TimeSpan` values
use the `"hh:mm:ss"` string form.

## Where the secret goes

`ClientSecret` must never sit in `appsettings.json` (it gets committed) or
`appsettings.Production.json` (it gets deployed and diffed). In order of preference:

1. **No secret at all** — `UseManagedIdentity()` in Azure; delete `ClientSecret` from the
   picture entirely.
2. **Development** — user-secrets: `dotnet user-secrets set "Dataverse:ClientSecret" "<secret>"`.
   It merges into the same `Dataverse:` section; the binding code above doesn't change.
3. **Production** — Key Vault via the configuration provider, or a platform secret surfaced as
   the environment variable `Dataverse__ClientSecret` (the host maps it into
   `Dataverse:ClientSecret` automatically). Again, the binding code doesn't change.

That is the payoff of the pattern: one binding block, and *where* values come from is decided
per environment by configuration providers.

## Per-environment overrides

`appsettings.Development.json` can point at a dev environment and relax budgets:

```json
{
  "Dataverse": {
    "EnvironmentUrl": "https://contoso-dev.crm.dynamics.com",
    "Timeout": "00:01:40"
  }
}
```

## Named clients

Give each client its own section:

```json
{
  "Dataverse": {
    "crm-prod": { "EnvironmentUrl": "https://contoso.crm.dynamics.com" },
    "crm-uat":  { "EnvironmentUrl": "https://contoso-uat.crm.dynamics.com" }
  }
}
```

```csharp
foreach (string name in new[] { "crm-prod", "crm-uat" })
{
    IConfigurationSection section = builder.Configuration.GetSection($"Dataverse:{name}");
    builder.Services.AddDataverse(name, options =>
    {
        options.EnvironmentUrl = new Uri(section["EnvironmentUrl"]!);
        options.Authentication.UseManagedIdentity();
    });
}
```

## Entity set name overrides

```json
{
  "Dataverse": {
    "EntitySetNameOverrides": {
      "new_metadata": "new_metadataset"
    }
  }
}
```

```csharp
foreach (IConfigurationSection entry in config.GetSection("EntitySetNameOverrides").GetChildren())
{
    options.EntitySetNameOverrides[entry.Key] = entry.Value!;
}
```

## Validation catches binding mistakes

A missing `EnvironmentUrl`, a `Timeout` of zero, or an incomplete credential set fails at
startup via `ValidateOnStart` with a message naming the client and the problem — see
[validation](validation.md).
