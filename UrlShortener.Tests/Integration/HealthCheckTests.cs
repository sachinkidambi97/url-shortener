using System.Net;
using System.Text.Json;
using FluentAssertions;

namespace UrlShortener.Tests.Integration;

[Collection(IntegrationTestCollection.Name)]
public sealed class HealthCheckTests(TestWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Get_Health_Returns200WithHealthyStatus()
    {
        var response = await _client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Get_Health_ReturnsJsonWithStatusField()
    {
        var response = await _client.GetAsync("/health");

        var body = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(body);
        json.RootElement.TryGetProperty("status", out var statusElement).Should().BeTrue();
        var status = statusElement.GetString();
        status.Should().BeOneOf("healthy", "degraded", "unhealthy");
    }

    [Fact]
    public async Task Get_Health_ReturnsJsonWithChecksField()
    {
        var response = await _client.GetAsync("/health");

        var body = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(body);
        json.RootElement.TryGetProperty("checks", out var checksElement).Should().BeTrue();
        checksElement.ValueKind.Should().Be(JsonValueKind.Object);
    }

    [Fact]
    public async Task Get_Health_ResponseHasJsonContentType()
    {
        var response = await _client.GetAsync("/health");

        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task Get_Health_IsNotRateLimited()
    {
        // Health endpoint should not be subject to rate limiting
        // (it's mapped before MapControllers without EnableRateLimiting)
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => _client.GetAsync("/health"))
            .ToList();

        var responses = await Task.WhenAll(tasks);

        // All health check responses should succeed, not return 429
        responses.Should().AllSatisfy(r =>
            r.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests));
    }
}
