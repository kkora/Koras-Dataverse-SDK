# Configuration Reference: Validation

Invalid configuration fails **before** the first Dataverse request — at host startup under DI,
or at construction with `DataverseClient.Create`.

## What is enforced

| Check | Rule | Failure message (excerpt) |
|---|---|---|
| Environment URL present | `EnvironmentUrl` must be set | `DataverseClientOptions.EnvironmentUrl is required (for example https://contoso.crm.dynamics.com).` |
| Environment URL shape | absolute URI, scheme `https` | `DataverseClientOptions.EnvironmentUrl must be an absolute HTTPS URL; '<value>' is not.` |
| API version | non-blank, starts with `v` | `DataverseClientOptions.ApiVersion must look like 'v9.2'; '<value>' is not.` |
| Timeout | greater than zero | `DataverseClientOptions.Timeout must be positive.` |
| Retry attempts | `MaxRetries` between 0 and 10 | `DataverseRetryOptions.MaxRetries must be between 0 and 10.` |
| Retry delays | `BaseDelay > 0` and `MaxDelay >= BaseDelay` | `DataverseRetryOptions delays must be positive and MaxDelay must be at least BaseDelay.` |
| Client-secret auth | tenant id, client id, and secret all non-blank | `Client-secret authentication requires TenantId, ClientId, and ClientSecret.` |
| Certificate auth | tenant id, client id, and certificate present | `Certificate authentication requires TenantId, ClientId, and a certificate.` |
| Token credential | credential instance present | `UseTokenCredential requires a credential instance.` |
| Token provider | provider instance present | `UseTokenProvider requires a provider instance.` |

The HTTPS requirement is a security control, not pedantry: a plain-HTTP environment URL would
send bearer tokens in cleartext, so it is rejected outright.

Note the `Use…` methods themselves also validate their arguments eagerly —
`UseClientSecret("", …)` throws `ArgumentException` at the call site, before options
validation even runs.

## When validation runs

### Under dependency injection: at startup

`AddDataverse` registers the options with `ValidateOnStart()` and bridges the SDK's validation
into the options pipeline. When the host starts (before serving traffic), every registered
client's options are validated. Failures throw `OptionsValidationException` with a message that
names the client:

```text
Microsoft.Extensions.Options.OptionsValidationException:
  Dataverse client 'crm-prod' configuration is invalid:
  DataverseClientOptions.EnvironmentUrl must be an absolute HTTPS URL; 'http://contoso.crm.dynamics.com/' is not.
```

The host fails to start — a misconfigured deployment is caught by the rollout, not by the
first user. This covers **all** named clients, including ones no code has resolved yet.

The options are additionally validated when each client's HTTP pipeline is first built, so
even a client resolved lazily can never run with invalid options.

### Outside DI: at construction

`DataverseClient.Create(options)` (and the `DataverseClient` constructor) run the same
validation immediately and throw `InvalidOperationException` with the messages from the table
above:

```csharp
var options = new DataverseClientOptions(); // EnvironmentUrl missing
using var client = DataverseClient.Create(options);
// throws InvalidOperationException: DataverseClientOptions.EnvironmentUrl is required (…)
```

## What validation does not do

- It does not contact Dataverse or Entra ID — a *valid-looking* but wrong URL, or an expired
  secret, surfaces on the first call as a `DataverseException`
  (`Network`/`Authentication`/`Authorization`). Add the
  [health check](../guides/health-checks.md) for a startup-adjacent live probe.
- It does not validate `EntitySetNameOverrides` values — a wrong override surfaces as
  `NotFound` on first use.
