namespace UrlShortener.Api.Models.Dtos;

public sealed record ShortenResponse
{
    public required string ShortCode { get; init; }
    public required string ShortUrl { get; init; }
}
