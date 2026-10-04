namespace UrlShortener.Api.Services;

public interface IRedirectService
{
    Task<RedirectResult> GetOriginalUrlAsync(
        string shortCode,
        string? ipAddress,
        string? userAgent,
        string? referrer,
        CancellationToken cancellationToken = default);
}

public sealed record RedirectResult(string? OriginalUrl, bool IsExpired);
