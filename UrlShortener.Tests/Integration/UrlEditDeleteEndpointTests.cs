using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using UrlShortener.Api.Data;
using UrlShortener.Api.Models;
using UrlShortener.Api.Models.Dtos;

namespace UrlShortener.Tests.Integration;

[Collection(IntegrationTestCollection.Name)]
public sealed class UrlEditDeleteEndpointTests(TestWebApplicationFactory factory)
{
    // Helper: create a URL owned by given userId, return short code
    private async Task<string> CreateUrlForUserAsync(int userId, string email, string originalUrl)
    {
        var client = factory.CreateAuthenticatedClient(userId, email);
        var response = await client.PostAsJsonAsync("/api/shorten", new ShortenRequest { Url = originalUrl });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ShortenResponse>();
        return body!.ShortCode;
    }

    // ---- PUT /api/urls/{code} ----

    [Fact]
    public async Task Put_ValidOwner_Returns200WithUpdatedUrl()
    {
        await factory.ResetDatabaseAsync();

        var shortCode = await CreateUrlForUserAsync(1, "owner@example.com", "https://original.example.com");
        var client = factory.CreateAuthenticatedClient(1, "owner@example.com");

        var response = await client.PutAsJsonAsync($"/api/urls/{shortCode}",
            new UpdateUrlRequest { Url = "https://updated.example.com" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ShortenResponse>();
        body.Should().NotBeNull();
        body!.ShortCode.Should().Be(shortCode);
        body.ShortUrl.Should().Contain(shortCode);
    }

    [Fact]
    public async Task Put_ValidOwner_OriginalUrlIsUpdatedInDatabase()
    {
        await factory.ResetDatabaseAsync();

        var shortCode = await CreateUrlForUserAsync(1, "owner@example.com", "https://original.example.com");
        var client = factory.CreateAuthenticatedClient(1, "owner@example.com");

        await client.PutAsJsonAsync($"/api/urls/{shortCode}",
            new UpdateUrlRequest { Url = "https://updated.example.com" });

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entity = db.ShortenedUrls.FirstOrDefault(u => u.ShortCode == shortCode);

        entity.Should().NotBeNull();
        entity!.OriginalUrl.Should().Be("https://updated.example.com");
    }

    [Fact]
    public async Task Put_CodeNotFound_Returns404()
    {
        var client = factory.CreateAuthenticatedClient(1, "owner@example.com");

        var response = await client.PutAsJsonAsync("/api/urls/doesnotexist",
            new UpdateUrlRequest { Url = "https://updated.example.com" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Put_WrongOwner_Returns403()
    {
        await factory.ResetDatabaseAsync();

        // Create URL as user 400
        var shortCode = await CreateUrlForUserAsync(400, "user400@example.com", "https://original.example.com");

        // Try to update as user 401
        var otherClient = factory.CreateAuthenticatedClient(401, "user401@example.com");
        var response = await otherClient.PutAsJsonAsync($"/api/urls/{shortCode}",
            new UpdateUrlRequest { Url = "https://updated.example.com" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Put_InvalidNewUrl_Returns400()
    {
        await factory.ResetDatabaseAsync();

        var shortCode = await CreateUrlForUserAsync(1, "owner@example.com", "https://original.example.com");
        var client = factory.CreateAuthenticatedClient(1, "owner@example.com");

        var response = await client.PutAsJsonAsync($"/api/urls/{shortCode}",
            new UpdateUrlRequest { Url = "not-a-valid-url" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Put_WithoutToken_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync("/api/urls/anycode",
            new UpdateUrlRequest { Url = "https://updated.example.com" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---- DELETE /api/urls/{code} ----

    [Fact]
    public async Task Delete_ValidOwner_Returns204()
    {
        await factory.ResetDatabaseAsync();

        var shortCode = await CreateUrlForUserAsync(1, "owner@example.com", "https://todelete.example.com");
        var client = factory.CreateAuthenticatedClient(1, "owner@example.com");

        var response = await client.DeleteAsync($"/api/urls/{shortCode}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_ValidOwner_RemovesFromDatabase()
    {
        await factory.ResetDatabaseAsync();

        var shortCode = await CreateUrlForUserAsync(1, "owner@example.com", "https://todelete.example.com");
        var client = factory.CreateAuthenticatedClient(1, "owner@example.com");

        await client.DeleteAsync($"/api/urls/{shortCode}");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entity = db.ShortenedUrls.FirstOrDefault(u => u.ShortCode == shortCode);

        entity.Should().BeNull();
    }

    [Fact]
    public async Task Delete_CodeNotFound_Returns404()
    {
        var client = factory.CreateAuthenticatedClient(1, "owner@example.com");

        var response = await client.DeleteAsync("/api/urls/doesnotexist");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_WrongOwner_Returns403()
    {
        await factory.ResetDatabaseAsync();

        // Create URL as user 500
        var shortCode = await CreateUrlForUserAsync(500, "user500@example.com", "https://original.example.com");

        // Try to delete as user 501
        var otherClient = factory.CreateAuthenticatedClient(501, "user501@example.com");
        var response = await otherClient.DeleteAsync($"/api/urls/{shortCode}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Delete_WithoutToken_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.DeleteAsync("/api/urls/anycode");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Delete_ValidOwner_ClickEventsAreAlsoDeleted()
    {
        await factory.ResetDatabaseAsync();

        var shortCode = await CreateUrlForUserAsync(1, "owner@example.com", "https://todelete.example.com");

        // Simulate a click event
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.ClickEvents.Add(new ClickEvent
            {
                ShortCode = shortCode,
                ClickedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var client = factory.CreateAuthenticatedClient(1, "owner@example.com");
        var response = await client.DeleteAsync($"/api/urls/{shortCode}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var events = db.ClickEvents.Where(e => e.ShortenedUrl!.ShortCode == shortCode).ToList();
            events.Should().BeEmpty();
        }
    }
}
