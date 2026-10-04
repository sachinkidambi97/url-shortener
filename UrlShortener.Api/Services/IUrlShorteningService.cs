using UrlShortener.Api.Models.Dtos;

namespace UrlShortener.Api.Services;

public interface IUrlShorteningService
{
    Task<ShortenResponse> ShortenAsync(ShortenRequest request, string baseUrl, int? userId = null, CancellationToken cancellationToken = default);
    Task<BulkShortenResponse> BulkShortenAsync(BulkShortenRequest request, string baseUrl, int? userId = null, CancellationToken cancellationToken = default);
    Task<ShortenResponse> UpdateUrlAsync(string code, string newUrl, int userId, string baseUrl, CancellationToken cancellationToken = default);
    Task DeleteUrlAsync(string code, int userId, CancellationToken cancellationToken = default);
}
