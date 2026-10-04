using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using UrlShortener.Api.Models.Dtos;

namespace UrlShortener.Tests.Integration;

[Collection(IntegrationTestCollection.Name)]
public sealed class AliasEndpointTests(TestWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();
    private readonly HttpClient _nonRedirectClient = factory.CreateNonRedirectingClient();

    [Fact]
    public async Task Post_WithValidAlias_UsesAliasAsShortCode()
    {
        await factory.ResetDatabaseAsync();

        var request = new ShortenRequest { Url = "https://example.com", Alias = "my-alias" };
        var response = await _client.PostAsJsonAsync("/api/shorten", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<ShortenResponse>();
        body!.ShortCode.Should().Be("my-alias");
        body.ShortUrl.Should().EndWith("/my-alias");
    }

    [Fact]
    public async Task Post_WithAliasTooShort_Returns400()
    {
        var request = new ShortenRequest { Url = "https://example.com", Alias = "ab" };
        var response = await _client.PostAsJsonAsync("/api/shorten", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_WithAliasTooLong_Returns400()
    {
        var longAlias = new string('a', 31);
        var request = new ShortenRequest { Url = "https://example.com", Alias = longAlias };
        var response = await _client.PostAsJsonAsync("/api/shorten", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_WithAliasInvalidChars_Returns400()
    {
        var request = new ShortenRequest { Url = "https://example.com", Alias = "my_alias!" };
        var response = await _client.PostAsJsonAsync("/api/shorten", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_WithDuplicateAlias_Returns409()
    {
        await factory.ResetDatabaseAsync();

        // Create first URL with alias
        var request = new ShortenRequest { Url = "https://first.example.com", Alias = "unique-alias" };
        await _client.PostAsJsonAsync("/api/shorten", request);

        // Try to create second URL with the same alias
        var duplicateRequest = new ShortenRequest { Url = "https://second.example.com", Alias = "unique-alias" };
        var response = await _client.PostAsJsonAsync("/api/shorten", duplicateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Get_AliasCode_RedirectsCorrectly()
    {
        await factory.ResetDatabaseAsync();
        await factory.ResetCacheAsync();

        // Create URL with alias
        var createRequest = new ShortenRequest { Url = "https://alias-redirect.example.com", Alias = "redirect-me" };
        await _client.PostAsJsonAsync("/api/shorten", createRequest);

        // Use alias to redirect
        var redirectResponse = await _nonRedirectClient.GetAsync("/redirect-me");

        redirectResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
        redirectResponse.Headers.Location!.ToString().Should().StartWith("https://alias-redirect.example.com");
    }

    [Fact]
    public async Task Post_WithValidAlias_MinLength3_Succeeds()
    {
        await factory.ResetDatabaseAsync();

        var request = new ShortenRequest { Url = "https://min-alias.example.com", Alias = "abc" };
        var response = await _client.PostAsJsonAsync("/api/shorten", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Post_WithValidAlias_MaxLength30_Succeeds()
    {
        await factory.ResetDatabaseAsync();

        var maxAlias = new string('a', 30);
        var request = new ShortenRequest { Url = "https://max-alias.example.com", Alias = maxAlias };
        var response = await _client.PostAsJsonAsync("/api/shorten", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Post_WithHyphenAlias_Succeeds()
    {
        await factory.ResetDatabaseAsync();

        var request = new ShortenRequest { Url = "https://hyphen-alias.example.com", Alias = "my-custom-link" };
        var response = await _client.PostAsJsonAsync("/api/shorten", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<ShortenResponse>();
        body!.ShortCode.Should().Be("my-custom-link");
    }
}
