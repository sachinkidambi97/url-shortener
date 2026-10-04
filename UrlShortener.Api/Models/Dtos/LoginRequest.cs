namespace UrlShortener.Api.Models.Dtos;

public sealed record LoginRequest
{
    public required string Email { get; init; }
    public required string Password { get; init; }
}
