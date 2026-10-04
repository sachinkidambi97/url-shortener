using UrlShortener.Api.Models;

namespace UrlShortener.Api.Repositories;

public interface IUrlRepository
{
    Task<ShortenedUrl?> GetByShortCodeAsync(string shortCode, CancellationToken cancellationToken = default);
    Task<ShortenedUrl> CreateAsync(ShortenedUrl shortenedUrl, CancellationToken cancellationToken = default);
    Task<ShortenedUrl> UpdateAsync(ShortenedUrl shortenedUrl, CancellationToken cancellationToken = default);
    Task DeleteAsync(ShortenedUrl shortenedUrl, CancellationToken cancellationToken = default);
    Task<bool> ExistsByShortCodeAsync(string shortCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ShortenedUrl>> GetExpiredAsync(CancellationToken cancellationToken = default);
    Task DeleteRangeAsync(IEnumerable<ShortenedUrl> urls, CancellationToken cancellationToken = default);
}
