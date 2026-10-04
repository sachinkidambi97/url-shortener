using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using UrlShortener.Api.Data;
using UrlShortener.Api.Models.Dtos;

namespace UrlShortener.Tests.Integration;

[Collection(IntegrationTestCollection.Name)]
public sealed class ShortenEndpointTests(TestWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateAuthenticatedClient();

    [Fact]
    public async Task Post_ValidHttpsUrl_Returns201WithShortCode()
    {
        await factory.ResetDatabaseAsync();

        var request = new ShortenRequest { Url = "https://example.com" };
        var response = await _client.PostAsJsonAsync("/api/shorten", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<ShortenResponse>();
        body.Should().NotBeNull();
        body!.ShortCode.Should().NotBeNullOrEmpty();
        body.ShortUrl.Should().Contain(body.ShortCode);
    }

    [Fact]
    public async Task Post_ValidHttpUrl_Returns201WithShortCode()
    {
        await factory.ResetDatabaseAsync();

        var request = new ShortenRequest { Url = "http://example.com/path?q=test&other=value" };
        var response = await _client.PostAsJsonAsync("/api/shorten", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<ShortenResponse>();
        body!.ShortCode.Should().HaveLength(7);
    }

    [Fact]
    public async Task Post_InvalidUrl_Returns400()
    {
        var request = new ShortenRequest { Url = "not-a-url" };
        var response = await _client.PostAsJsonAsync("/api/shorten", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_FtpUrl_Returns400()
    {
        var request = new ShortenRequest { Url = "ftp://files.example.com/file.txt" };
        var response = await _client.PostAsJsonAsync("/api/shorten", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_EmptyUrl_Returns400()
    {
        var request = new ShortenRequest { Url = "" };
        var response = await _client.PostAsJsonAsync("/api/shorten", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_MissingBody_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/shorten", new { });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_ValidUrl_ShortCodeIsPersistedInDatabase()
    {
        await factory.ResetDatabaseAsync();

        var request = new ShortenRequest { Url = "https://persisted.example.com" };
        var response = await _client.PostAsJsonAsync("/api/shorten", request);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<ShortenResponse>();
        body.Should().NotBeNull();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entity = db.ShortenedUrls.FirstOrDefault(u => u.ShortCode == body!.ShortCode);

        entity.Should().NotBeNull();
        entity!.OriginalUrl.Should().Be("https://persisted.example.com");
    }

    [Fact]
    public async Task Post_ValidUrl_ResponseContainsLocationHeader()
    {
        await factory.ResetDatabaseAsync();

        var request = new ShortenRequest { Url = "https://example.com/with/path" };
        var response = await _client.PostAsJsonAsync("/api/shorten", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        // 201 Created responses should have a Location header
        response.Headers.Location.Should().NotBeNull();
    }

    [Fact]
    public async Task Post_TwoUniqueUrls_ReturnDifferentShortCodes()
    {
        await factory.ResetDatabaseAsync();

        var response1 = await _client.PostAsJsonAsync("/api/shorten", new ShortenRequest { Url = "https://first.example.com" });
        var response2 = await _client.PostAsJsonAsync("/api/shorten", new ShortenRequest { Url = "https://second.example.com" });

        var body1 = await response1.Content.ReadFromJsonAsync<ShortenResponse>();
        var body2 = await response2.Content.ReadFromJsonAsync<ShortenResponse>();

        body1!.ShortCode.Should().NotBe(body2!.ShortCode);
    }

    [Fact]
    public async Task Post_ValidUrl_ShortUrlUsesCorrectHost()
    {
        await factory.ResetDatabaseAsync();

        var request = new ShortenRequest { Url = "https://example.com" };
        var response = await _client.PostAsJsonAsync("/api/shorten", request);

        var body = await response.Content.ReadFromJsonAsync<ShortenResponse>();
        body!.ShortUrl.Should().StartWith("http://localhost");
    }

    [Fact]
    public async Task Post_WithoutToken_Returns401()
    {
        var unauthClient = factory.CreateClient();
        var request = new ShortenRequest { Url = "https://example.com" };
        var response = await unauthClient.PostAsJsonAsync("/api/shorten", request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
