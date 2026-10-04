namespace UrlShortener.Api.Models.Dtos;

public sealed record AuthResponse
{
    public required string Token { get; init; }
    public required string Email { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
}
