using System.Collections.Concurrent;
using System.Net;
using Koras.Dataverse.Authentication;
using Koras.Dataverse.Errors;
using Koras.Dataverse.UnitTests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace Koras.Dataverse.UnitTests.Diagnostics;

/// <summary>
/// Security checklist §2 / data-protection §2.3: no bearer-token material may reach any log
/// output on any path — success, failure (even when the server echoes the token back in the
/// error body), or retry — at any log level including Trace.
/// </summary>
public class LogRedactionTests
{
    private const string TokenSentinel = "REDACTION-SENTINEL-TOKEN-0f9e8d7c6b5a";

    [Fact]
    public async Task No_log_output_contains_bearer_token_material_on_any_path()
    {
        string whoAmIBody =
            "{\"BusinessUnitId\":\"" + Guid.NewGuid() + "\",\"UserId\":\"" + Guid.NewGuid() +
            "\",\"OrganizationId\":\"" + Guid.NewGuid() + "\"}";
        string echoingErrorBody =
            "{\"error\":{\"code\":\"0x1\",\"message\":\"token was " + TokenSentinel + "\"}}";

        var fake = new FakeHttpMessageHandler();
        // 1. Success (WhoAmI).
        fake.Enqueue(_ => FakeHttpMessageHandler.Json(HttpStatusCode.OK, whoAmIBody));
        // 2. Failure whose body hostilely echoes the bearer token.
        fake.Enqueue(_ => FakeHttpMessageHandler.Json(HttpStatusCode.BadRequest, echoingErrorBody));
        // 3. Throttled once (exercises the retry-handler log), then success.
        fake.Enqueue(_ =>
        {
            HttpResponseMessage throttled = FakeHttpMessageHandler.Json((HttpStatusCode)429, """{"error":{"code":"0x80072322","message":"throttled"}}""");
            throttled.Headers.TryAddWithoutValidation("Retry-After", "0");
            return throttled;
        });
        fake.Enqueue(_ => FakeHttpMessageHandler.Json(HttpStatusCode.OK, whoAmIBody));

        var capture = new CapturingLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.AddProvider(capture).SetMinimumLevel(LogLevel.Trace));
        services.AddDataverse(o =>
        {
            o.EnvironmentUrl = new Uri("https://redaction.crm.dynamics.com");
            o.Authentication.UseTokenProvider(new SentinelTokenProvider());
            o.Retry.BaseDelay = TimeSpan.FromMilliseconds(1);
            o.Retry.MaxDelay = TimeSpan.FromMilliseconds(1);
        });
        services.ConfigureAll<HttpClientFactoryOptions>(o =>
            o.HttpMessageHandlerBuilderActions.Add(b => b.PrimaryHandler = fake));

        await using ServiceProvider provider = services.BuildServiceProvider();
        IDataverseClient client = provider.GetRequiredService<IDataverseClient>();

        await client.WhoAmIAsync();
        await Assert.ThrowsAsync<DataverseException>(() => client.WhoAmIAsync());
        await client.WhoAmIAsync(); // 429 → retry → success

        Assert.NotEmpty(capture.Lines);
        foreach (string line in capture.Lines)
        {
            Assert.DoesNotContain(TokenSentinel, line, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Bearer ", line, StringComparison.Ordinal);
        }
    }

    private sealed class SentinelTokenProvider : IDataverseTokenProvider
    {
        public ValueTask<string> GetAccessTokenAsync(Uri environmentUrl, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(TokenSentinel);
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<string> Lines { get; } = new();

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, Lines);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(string category, ConcurrentQueue<string> lines) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                lines.Enqueue($"{category}|{formatter(state, exception)}|{state}|{exception}");
        }
    }
}
