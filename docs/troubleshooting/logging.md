# Troubleshooting: Log Filtering

The SDK logs on exactly two categories:

- `Koras.Dataverse` — client layer; `Error` on operation failure.
- `Koras.Dataverse.Http` — retry handler; `Warning` per retry attempt.

What they emit (and what is never logged) is documented in the
[logging guide](../guides/logging.md). This page is filter recipes.

## appsettings.json filters

Production baseline — retries and failures visible, nothing else:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Koras.Dataverse": "Warning",
      "Koras.Dataverse.Http": "Warning"
    }
  }
}
```

Silence retry noise during a known throttling window (metrics still count retries):

```json
{
  "Logging": {
    "LogLevel": {
      "Koras.Dataverse": "Warning",
      "Koras.Dataverse.Http": "Error"
    }
  }
}
```

Investigation mode — everything the SDK emits, plus HTTP-stack logs around it:

```json
{
  "Logging": {
    "LogLevel": {
      "Koras.Dataverse": "Debug",
      "Koras.Dataverse.Http": "Debug",
      "System.Net.Http.HttpClient": "Information"
    }
  }
}
```

The `System.Net.Http.HttpClient.*` categories come from `IHttpClientFactory` logging (request
start/stop and duration per named client, including the SDK's `Koras.Dataverse:Default`
client). They are separate from the SDK's categories but useful alongside them. Be aware that
those framework logs include full request URIs — which for queries contain `$filter`
expressions. The SDK's own log lines never include query strings; if filter literals are
sensitive in your logs, keep `System.Net.Http.HttpClient` at `Warning`.

Per-provider filtering (e.g. verbose to a file sink, quiet on console):

```json
{
  "Logging": {
    "Console": {
      "LogLevel": {
        "Koras.Dataverse.Http": "Error"
      }
    },
    "LogLevel": {
      "Koras.Dataverse": "Warning",
      "Koras.Dataverse.Http": "Warning"
    }
  }
}
```

## Code-based filters

```csharp
builder.Logging.AddFilter("Koras.Dataverse", LogLevel.Warning);
builder.Logging.AddFilter("Koras.Dataverse.Http", LogLevel.Warning);
```

Both SDK categories share the `Koras.Dataverse` prefix, so a single prefix filter covers them:

```csharp
builder.Logging.AddFilter((category, level) =>
    category?.StartsWith("Koras.Dataverse", StringComparison.Ordinal) != true
    || level >= LogLevel.Warning);
```

## Serilog

```csharp
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Koras.Dataverse", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Koras.Dataverse.Http", Serilog.Events.LogEventLevel.Warning)
    .WriteTo.Console()
    .CreateLogger();
```

## Console tools

Filtering only matters if a logger factory was passed to `DataverseClient.Create` — without
one, the SDK is silent:

```csharp
using ILoggerFactory loggerFactory = LoggerFactory.Create(b => b
    .AddSimpleConsole()
    .AddFilter("Koras.Dataverse.Http", LogLevel.Warning)
    .SetMinimumLevel(LogLevel.Information));

using var client = DataverseClient.Create(options, loggerFactory);
```
