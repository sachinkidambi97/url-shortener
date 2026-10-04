using UrlShortener.Api.Models;
using UrlShortener.Api.Models.Dtos;
using UrlShortener.Api.Repositories;

namespace UrlShortener.Api.Services;

public sealed class AnalyticsService(
    IClickEventRepository clickEventRepository,
    IUrlRepository urlRepository) : IAnalyticsService
{
    public async Task RecordClickAsync(
        string shortCode,
        string? ipAddress,
        string? userAgent,
        string? referrer,
        CancellationToken cancellationToken = default)
    {
        var clickEvent = new ClickEvent
        {
            ShortCode = shortCode,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            Referrer = referrer
        };

        await clickEventRepository.RecordClickAsync(clickEvent, cancellationToken).ConfigureAwait(false);
    }

    public async Task<StatsResponse?> GetStatsAsync(
        string shortCode,
        int page = 1,
        int pageSize = 100,
        CancellationToken cancellationToken = default)
    {
        var shortenedUrl = await urlRepository.GetByShortCodeAsync(shortCode, cancellationToken).ConfigureAwait(false);
        if (shortenedUrl is null)
            return null;

        var totalClicks = await clickEventRepository.GetCountByShortCodeAsync(shortCode, cancellationToken).ConfigureAwait(false);
        var clicks = await clickEventRepository.GetByShortCodeAsync(shortCode, page, pageSize, cancellationToken).ConfigureAwait(false);

        return new StatsResponse
        {
            ShortCode = shortCode,
            OriginalUrl = shortenedUrl.OriginalUrl,
            TotalClicks = totalClicks,
            Clicks = clicks.Select(ce => new ClickDto
            {
                ClickedAt = ce.ClickedAt,
                Referrer = ce.Referrer,
                UserAgent = ce.UserAgent,
                IpAddress = ce.IpAddress
            }).ToList()
        };
    }
}
