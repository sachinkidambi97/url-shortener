using Microsoft.Extensions.Options;
using UrlShortener.Api.Infrastructure;
using UrlShortener.Api.Repositories;

namespace UrlShortener.Api.Services;

public sealed class RedirectService(
    ICacheService cacheService,
    IUrlRepository urlRepository,
    IAnalyticsService analyticsService,
    IServiceScopeFactory scopeFactory,
    IOptions<CacheOptions> cacheOptions,
    ILogger<RedirectService> logger) : IRedirectService
{
    public async Task<RedirectResult> GetOriginalUrlAsync(
        string shortCode,
        string? ipAddress,
        string? userAgent,
        string? referrer,
        CancellationToken cancellationToken = default)
    {
        string cacheKey = $"{cacheOptions.Value.KeyPrefix}{shortCode}";

        string? cachedUrl = await cacheService.GetAsync(cacheKey, cancellationToken).ConfigureAwait(false);
        if (cachedUrl is not null)
        {
            RecordClickFireAndForget(shortCode, ipAddress, userAgent, referrer);
            return new RedirectResult(cachedUrl, false);
        }

        var entity = await urlRepository.GetByShortCodeAsync(shortCode, cancellationToken).ConfigureAwait(false);
        if (entity is null)
            return new RedirectResult(null, false);

        if (entity.ExpiresAt.HasValue && entity.ExpiresAt.Value <= DateTimeOffset.UtcNow)
        {
            logger.LogInformation("Short code {ShortCode} has expired at {ExpiresAt}", shortCode, entity.ExpiresAt);
            return new RedirectResult(null, true);
        }

        await cacheService.SetAsync(cacheKey, entity.OriginalUrl, cancellationToken: cancellationToken).ConfigureAwait(false);
        RecordClickFireAndForget(shortCode, ipAddress, userAgent, referrer);
        return new RedirectResult(entity.OriginalUrl, false);
    }

    private void RecordClickFireAndForget(string shortCode, string? ipAddress, string? userAgent, string? referrer)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var scopedAnalytics = scope.ServiceProvider.GetRequiredService<IAnalyticsService>();
                await scopedAnalytics.RecordClickAsync(shortCode, ipAddress, userAgent, referrer).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Fire-and-forget click recording failed for short code {ShortCode}", shortCode);
            }
        });
    }
}
