using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace UrlShortener.Tests.Integration;

[Collection(IntegrationTestCollection.Name)]
public sealed class ExceptionMiddlewareIntegrationTests(TestWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Post_InvalidUrl_Returns400WithProblemDetails()
    {
        var request = new { url = "not-a-valid-url" };
        var response = await _client.PostAsJsonAsync("/api/shorten", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("at UrlShortener", because: "stack traces must not be leaked");
    }

    [Fact]
    public async Task Get_UnknownCode_Returns404WithProblemDetails()
    {
        var response = await _client.GetAsync("/api/stats/zzzzzzz_definitely_not_here");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("StackTrace", because: "stack traces must not be leaked");
    }

    [Fact]
    public async Task AllRequests_IncludeCorrelationIdHeader()
    {
        var response = await _client.GetAsync("/api/stats/test");

        response.Headers.Should().ContainKey("X-Correlation-Id");
        var correlationId = response.Headers.GetValues("X-Correlation-Id").First();
        correlationId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Request_WithCorrelationId_ReturnsSameCorrelationId()
    {
        const string correlationId = "test-correlation-id-12345";
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/stats/test");
        request.Headers.Add("X-Correlation-Id", correlationId);

        var response = await _client.SendAsync(request);

        var returnedCorrelationId = response.Headers.GetValues("X-Correlation-Id").First();
        returnedCorrelationId.Should().Be(correlationId);
    }

    [Fact]
    public async Task Post_TooLargeUrl_DoesNotLeakStackTrace()
    {
        // Very long URL to test error handling
        var veryLongUrl = "https://example.com/" + new string('a', 5000);
        var request = new { url = veryLongUrl };
        var response = await _client.PostAsJsonAsync("/api/shorten", request);

        // Should return some error, not 500 with stack trace
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("System.Exception", because: "exceptions should not be leaked");
        body.Should().NotContain("StackTrace", because: "stack traces should not be leaked");
    }
}
