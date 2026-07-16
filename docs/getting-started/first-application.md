# Your First Application

A complete, minimal console application that talks to Dataverse **without dependency
injection**, using `DataverseClient.Create`. Use this shape for scripts, admin tools, and
one-off utilities; for hosted applications prefer [dependency injection](dependency-injection.md).

## 1. Create the project

```bash
dotnet new console -n DataverseFirstApp
cd DataverseFirstApp
dotnet add package Koras.Dataverse
```

## 2. Program.cs

```csharp
using Koras.Dataverse;
using Koras.Dataverse.Errors;
using Koras.Dataverse.Queries;

// --- configure ---------------------------------------------------------
var options = new DataverseClientOptions
{
    EnvironmentUrl = new Uri("https://contoso.crm.dynamics.com"),
};

// Interactive browser sign-in: ideal for a personal admin tool.
// For unattended runs use UseClientSecret / UseCertificate / UseManagedIdentity instead.
options.Authentication.UseInteractive();

using var dataverse = DataverseClient.Create(options);

using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
CancellationToken ct = cts.Token;

try
{
    // --- who am I? ------------------------------------------------------
    WhoAmIResponse who = await dataverse.WhoAmIAsync(ct);
    Console.WriteLine($"Connected as user {who.UserId} in organization {who.OrganizationId}.");

    // --- create ---------------------------------------------------------
    var account = new Entity("account")
    {
        ["name"] = "First App Sample Account",
        ["revenue"] = 12_500m,
    };
    Guid accountId = await dataverse.CreateAsync(account, ct);
    Console.WriteLine($"Created account {accountId}.");

    // --- retrieve -------------------------------------------------------
    Entity fetched = await dataverse.RetrieveAsync("account", accountId, ColumnSet.Of("name", "revenue"), ct);
    Console.WriteLine($"Retrieved: {fetched.GetValue<string>("name")} (revenue {fetched.GetValue<decimal?>("revenue")}).");

    // --- query ----------------------------------------------------------
    var query = ODataQuery.For("account")
        .Select("name")
        .Where(f => f.StartsWith("name", "First App"))
        .OrderBy("name");

    await foreach (Entity row in dataverse.QueryAllAsync(query, ct))
    {
        Console.WriteLine($"  match: {row.GetValue<string>("name")} ({row.Id})");
    }

    // --- clean up -------------------------------------------------------
    await dataverse.DeleteAsync("account", accountId, ct);
    Console.WriteLine("Deleted the sample account.");
}
catch (DataverseException exception)
{
    Console.Error.WriteLine($"Dataverse call failed: {exception.Error}");
    Console.Error.WriteLine($"  Category:  {exception.Category}");
    Console.Error.WriteLine($"  RequestId: {exception.Error.RequestId ?? "-"}");
    return;
}
```

`DataverseClient.Create` builds a self-contained client: token acquisition (with caching),
retry handling, and HTTP resources are all wired internally. The returned client owns those
resources — dispose it when done (the `using` above).

## 3. Expected output

```text
Connected as user 3f2504e0-4f89-11d3-9a0c-0305e82c3301 in organization 8a9e...
Created account 0f3f3f3f-....
Retrieved: First App Sample Account (revenue 12500).
  match: First App Sample Account (0f3f3f3f-....)
Deleted the sample account.
```

With `UseInteractive()` a browser window opens for sign-in on the first call; subsequent calls
reuse the cached token.

## Common first errors

| Symptom | Meaning | Fix |
|---|---|---|
| `DataverseException`, `Category: Authentication` (HTTP 401) | The token was rejected — wrong tenant, expired secret, or the credential could not sign in | Check tenant/client id, regenerate the secret, confirm you can sign in to the environment in a browser |
| `DataverseException`, `Category: Authorization` (HTTP 403) | Authenticated, but the identity is not an application user in this environment, or has no security role | In the Power Platform admin center, add the app registration as an application user and assign a role |
| `InvalidOperationException: DataverseClientOptions.EnvironmentUrl is required …` at startup | `EnvironmentUrl` was not set | Set `options.EnvironmentUrl` |
| `InvalidOperationException: … must be an absolute HTTPS URL` | URL typo, or `http://` | Use the full HTTPS environment URL, e.g. `https://contoso.crm.dynamics.com` (no `/api/data` suffix) |
| `DataverseException`, `Category: Network` | DNS/TLS/socket failure before any HTTP response | Check the host name for typos (`contoso.crm.dynamics.com`, not `contoso.dynamics.com`) and your network/proxy |
| `DataverseException`, `Category: NotFound` on a query | Usually the entity set name could not be resolved for a table with irregular pluralization | See [`EntitySetNameOverrides`](../configuration/all-options.md) |

More in [Troubleshooting: common errors](../troubleshooting/common-errors.md).

## Where next

- [Console app guide](../guides/console-app.md) — a fuller admin-tool walkthrough
- [Dependency injection](dependency-injection.md) — the recommended setup for hosted apps
