using UrlShortener.Api.Models.Dtos;

namespace UrlShortener.Api.Services;

public interface IAnalyticsService
{
    Task RecordClickAsync(string shortCode, string? ipAddress, string? userAgent, string? referrer, CancellationToken cancellationToken = default);
    Task<StatsResponse?> GetStatsAsync(string shortCode, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default);
}
