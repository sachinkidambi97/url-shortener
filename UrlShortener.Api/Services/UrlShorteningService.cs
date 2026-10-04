using System.Text.RegularExpressions;
using UrlShortener.Api.Exceptions;
using UrlShortener.Api.Models;
using UrlShortener.Api.Models.Dtos;
using UrlShortener.Api.Repositories;

namespace UrlShortener.Api.Services;

public sealed partial class UrlShorteningService(IUrlRepository urlRepository, ICacheService cacheService) : IUrlShorteningService
{
    private const int MaxRetries = 3;
    private const int BaseCodeLength = 7;
    private const int AliasMinLength = 3;
    private const int AliasMaxLength = 30;

    [GeneratedRegex(@"^[a-zA-Z0-9\-]+$", RegexOptions.Compiled)]
    private static partial Regex AliasRegex();

    public async Task<ShortenResponse> ShortenAsync(
        ShortenRequest request,
        string baseUrl,
        int? userId = null,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException($"Invalid URL: '{request.Url}'. Must be an absolute http or https URL.", nameof(request));
        }

        if (request.ExpiresAt.HasValue && request.ExpiresAt.Value <= DateTimeOffset.UtcNow)
        {
            throw new ArgumentException("ExpiresAt must be a future date.", nameof(request));
        }

        string shortCode;
        if (request.Alias is not null)
        {
            shortCode = await ResolveAliasAsync(request.Alias, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            shortCode = await GenerateUniqueCodeAsync(cancellationToken).ConfigureAwait(false);
        }

        var shortenedUrl = new ShortenedUrl
        {
            ShortCode = shortCode,
            OriginalUrl = request.Url,
            Alias = request.Alias,
            ExpiresAt = request.ExpiresAt,
            UserId = userId
        };

        await urlRepository.CreateAsync(shortenedUrl, cancellationToken).ConfigureAwait(false);

        return new ShortenResponse
        {
            ShortCode = shortCode,
            ShortUrl = $"{baseUrl.TrimEnd('/')}/{shortCode}"
        };
    }

    public async Task<ShortenResponse> UpdateUrlAsync(
        string code,
        string newUrl,
        int userId,
        string baseUrl,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(newUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException($"Invalid URL: '{newUrl}'. Must be an absolute http or https URL.", nameof(newUrl));
        }

        var existing = await urlRepository.GetByShortCodeAsync(code, cancellationToken).ConfigureAwait(false);
        if (existing is null)
            throw new KeyNotFoundException($"Short code '{code}' not found.");

        if (existing.UserId is null || existing.UserId != userId)
            throw new ForbiddenAccessException($"You do not have permission to update short code '{code}'.");

        existing.OriginalUrl = newUrl;
        await urlRepository.UpdateAsync(existing, cancellationToken).ConfigureAwait(false);
        await cacheService.RemoveAsync(code, cancellationToken).ConfigureAwait(false);

        return new ShortenResponse
        {
            ShortCode = code,
            ShortUrl = $"{baseUrl.TrimEnd('/')}/{code}"
        };
    }

    public async Task DeleteUrlAsync(
        string code,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var existing = await urlRepository.GetByShortCodeAsync(code, cancellationToken).ConfigureAwait(false);
        if (existing is null)
            throw new KeyNotFoundException($"Short code '{code}' not found.");

        if (existing.UserId is null || existing.UserId != userId)
            throw new ForbiddenAccessException($"You do not have permission to delete short code '{code}'.");

        await urlRepository.DeleteAsync(existing, cancellationToken).ConfigureAwait(false);
        await cacheService.RemoveAsync(code, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> ResolveAliasAsync(string alias, CancellationToken cancellationToken)
    {
        if (alias.Length < AliasMinLength || alias.Length > AliasMaxLength)
        {
            throw new ArgumentException(
                $"Alias must be between {AliasMinLength} and {AliasMaxLength} characters.", "alias");
        }

        if (!AliasRegex().IsMatch(alias))
        {
            throw new ArgumentException(
                "Alias may only contain alphanumeric characters and hyphens.", "alias");
        }

        bool exists = await urlRepository.ExistsByShortCodeAsync(alias, cancellationToken).ConfigureAwait(false);
        if (exists)
        {
            throw new InvalidOperationException($"Alias '{alias}' is already taken.");
        }

        return alias;
    }

    private async Task<string> GenerateUniqueCodeAsync(CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            int codeLength = BaseCodeLength + attempt;
            string code = ShortCodeGenerator.Generate(codeLength);

            bool exists = await urlRepository.ExistsByShortCodeAsync(code, cancellationToken).ConfigureAwait(false);
            if (!exists)
                return code;
        }

        throw new InvalidOperationException("Failed to generate a unique short code after maximum retries.");
    }
}
