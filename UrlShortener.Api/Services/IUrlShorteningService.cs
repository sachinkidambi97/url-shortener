using UrlShortener.Api.Models.Dtos;

namespace UrlShortener.Api.Services;

public interface IUrlShorteningService
{
    Task<ShortenResponse> ShortenAsync(ShortenRequest request, string baseUrl, CancellationToken cancellationToken = default);
}
