using System.Net;
using Koras.Dataverse.Errors;
using Koras.Dataverse.UnitTests.TestInfrastructure;

namespace Koras.Dataverse.UnitTests.Errors;

public class DataverseErrorParserTests
{
    [Theory]
    [InlineData(400, DataverseErrorCategory.Validation, false)]
    [InlineData(401, DataverseErrorCategory.Authentication, false)]
    [InlineData(403, DataverseErrorCategory.Authorization, false)]
    [InlineData(404, DataverseErrorCategory.NotFound, false)]
    [InlineData(408, DataverseErrorCategory.Timeout, true)]
    [InlineData(409, DataverseErrorCategory.Concurrency, false)]
    [InlineData(412, DataverseErrorCategory.Concurrency, false)]
    [InlineData(429, DataverseErrorCategory.Throttling, true)]
    [InlineData(500, DataverseErrorCategory.Server, false)]
    [InlineData(502, DataverseErrorCategory.Server, true)]
    [InlineData(503, DataverseErrorCategory.Server, true)]
    [InlineData(504, DataverseErrorCategory.Server, true)]
    [InlineData(418, DataverseErrorCategory.Unknown, false)]
    public void Status_codes_map_to_categories(int status, DataverseErrorCategory expected, bool transient)
    {
        DataverseError error = DataverseErrorParser.Create(status, null, null, null, null);
        Assert.Equal(expected, error.Category);
        Assert.Equal(transient, error.IsTransient);
        Assert.Equal(status, error.HttpStatusCode);
    }

    [Fact]
    public async Task Response_body_code_message_and_headers_are_captured()
    {
        HttpResponseMessage response = FakeHttpMessageHandler.Json(
            (HttpStatusCode)429,
            """{"error":{"code":"0x80072322","message":"Rate limit is exceeded."}}""");
        response.Headers.Add("x-ms-service-request-id", "req-123");
        response.Headers.Add("Retry-After", "42");

        DataverseError error = await DataverseErrorParser.FromResponseAsync(response, CancellationToken.None);

        Assert.Equal(DataverseErrorCategory.Throttling, error.Category);
        Assert.Equal("0x80072322", error.ErrorCode);
        Assert.Equal("Rate limit is exceeded.", error.Message);
        Assert.Equal("req-123", error.RequestId);
        Assert.Equal(TimeSpan.FromSeconds(42), error.RetryAfter);
        Assert.True(error.IsTransient);
    }

    [Fact]
    public async Task Unparseable_bodies_fall_back_to_status_classification()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = new StringContent("<html>bad gateway</html>"),
        };

        DataverseError error = await DataverseErrorParser.FromResponseAsync(response, CancellationToken.None);
        Assert.Equal(DataverseErrorCategory.Server, error.Category);
        Assert.Contains("502", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Exception_exposes_error_and_shortcuts()
    {
        DataverseError error = DataverseErrorParser.Create(404, "0x80040217", "does not exist", "r", null);
        var exception = new DataverseException(error);
        Assert.Equal(DataverseErrorCategory.NotFound, exception.Category);
        Assert.False(exception.IsTransient);
        Assert.Equal("does not exist", exception.Message);
        Assert.Same(error, exception.Error);
    }
}
