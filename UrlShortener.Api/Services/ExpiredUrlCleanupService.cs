using UrlShortener.Api.Infrastructure;
using UrlShortener.Api.Repositories;
using UrlShortener.Api.Services;
using Microsoft.Extensions.Options;

namespace UrlShortener.Api.Services;

public sealed class ExpiredUrlCleanupService(
    IServiceProvider serviceProvider,
    IOptions<CacheOptions> cacheOptions,
    ILogger<ExpiredUrlCleanupService> logger) : BackgroundService
{
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("ExpiredUrlCleanupService started. Cleanup interval: {Interval}", CleanupInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(CleanupInterval, stoppingToken).ConfigureAwait(false);

            if (stoppingToken.IsCancellationRequested)
                break;

            await CleanupExpiredUrlsAsync(stoppingToken).ConfigureAwait(false);
        }

        logger.LogInformation("ExpiredUrlCleanupService stopped.");
    }

    private async Task CleanupExpiredUrlsAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = serviceProvider.CreateScope();
            var urlRepository = scope.ServiceProvider.GetRequiredService<IUrlRepository>();
            var cacheService = scope.ServiceProvider.GetRequiredService<ICacheService>();

            var expiredUrls = await urlRepository.GetExpiredAsync(cancellationToken).ConfigureAwait(false);

            if (expiredUrls.Count == 0)
            {
                logger.LogDebug("No expired URLs to clean up.");
                return;
            }

            logger.LogInformation("Found {Count} expired URLs to clean up.", expiredUrls.Count);

            // Evict from Redis cache first
            foreach (var url in expiredUrls)
            {
                string cacheKey = $"{cacheOptions.Value.KeyPrefix}{url.ShortCode}";
                await cacheService.RemoveAsync(cacheKey, cancellationToken).ConfigureAwait(false);
            }

            // Delete from DB
            await urlRepository.DeleteRangeAsync(expiredUrls, cancellationToken).ConfigureAwait(false);

            logger.LogInformation("Cleaned up {Count} expired URLs.", expiredUrls.Count);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown — swallow
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during expired URL cleanup.");
        }
    }
}
