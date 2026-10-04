namespace UrlShortener.Api.Models.Dtos;

public sealed record ClickDto
{
    public required DateTimeOffset ClickedAt { get; init; }
    public string? Referrer { get; init; }
    public string? UserAgent { get; init; }
    public string? IpAddress { get; init; }
}
