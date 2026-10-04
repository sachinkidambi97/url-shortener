namespace UrlShortener.Api.Models.Dtos;

public sealed record UpdateUrlRequest
{
    public required string Url { get; init; }
}
