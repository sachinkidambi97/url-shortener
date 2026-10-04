namespace UrlShortener.Api.Models.Dtos;

public sealed record BulkShortenRequest
{
    public required List<ShortenRequest> Items { get; init; }
}
