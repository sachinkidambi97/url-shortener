using UrlShortener.Api.Models;

namespace UrlShortener.Api.Repositories;

public interface IClickEventRepository
{
    Task RecordClickAsync(ClickEvent clickEvent, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ClickEvent>> GetByShortCodeAsync(string shortCode, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<long> GetCountByShortCodeAsync(string shortCode, CancellationToken cancellationToken = default);
}
