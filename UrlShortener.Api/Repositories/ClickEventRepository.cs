using Microsoft.EntityFrameworkCore;
using Polly;
using UrlShortener.Api.Data;
using UrlShortener.Api.Infrastructure;
using UrlShortener.Api.Models;

namespace UrlShortener.Api.Repositories;

public sealed class ClickEventRepository(AppDbContext dbContext, ILogger<ClickEventRepository> logger) : IClickEventRepository
{
    private readonly IAsyncPolicy _retryPolicy = ResiliencePolicies.CreateDatabaseRetryPolicy(logger);

    public async Task RecordClickAsync(ClickEvent clickEvent, CancellationToken cancellationToken = default)
    {
        clickEvent.ClickedAt = DateTimeOffset.UtcNow;
        dbContext.ClickEvents.Add(clickEvent);
        await _retryPolicy.ExecuteAsync(() =>
            dbContext.SaveChangesAsync(cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ClickEvent>> GetByShortCodeAsync(
        string shortCode,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
        => await _retryPolicy.ExecuteAsync(() =>
            dbContext.ClickEvents
                .AsNoTracking()
                .Where(ce => ce.ShortCode == shortCode)
                .OrderByDescending(ce => ce.ClickedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken))
            .ConfigureAwait(false);

    public async Task<long> GetCountByShortCodeAsync(string shortCode, CancellationToken cancellationToken = default)
        => await _retryPolicy.ExecuteAsync(() =>
            dbContext.ClickEvents
                .LongCountAsync(ce => ce.ShortCode == shortCode, cancellationToken))
            .ConfigureAwait(false);
}
