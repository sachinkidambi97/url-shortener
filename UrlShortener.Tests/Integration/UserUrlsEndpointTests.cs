using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using UrlShortener.Api.Models.Dtos;

namespace UrlShortener.Tests.Integration;

[Collection(IntegrationTestCollection.Name)]
public sealed class UserUrlsEndpointTests(TestWebApplicationFactory factory)
{
    [Fact]
    public async Task GetMyUrls_WithoutToken_Returns401()
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync("/api/urls");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMyUrls_EmptyList_Returns200WithEmptyArray()
    {
        await factory.ResetDatabaseAsync();

        // Use a unique userId so no URLs are linked
        var client = factory.CreateAuthenticatedClient(userId: 9999, email: "emptylist@example.com");
        var response = await client.GetAsync("/api/urls");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<UrlListItem>>();
        body.Should().NotBeNull();
        body.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMyUrls_AfterCreatingUrls_ReturnsThem()
    {
        await factory.ResetDatabaseAsync();

        // Use userId=100 for this test to isolate from others
        var client = factory.CreateAuthenticatedClient(userId: 100, email: "myurls@example.com");

        await client.PostAsJsonAsync("/api/shorten", new ShortenRequest { Url = "https://first.example.com" });
        await client.PostAsJsonAsync("/api/shorten", new ShortenRequest { Url = "https://second.example.com" });

        var response = await client.GetAsync("/api/urls");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<UrlListItem>>();
        body.Should().NotBeNull();
        body!.Count.Should().Be(2);
        body.Should().Contain(u => u.OriginalUrl == "https://first.example.com");
        body.Should().Contain(u => u.OriginalUrl == "https://second.example.com");
    }

    [Fact]
    public async Task GetMyUrls_OnlyReturnsCurrentUserUrls()
    {
        await factory.ResetDatabaseAsync();

        var clientA = factory.CreateAuthenticatedClient(userId: 200, email: "usera@example.com");
        var clientB = factory.CreateAuthenticatedClient(userId: 201, email: "userb@example.com");

        await clientA.PostAsJsonAsync("/api/shorten", new ShortenRequest { Url = "https://usera.example.com" });
        await clientB.PostAsJsonAsync("/api/shorten", new ShortenRequest { Url = "https://userb.example.com" });

        var responseA = await clientA.GetAsync("/api/urls");
        var urlsA = await responseA.Content.ReadFromJsonAsync<List<UrlListItem>>();

        urlsA.Should().NotBeNull();
        urlsA!.Should().HaveCount(1);
        urlsA[0].OriginalUrl.Should().Be("https://usera.example.com");
    }

    [Fact]
    public async Task GetMyUrls_ResponseIncludesShortUrl()
    {
        await factory.ResetDatabaseAsync();

        var client = factory.CreateAuthenticatedClient(userId: 300, email: "shorturl@example.com");
        await client.PostAsJsonAsync("/api/shorten", new ShortenRequest { Url = "https://check-shorturl.example.com" });

        var response = await client.GetAsync("/api/urls");
        var body = await response.Content.ReadFromJsonAsync<List<UrlListItem>>();

        body.Should().NotBeNull();
        body!.Should().HaveCount(1);
        body[0].ShortUrl.Should().StartWith("http://localhost");
        body[0].ShortCode.Should().NotBeNullOrEmpty();
        body[0].ShortUrl.Should().EndWith(body[0].ShortCode);
    }
}
