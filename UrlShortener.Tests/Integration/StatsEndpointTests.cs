using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using UrlShortener.Api.Models.Dtos;

namespace UrlShortener.Tests.Integration;

[Collection(IntegrationTestCollection.Name)]
public sealed class StatsEndpointTests(TestWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateAuthenticatedClient();
    private readonly HttpClient _nonRedirectClient = factory.CreateNonRedirectingClient();

    private async Task<string> CreateShortCodeAsync(string url)
    {
        var response = await _client.PostAsJsonAsync("/api/shorten", new ShortenRequest { Url = url });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ShortenResponse>();
        return body!.ShortCode;
    }

    private async Task SimulateClickAsync(string shortCode)
    {
        await _nonRedirectClient.GetAsync($"/{shortCode}");
        // Allow fire-and-forget click recording to complete
        await Task.Delay(1000);
    }

    [Fact]
    public async Task GetStats_UnknownCode_Returns404()
    {
        var response = await _client.GetAsync("/api/stats/zzzzzzz");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetStats_KnownCodeNoClicks_Returns200WithZeroCount()
    {
        await factory.ResetDatabaseAsync();
        await factory.ResetCacheAsync();

        var shortCode = await CreateShortCodeAsync("https://stats-no-clicks.example.com");

        var response = await _client.GetAsync($"/api/stats/{shortCode}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stats = await response.Content.ReadFromJsonAsync<StatsResponse>();
        stats.Should().NotBeNull();
        stats!.ShortCode.Should().Be(shortCode);
        stats.OriginalUrl.Should().Be("https://stats-no-clicks.example.com");
        stats.TotalClicks.Should().Be(0);
        stats.Clicks.Should().BeEmpty();
    }

    [Fact]
    public async Task GetStats_AfterOneClick_ReturnsOneClick()
    {
        await factory.ResetDatabaseAsync();
        await factory.ResetCacheAsync();

        var shortCode = await CreateShortCodeAsync("https://one-click.example.com");
        await SimulateClickAsync(shortCode);

        var response = await _client.GetAsync($"/api/stats/{shortCode}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stats = await response.Content.ReadFromJsonAsync<StatsResponse>();
        stats!.TotalClicks.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task GetStats_AfterMultipleClicks_ReflectsAllClicks()
    {
        await factory.ResetDatabaseAsync();
        await factory.ResetCacheAsync();

        var shortCode = await CreateShortCodeAsync("https://multi-click.example.com");

        // Simulate 3 clicks
        await SimulateClickAsync(shortCode);
        await SimulateClickAsync(shortCode);
        await SimulateClickAsync(shortCode);

        var response = await _client.GetAsync($"/api/stats/{shortCode}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stats = await response.Content.ReadFromJsonAsync<StatsResponse>();
        stats!.TotalClicks.Should().BeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task GetStats_ReturnsCorrectOriginalUrl()
    {
        await factory.ResetDatabaseAsync();
        await factory.ResetCacheAsync();

        const string originalUrl = "https://stats-url-check.example.com/path?q=1";
        var shortCode = await CreateShortCodeAsync(originalUrl);

        var response = await _client.GetAsync($"/api/stats/{shortCode}");

        var stats = await response.Content.ReadFromJsonAsync<StatsResponse>();
        stats!.OriginalUrl.Should().Be(originalUrl);
    }

    [Fact]
    public async Task GetStats_ClicksContainTimestamps()
    {
        await factory.ResetDatabaseAsync();
        await factory.ResetCacheAsync();

        var shortCode = await CreateShortCodeAsync("https://timestamp-check.example.com");
        await SimulateClickAsync(shortCode);

        var response = await _client.GetAsync($"/api/stats/{shortCode}");
        var stats = await response.Content.ReadFromJsonAsync<StatsResponse>();

        if (stats!.Clicks.Count > 0)
        {
            stats.Clicks[0].ClickedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(30));
        }
    }

    [Fact]
    public async Task GetStats_WithoutToken_Returns401()
    {
        var unauthClient = factory.CreateClient();
        var response = await unauthClient.GetAsync("/api/stats/anycode");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
