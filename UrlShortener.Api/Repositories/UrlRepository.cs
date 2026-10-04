using Microsoft.EntityFrameworkCore;
using Polly;
using UrlShortener.Api.Data;
using UrlShortener.Api.Infrastructure;
using UrlShortener.Api.Models;

namespace UrlShortener.Api.Repositories;

public sealed class UrlRepository(AppDbContext dbContext, ILogger<UrlRepository> logger) : IUrlRepository
{
    private readonly IAsyncPolicy _retryPolicy = ResiliencePolicies.CreateDatabaseRetryPolicy(logger);

    public async Task<ShortenedUrl?> GetByShortCodeAsync(string shortCode, CancellationToken cancellationToken = default)
        => await _retryPolicy.ExecuteAsync(() =>
            dbContext.ShortenedUrls
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.ShortCode == shortCode, cancellationToken))
            .ConfigureAwait(false);

    public async Task<ShortenedUrl> CreateAsync(ShortenedUrl shortenedUrl, CancellationToken cancellationToken = default)
    {
        shortenedUrl.CreatedAt = DateTimeOffset.UtcNow;
        dbContext.ShortenedUrls.Add(shortenedUrl);
        await _retryPolicy.ExecuteAsync(() =>
            dbContext.SaveChangesAsync(cancellationToken))
            .ConfigureAwait(false);
        return shortenedUrl;
    }

    public async Task<bool> ExistsByShortCodeAsync(string shortCode, CancellationToken cancellationToken = default)
        => await _retryPolicy.ExecuteAsync(() =>
            dbContext.ShortenedUrls
                .AnyAsync(u => u.ShortCode == shortCode, cancellationToken))
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<ShortenedUrl>> GetExpiredAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        return await _retryPolicy.ExecuteAsync(() =>
            dbContext.ShortenedUrls
                .Where(u => u.ExpiresAt != null && u.ExpiresAt <= now)
                .ToListAsync(cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task DeleteRangeAsync(IEnumerable<ShortenedUrl> urls, CancellationToken cancellationToken = default)
    {
        dbContext.ShortenedUrls.RemoveRange(urls);
        await _retryPolicy.ExecuteAsync(() =>
            dbContext.SaveChangesAsync(cancellationToken))
            .ConfigureAwait(false);
    }
}
