namespace UrlShortener.Api.Models.Dtos;

public sealed record BulkShortenResultItem
{
    public required string Url { get; init; }
    public string? ShortCode { get; init; }
    public string? ShortUrl { get; init; }
    public required bool Success { get; init; }
    public string? Error { get; init; }
}
