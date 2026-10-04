namespace UrlShortener.Api.Models.Dtos;

public sealed record ShortenRequest
{
    public required string Url { get; init; }
    public string? Alias { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
}
