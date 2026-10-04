namespace UrlShortener.Api.Models;

public sealed class ShortenedUrl
{
    public int Id { get; set; }
    public required string ShortCode { get; set; }
    public required string OriginalUrl { get; set; }
    public string? Alias { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }

    public ICollection<ClickEvent> ClickEvents { get; set; } = new List<ClickEvent>();
}
