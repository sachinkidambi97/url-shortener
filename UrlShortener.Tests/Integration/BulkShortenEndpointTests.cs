using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using UrlShortener.Api.Models.Dtos;

namespace UrlShortener.Tests.Integration;

[Collection(IntegrationTestCollection.Name)]
public sealed class BulkShortenEndpointTests(TestWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateAuthenticatedClient();

    [Fact]
    public async Task Post_BulkShorten_WithoutToken_Returns401()
    {
        var unauthClient = factory.CreateClient();
        var request = new BulkShortenRequest
        {
            Items = [new ShortenRequest { Url = "https://example.com" }]
        };

        var response = await unauthClient.PostAsJsonAsync("/api/shorten/bulk", request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_BulkShorten_AllValidUrls_Returns200WithAllSuccessResults()
    {
        await factory.ResetDatabaseAsync();

        var request = new BulkShortenRequest
        {
            Items =
            [
                new ShortenRequest { Url = "https://example.com" },
                new ShortenRequest { Url = "https://another-bulk.com" }
            ]
        };

        var response = await _client.PostAsJsonAsync("/api/shorten/bulk", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<BulkShortenResponse>();
        body.Should().NotBeNull();
        body!.Results.Should().HaveCount(2);
        body.Results.Should().AllSatisfy(r =>
        {
            r.Success.Should().BeTrue();
            r.ShortCode.Should().NotBeNullOrEmpty();
            r.ShortUrl.Should().Contain(r.ShortCode);
            r.Error.Should().BeNull();
        });
    }

    [Fact]
    public async Task Post_BulkShorten_MixedValidAndInvalidUrls_Returns200WithMixedResults()
    {
        await factory.ResetDatabaseAsync();

        var request = new BulkShortenRequest
        {
            Items =
            [
                new ShortenRequest { Url = "https://valid-bulk.com" },
                new ShortenRequest { Url = "not-a-valid-url" },
                new ShortenRequest { Url = "https://also-valid-bulk.com" }
            ]
        };

        var response = await _client.PostAsJsonAsync("/api/shorten/bulk", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<BulkShortenResponse>();
        body.Should().NotBeNull();
        body!.Results.Should().HaveCount(3);
        body.Results[0].Success.Should().BeTrue();
        body.Results[0].Url.Should().Be("https://valid-bulk.com");
        body.Results[1].Success.Should().BeFalse();
        body.Results[1].Url.Should().Be("not-a-valid-url");
        body.Results[1].Error.Should().NotBeNullOrEmpty();
        body.Results[2].Success.Should().BeTrue();
        body.Results[2].Url.Should().Be("https://also-valid-bulk.com");
    }

    [Fact]
    public async Task Post_BulkShorten_Over100Items_Returns400()
    {
        var items = Enumerable.Range(1, 101)
            .Select(i => new ShortenRequest { Url = $"https://example{i}.com" })
            .ToList();
        var request = new BulkShortenRequest { Items = items };

        var response = await _client.PostAsJsonAsync("/api/shorten/bulk", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_BulkShorten_EmptyList_Returns200WithEmptyResults()
    {
        var request = new BulkShortenRequest { Items = [] };

        var response = await _client.PostAsJsonAsync("/api/shorten/bulk", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<BulkShortenResponse>();
        body.Should().NotBeNull();
        body!.Results.Should().BeEmpty();
    }
}
