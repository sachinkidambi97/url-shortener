namespace UrlShortener.Api.Models;

public sealed class ClickEvent
{
    public long Id { get; set; }
    public required string ShortCode { get; set; }
    public DateTimeOffset ClickedAt { get; set; }
    public string? Referrer { get; set; }
    public string? UserAgent { get; set; }
    public string? IpAddress { get; set; }

    public ShortenedUrl? ShortenedUrl { get; set; }
}
