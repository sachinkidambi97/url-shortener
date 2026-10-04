namespace UrlShortener.Api.Models.Dtos;

public sealed record BulkShortenResponse
{
    public required List<BulkShortenResultItem> Results { get; init; }
}
