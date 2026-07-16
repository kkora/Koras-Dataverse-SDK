# Troubleshooting: Common Errors

Symptom → category → cause → fix, for the failures nearly every Dataverse integration meets.
Always capture `DataverseException.Error.RequestId` when reporting issues — see
[diagnostics](diagnostics.md).

| Symptom | Category (HTTP) | Likely cause | Fix |
|---|---|---|---|
| Every call fails immediately after working for weeks | `Authentication` (401) | **Client secret expired** (default Entra ID secrets expire after 6–24 months) | Create a new secret on the app registration, rotate it into your secret store. Longer term: move to certificate or managed identity |
| 401 on first-ever call | `Authentication` (401) | Wrong tenant id / client id, or token issued for the wrong resource | Verify tenant and client ids; verify `EnvironmentUrl` matches the environment the app registration should access |
| 403 on every call, token acquisition succeeds | `Authorization` (403) | The app registration/managed identity is **not an application user** in this environment, or has no security role | Power Platform admin center → environment → Settings → Users + permissions → Application users → add the app and assign a role |
| 403 only on some tables/operations | `Authorization` (403) | Security role lacks privileges for those tables | Extend the role (least privilege — add only what is needed) |
| 404 on a table you know exists | `NotFound` (404) | **Wrong entity set name** — the table's set name doesn't follow standard pluralization, so the client's derived name misses | Look up the real name (`await client.Metadata.GetEntitySetNameAsync("table", ct)`) and add `options.EntitySetNameOverrides["table"] = "actualsetname"` |
| 404 on a specific row | `NotFound` (404) | Row deleted, or id from another environment | Often a normal business case — handle the category, don't treat as outage |
| Bursts of slow calls, then failures | `Throttling` (429) | Dataverse **service protection limits** (requests/execution-time/concurrency per user per 5 minutes) | The SDK already retries honoring `Retry-After`. Reduce parallelism, batch writes, spread load; for sustained bulk work see [provider errors](provider-errors.md) |
| Startup failure: `…EnvironmentUrl must be an absolute HTTPS URL` | — (config validation) | URL typo, missing scheme, or `http://` | Use the full HTTPS URL, e.g. `https://contoso.crm.dynamics.com`. HTTP is rejected by design (bearer tokens) |
| `Network` failures, DNS errors | `Network` (no response) | Host name typo (`contoso.dynamics.com` vs `contoso.crm.dynamics.com`), proxy/firewall, DNS | Verify the URL opens in a browser from the same network; check egress rules for `*.dynamics.com` and `login.microsoftonline.com` |
| `Failed to acquire a Dataverse access token … AADSTS7000215` | `Authentication` | Invalid client secret (wrong value, or the secret *id* was pasted instead of the value) | Paste the secret **value** (shown only at creation time); create a new one if lost |
| `…AADSTS700016` | `Authentication` | Application not found in the tenant | Wrong `ClientId`, or wrong `TenantId` for that app |
| `…AADSTS90002` | `Authentication` | Tenant not found | Fix `TenantId` (GUID or `contoso.onmicrosoft.com`) |
| `…AADSTS50126` / interactive login loops | `Authentication` | User credentials invalid, or conditional access blocks the sign-in | Fix credentials; for CA-blocked service scenarios use an app identity instead of interactive |
| `Timeout` after ~100 s on solution import/export | `Timeout` | Default `Timeout` too small for long-running solution operations | Raise `options.Timeout` (e.g. 15 minutes) on a dedicated client for solution work |
| 400 with a business-rule message | `Validation` (400) | Bad payload: wrong column name, wrong type, violated business rule, malformed query | Read `Error.Message` (Dataverse's text is usually specific) and `Error.ErrorCode`; fix the data or query |
| 409 / 412 | `Concurrency` | Duplicate detection or optimistic-concurrency conflict | Re-read the row and reconcile; for duplicates, review duplicate-detection rules |
| 500 `Server` errors, not retried | `Server` (500) | Dataverse-side fault (sometimes triggered by a specific payload, e.g. plugin exceptions) | 500 is not auto-retried (it is not marked transient). Check plugin/flow errors in the environment; report with the request id if unexplained |
| 502/503/504 exhausting retries | `Server` (transient) | Dataverse instance restarting/degraded | The SDK retried already; check the service health dashboard, retry the workload later |

More on AADSTS codes and service-protection mechanics: [provider errors](provider-errors.md).
Error-handling patterns in code: [concepts/error-handling](../concepts/error-handling.md).
