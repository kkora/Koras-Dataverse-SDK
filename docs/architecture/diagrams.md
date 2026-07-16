# Architecture Diagrams

> Visual companion to [`overview.md`](overview.md) and §2/§5 of
> [`docs/planning/master-plan.md`](../planning/master-plan.md). If a diagram and the master
> plan disagree, the master plan wins. All diagrams are Mermaid and render on GitHub.

## 1. Package dependency graph

```mermaid
graph TD
    APP["Consumer application"]
    OTELPKG["Koras.Dataverse.OpenTelemetry<br/>(OTel registration helpers)"]
    CORE["Koras.Dataverse<br/>(Web API client, DI, resilience, telemetry)"]
    ABS["Koras.Dataverse.Abstractions<br/>(interfaces, models, errors, options)"]
    FX["Koras.Dataverse.FetchXml<br/>(standalone builder, netstandard2.0)"]
    ORGSVC["Koras.Dataverse.OrganizationService<br/>(v1.1, optional transport)"]

    APP --> CORE
    APP -. "optional" .-> OTELPKG
    OTELPKG -->|"ids only"| CORE
    CORE --> ABS
    CORE --> FX
    ORGSVC -. "planned v1.1" .-> ABS

    AZID["Azure.Identity"]
    MEXT["Microsoft.Extensions.*<br/>(Http, Options, Logging.Abstractions, DI.Abstractions, HealthChecks.Abstractions)"]
    OTELAPI["OpenTelemetry.Api"]

    CORE --> AZID
    CORE --> MEXT
    OTELPKG --> OTELAPI
```

`Abstractions` and `FetchXml` have zero dependencies. Nothing depends on the implementation
package except the OpenTelemetry helper (name constants only).

## 2. Component architecture

```mermaid
graph TB
    subgraph Consumer["Consumer application"]
        SVC["Application services<br/>(inject IDataverseClient)"]
    end

    subgraph DI["DI / options layer (Koras.Dataverse)"]
        ADD["AddDataverse / AddDataverseHealthCheck"]
        OPT["DataverseClientOptions<br/>+ DataAnnotations validation"]
        FACT["IDataverseClientFactory<br/>(named clients)"]
    end

    subgraph Core["Core client (Koras.Dataverse)"]
        CLIENT["DataverseClient<br/>CRUD · OData · FetchXML · $batch · paging"]
        META["Metadata client"]
        SOL["Solution client"]
        ERR["Error mapper<br/>(OData payload → DataverseError)"]
        TEL["Telemetry<br/>ActivitySource + Meter 'Koras.Dataverse'"]
        HC["Health check (WhoAmI)"]
    end

    subgraph Pipeline["HTTP pipeline (IHttpClientFactory, named 'Koras.Dataverse:{name}')"]
        AUTH["AuthenticationHandler"]
        RETRY["RetryHandler<br/>429/503/504 · Retry-After · jittered backoff"]
        USER["User DelegatingHandlers"]
        HTTP["HttpClient primary handler"]
    end

    TP["IDataverseTokenProvider<br/>default: TokenCredential adapter<br/>(cached, single-flight refresh)"]
    WEBAPI["Dataverse Web API v9.2"]

    SVC --> ADD
    ADD --> OPT
    ADD --> FACT
    FACT --> CLIENT
    SVC -->|"IDataverseClient"| CLIENT
    CLIENT --> META
    CLIENT --> SOL
    CLIENT --> ERR
    CLIENT --> TEL
    HC --> CLIENT
    CLIENT --> AUTH
    META --> AUTH
    SOL --> AUTH
    AUTH --> TP
    AUTH --> RETRY
    RETRY --> USER
    USER --> HTTP
    HTTP --> WEBAPI
```

## 3. Request lifecycle (with retry loop and token acquisition)

```mermaid
sequenceDiagram
    autonumber
    participant App as Consumer code
    participant C as DataverseClient
    participant A as AuthenticationHandler
    participant T as IDataverseTokenProvider
    participant R as RetryHandler
    participant N as HttpClient / network
    participant D as Dataverse Web API v9.2

    App->>C: CreateAsync(entity, ct)
    activate C
    C->>C: Start Activity "dataverse.execute"<br/>link ct + per-request timeout (linked CTS)
    C->>C: Build request (URL, headers, @odata.bind payload)
    C->>A: Send request
    A->>T: GetTokenAsync(scope, ct)
    alt Cached token still valid (> 5 min to expiry)
        T-->>A: cached token
    else Expired or near expiry
        T->>T: single-flight refresh via TokenCredential
        T-->>A: fresh token
    end
    A->>R: request + Authorization header

    loop Until success, non-retryable status, attempts exhausted, or ct canceled
        R->>N: attempt request
        N->>D: HTTPS
        D-->>N: response
        N-->>R: response
        alt 429 / 503 / 504
            R->>R: delay = Retry-After ?? jittered backoff (TimeProvider)
            Note over R: increments koras.dataverse.client.retries<br/>and .throttles metrics
        else Success or non-retryable
            R-->>A: final response
        end
    end

    A-->>C: final response
    alt Success (2xx)
        C->>C: Parse payload → plain CLR values
        C->>C: Stop activity (ok) · record operation metrics
        C-->>App: result (e.g., Guid)
    else Failure
        C->>C: Parse OData error → DataverseError
        C->>C: Stop activity (error) · record operation metrics
        C-->>App: throw DataverseException
    end
    deactivate C
```

## 4. Provider / auth lifecycle

```mermaid
sequenceDiagram
    autonumber
    participant H as AuthenticationHandler
    participant P as Default token provider
    participant CC as Token cache
    participant TC as Azure TokenCredential
    participant AAD as Microsoft Entra ID

    H->>P: GetTokenAsync("{environmentUrl}/.default", ct)
    P->>CC: lookup(scope)
    alt Token present and expires in > 5 minutes (TimeProvider)
        CC-->>P: cached token
        P-->>H: token
    else Missing / near expiry
        P->>P: acquire single-flight lock for scope
        alt Another caller already refreshing
            P->>P: await in-flight refresh
        else This caller refreshes
            P->>TC: GetTokenAsync(scope, ct)
            TC->>AAD: credential flow<br/>(secret / certificate / managed identity / interactive / default)
            AAD-->>TC: access token + expiry
            TC-->>P: AccessToken
            P->>CC: store(scope, token, expiry)
        end
        P-->>H: token
    end
    H->>H: attach "Authorization: Bearer …"
    Note over H,P: Tokens are never logged.<br/>Custom IDataverseTokenProvider replaces P entirely.
```

## 5. Error lifecycle

```mermaid
flowchart TD
    START(["HTTP attempt completes or faults"]) --> CTQ{"Caller's<br/>CancellationToken<br/>canceled?"}
    CTQ -- "Yes" --> OCE["Propagate OperationCanceledException<br/>(never wrapped)"]
    CTQ -- "No" --> RESP{"Response<br/>received?"}

    RESP -- "No (DNS/TLS/reset)" --> NETRETRY{"Retry budget<br/>left?"}
    RESP -- "No (per-request timeout)" --> TIMEOUT["DataverseError<br/>Category = Timeout · IsTransient = true"]
    RESP -- "Yes" --> STATUS{"Status"}

    STATUS -- "2xx" --> OK(["Success — parse result"])
    STATUS -- "429 / 503 / 504" --> RETRYQ{"Retry budget<br/>left?"}
    RETRYQ -- "Yes" --> WAIT["Wait Retry-After or jittered backoff<br/>(TimeProvider, cancellable)"] --> START
    NETRETRY -- "Yes" --> WAIT
    RETRYQ -- "No" --> MAP
    NETRETRY -- "No" --> NETERR["DataverseError<br/>Category = Network · IsTransient = true"] --> THROW
    STATUS -- "Other non-success" --> MAP["Parse OData error payload"]

    MAP --> CODEQ{"Known Dataverse<br/>error code?"}
    CODEQ -- "Yes" --> BYCODE["Category from code<br/>(e.g., 0x80072322 → Throttling,<br/>concurrency codes → Concurrency)"]
    CODEQ -- "No" --> BYSTATUS["Category from HTTP status<br/>401→Authentication · 403→Authorization<br/>404→NotFound · 412→Concurrency<br/>429→Throttling · 400→Validation<br/>5xx→Server · else→Unknown"]

    BYCODE --> BUILD["Build DataverseError<br/>category · code · status · request id ·<br/>Retry-After · IsTransient"]
    BYSTATUS --> BUILD
    TIMEOUT --> THROW["throw DataverseException"]
    BUILD --> THROW
```

## 6. DI registration flow

```mermaid
flowchart TD
    A["services.AddDataverse(name?, configure)"] --> B["Bind + register DataverseClientOptions<br/>(named options)"]
    B --> C["Add DataAnnotations validation<br/>+ ValidateOnStart"]
    C --> D["Register IDataverseTokenProvider<br/>(default TokenCredential adapter,<br/>unless custom provider configured)"]
    D --> E["AddHttpClient('Koras.Dataverse:{name}')"]
    E --> F["Attach AuthenticationHandler"]
    F --> G["Attach RetryHandler"]
    G --> H["Attach user DelegatingHandlers<br/>(builder callback)"]
    H --> I["Register singletons:<br/>IDataverseClient · IMetadataClient · ISolutionClient"]
    I --> J["Register IDataverseClientFactory<br/>(resolves clients by name)"]
    J --> K{"AddDataverseHealthCheck()?"}
    K -- "Yes" --> L["Register WhoAmI health check"]
    K -- "No" --> M(["Ready — first resolve validates options"])
    L --> M
```

## 7. Telemetry flow

```mermaid
flowchart LR
    subgraph CORE["Koras.Dataverse (BCL primitives only)"]
        OP["Client operation"]
        ACT["ActivitySource 'Koras.Dataverse'<br/>span: dataverse.execute<br/>tags: dataverse.operation · dataverse.table ·<br/>http.response.status_code · dataverse.request_id"]
        MET["Meter 'Koras.Dataverse'<br/>…client.operations (counter)<br/>…client.operation.duration (histogram, s)<br/>…client.retries (counter)<br/>…client.throttles (counter)"]
        LOG["ILogger categories<br/>'Koras.Dataverse' · 'Koras.Dataverse.Http'"]
    end

    subgraph OTELPKG["Koras.Dataverse.OpenTelemetry"]
        TRB["TracerProviderBuilder<br/>.AddDataverseInstrumentation()"]
        MRB["MeterProviderBuilder<br/>.AddDataverseInstrumentation()"]
    end

    subgraph BACKENDS["Consumer's pipeline"]
        OTELSDK["OpenTelemetry SDK + exporters<br/>(OTLP, Prometheus, …)"]
        LOGSINK["Logging providers<br/>(console, Seq, App Insights, …)"]
        DIAG["ActivityListener / MeterListener /<br/>dotnet-counters (no OTel needed)"]
    end

    OP --> ACT
    OP --> MET
    OP --> LOG
    TRB -- "AddSource('Koras.Dataverse')" --> OTELSDK
    MRB -- "AddMeter('Koras.Dataverse')" --> OTELSDK
    ACT --> OTELSDK
    MET --> OTELSDK
    ACT --> DIAG
    MET --> DIAG
    LOG --> LOGSINK
```
