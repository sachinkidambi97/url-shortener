using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using UrlShortener.Api.Models.Dtos;

namespace UrlShortener.Tests.Integration;

[Collection(IntegrationTestCollection.Name)]
public sealed class ExpirationEndpointTests(TestWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();
    private readonly HttpClient _nonRedirectClient = factory.CreateNonRedirectingClient();

    [Fact]
    public async Task Post_WithFutureExpiresAt_Returns201()
    {
        await factory.ResetDatabaseAsync();

        var request = new ShortenRequest
        {
            Url = "https://expiring.example.com",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7)
        };
        var response = await _client.PostAsJsonAsync("/api/shorten", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Post_WithPastExpiresAt_Returns400()
    {
        var request = new ShortenRequest
        {
            Url = "https://example.com",
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(-1)
        };
        var response = await _client.PostAsJsonAsync("/api/shorten", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_WithNoExpiresAt_Returns201AndWorks()
    {
        await factory.ResetDatabaseAsync();
        await factory.ResetCacheAsync();

        var request = new ShortenRequest { Url = "https://no-expiry.example.com" };
        var createResponse = await _client.PostAsJsonAsync("/api/shorten", request);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await createResponse.Content.ReadFromJsonAsync<ShortenResponse>();
        var redirectResponse = await _nonRedirectClient.GetAsync($"/{body!.ShortCode}");
        redirectResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task Get_NonExpiredUrl_Returns302()
    {
        await factory.ResetDatabaseAsync();
        await factory.ResetCacheAsync();

        var request = new ShortenRequest
        {
            Url = "https://not-yet-expired.example.com",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(1)
        };
        var createResponse = await _client.PostAsJsonAsync("/api/shorten", request);
        var body = await createResponse.Content.ReadFromJsonAsync<ShortenResponse>();

        var redirectResponse = await _nonRedirectClient.GetAsync($"/{body!.ShortCode}");

        redirectResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
        redirectResponse.Headers.Location!.ToString().Should().StartWith("https://not-yet-expired.example.com");
    }
}
