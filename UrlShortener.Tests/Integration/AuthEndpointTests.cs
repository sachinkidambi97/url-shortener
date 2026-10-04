using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using UrlShortener.Api.Models.Dtos;

namespace UrlShortener.Tests.Integration;

[Collection(IntegrationTestCollection.Name)]
public sealed class AuthEndpointTests(TestWebApplicationFactory factory)
{
    private HttpClient CreateFreshClient() => factory.CreateClient();

    [Fact]
    public async Task Register_ValidCredentials_Returns201WithToken()
    {
        await factory.ResetDatabaseAsync();

        var request = new RegisterRequest { Email = "newuser@example.com", Password = "password123" };
        var response = await CreateFreshClient().PostAsJsonAsync("/api/auth/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        body.Should().NotBeNull();
        body!.Token.Should().NotBeNullOrEmpty();
        body.Email.Should().Be("newuser@example.com");
        body.ExpiresAt.Should().BeAfter(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409()
    {
        await factory.ResetDatabaseAsync();

        var request = new RegisterRequest { Email = "duplicate@example.com", Password = "password123" };
        await CreateFreshClient().PostAsJsonAsync("/api/auth/register", request);

        var response = await CreateFreshClient().PostAsJsonAsync("/api/auth/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Register_ShortPassword_Returns400()
    {
        var request = new RegisterRequest { Email = "shortpw@example.com", Password = "abc" };
        var response = await CreateFreshClient().PostAsJsonAsync("/api/auth/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_EmptyEmail_Returns400()
    {
        var request = new RegisterRequest { Email = "", Password = "password123" };
        var response = await CreateFreshClient().PostAsJsonAsync("/api/auth/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_ValidCredentials_Returns200WithToken()
    {
        await factory.ResetDatabaseAsync();

        var email = "logintest@example.com";
        var password = "mypassword123";

        // Register first
        await CreateFreshClient().PostAsJsonAsync("/api/auth/register",
            new RegisterRequest { Email = email, Password = password });

        // Now login
        var response = await CreateFreshClient().PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = email, Password = password });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        body.Should().NotBeNull();
        body!.Token.Should().NotBeNullOrEmpty();
        body.Email.Should().Be(email);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        await factory.ResetDatabaseAsync();

        var email = "wrongpw@example.com";
        await CreateFreshClient().PostAsJsonAsync("/api/auth/register",
            new RegisterRequest { Email = email, Password = "correctpassword" });

        var response = await CreateFreshClient().PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = email, Password = "wrongpassword" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_NonExistentEmail_Returns401()
    {
        var response = await CreateFreshClient().PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = "nobody@example.com", Password = "password123" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_TokenCanBeUsedToShortenUrl()
    {
        await factory.ResetDatabaseAsync();

        var email = "tokenuse@example.com";
        var password = "password123";

        // Register
        var registerResponse = await CreateFreshClient().PostAsJsonAsync("/api/auth/register",
            new RegisterRequest { Email = email, Password = password });
        var authBody = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();

        // Use token to shorten URL
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authBody!.Token);

        var shortenResponse = await client.PostAsJsonAsync("/api/shorten",
            new ShortenRequest { Url = "https://example.com" });

        shortenResponse.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
