namespace UrlShortener.Api.Infrastructure;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "UrlShortener";
    public string Audience { get; set; } = "UrlShortener";
    public string SecretKey { get; set; } = string.Empty;
    public int ExpirationHours { get; set; } = 24;
}
