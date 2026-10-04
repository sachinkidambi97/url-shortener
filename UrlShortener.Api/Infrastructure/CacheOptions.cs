namespace UrlShortener.Api.Infrastructure;

public sealed class CacheOptions
{
    public const string SectionName = "Cache";
    public double DefaultTtlHours { get; set; } = 1.0;
    public string KeyPrefix { get; set; } = "url:";
}
