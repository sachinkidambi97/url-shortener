using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using UrlShortener.Api.Data;
using UrlShortener.Api.Models;
using UrlShortener.Api.Models.Dtos;

namespace UrlShortener.Tests.Integration;

[Collection(IntegrationTestCollection.Name)]
public sealed class RedirectEndpointTests(TestWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateNonRedirectingClient();

    private async Task<string> CreateShortCodeAsync(string url)
    {
        var client = factory.CreateAuthenticatedClient();
        var response = await client.PostAsJsonAsync("/api/shorten", new ShortenRequest { Url = url });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ShortenResponse>();
        return body!.ShortCode;
    }

    [Fact]
    public async Task Get_ValidCode_Returns302WithCorrectLocationHeader()
    {
        await factory.ResetDatabaseAsync();
        await factory.ResetCacheAsync();

        var shortCode = await CreateShortCodeAsync("https://redirect-target.example.com");

        var response = await _client.GetAsync($"/{shortCode}");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().StartWith("https://redirect-target.example.com");
    }

    [Fact]
    public async Task Get_UnknownCode_Returns404()
    {
        var response = await _client.GetAsync("/zzzzzzz");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_ValidCode_RecordsClickEvent()
    {
        await factory.ResetDatabaseAsync();
        await factory.ResetCacheAsync();

        var shortCode = await CreateShortCodeAsync("https://click-tracking.example.com");

        // Make one redirect request to record the click
        await _client.GetAsync($"/{shortCode}");

        // Give fire-and-forget time to complete
        await Task.Delay(1000);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var clickCount = db.ClickEvents.Count(ce => ce.ShortCode == shortCode);

        clickCount.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Get_ValidCode_SecondRequest_UsesCacheAndStillRedirects()
    {
        await factory.ResetDatabaseAsync();
        await factory.ResetCacheAsync();

        var shortCode = await CreateShortCodeAsync("https://cached-redirect.example.com");

        // First request populates cache
        var response1 = await _client.GetAsync($"/{shortCode}");
        // Second request should hit cache
        var response2 = await _client.GetAsync($"/{shortCode}");

        response1.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response2.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response2.Headers.Location!.ToString().Should().StartWith("https://cached-redirect.example.com");
    }

    [Fact]
    public async Task Get_ValidCode_Returns302NotPermanentRedirect()
    {
        await factory.ResetDatabaseAsync();
        await factory.ResetCacheAsync();

        var shortCode = await CreateShortCodeAsync("https://temporary-redirect.example.com");

        var response = await _client.GetAsync($"/{shortCode}");

        // Must be 302 (temporary redirect) not 301 (permanent)
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        ((int)response.StatusCode).Should().Be(302);
    }
}
