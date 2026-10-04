namespace UrlShortener.Api.Models.Dtos;

public sealed record RegisterRequest
{
    public required string Email { get; init; }
    public required string Password { get; init; }
}
