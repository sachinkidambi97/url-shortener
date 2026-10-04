namespace UrlShortener.Api.Models.Dtos;

public sealed record UrlListItem
{
    public required string ShortCode { get; init; }
    public required string ShortUrl { get; init; }
    public required string OriginalUrl { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
}
