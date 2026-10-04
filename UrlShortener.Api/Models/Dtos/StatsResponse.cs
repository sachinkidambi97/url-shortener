namespace UrlShortener.Api.Models.Dtos;

public sealed record StatsResponse
{
    public required string ShortCode { get; init; }
    public required string OriginalUrl { get; init; }
    public required long TotalClicks { get; init; }
    public required IReadOnlyList<ClickDto> Clicks { get; init; }
}
