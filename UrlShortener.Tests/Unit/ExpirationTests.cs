using FluentAssertions;
using Moq;
using UrlShortener.Api.Models;
using UrlShortener.Api.Models.Dtos;
using UrlShortener.Api.Repositories;
using UrlShortener.Api.Services;

namespace UrlShortener.Tests.Unit;

/// <summary>
/// Tests for URL expiration features (Brownfield Phase 5).
/// </summary>
public sealed class ExpirationTests
{
    private readonly Mock<IUrlRepository> _urlRepositoryMock = new();
    private readonly UrlShorteningService _sut;

    public ExpirationTests()
    {
        _urlRepositoryMock
            .Setup(r => r.ExistsByShortCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _urlRepositoryMock
            .Setup(r => r.CreateAsync(It.IsAny<ShortenedUrl>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShortenedUrl u, CancellationToken _) => u);

        _sut = new UrlShorteningService(_urlRepositoryMock.Object);
    }

    [Fact]
    public async Task ShortenAsync_FutureExpiresAt_StoresExpiration()
    {
        ShortenedUrl? created = null;
        _urlRepositoryMock
            .Setup(r => r.CreateAsync(It.IsAny<ShortenedUrl>(), It.IsAny<CancellationToken>()))
            .Callback<ShortenedUrl, CancellationToken>((u, _) => created = u)
            .ReturnsAsync((ShortenedUrl u, CancellationToken _) => u);

        var expiresAt = DateTimeOffset.UtcNow.AddDays(7);
        var request = new ShortenRequest { Url = "https://example.com", ExpiresAt = expiresAt };
        await _sut.ShortenAsync(request, "https://short.ly");

        created.Should().NotBeNull();
        created!.ExpiresAt.Should().Be(expiresAt);
    }

    [Fact]
    public async Task ShortenAsync_PastExpiresAt_ThrowsArgumentException()
    {
        var expiresAt = DateTimeOffset.UtcNow.AddHours(-1);
        var request = new ShortenRequest { Url = "https://example.com", ExpiresAt = expiresAt };
        Func<Task> act = () => _sut.ShortenAsync(request, "https://short.ly");

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*future date*");
    }

    [Fact]
    public async Task ShortenAsync_NoExpiresAt_StoresNullExpiration()
    {
        ShortenedUrl? created = null;
        _urlRepositoryMock
            .Setup(r => r.CreateAsync(It.IsAny<ShortenedUrl>(), It.IsAny<CancellationToken>()))
            .Callback<ShortenedUrl, CancellationToken>((u, _) => created = u)
            .ReturnsAsync((ShortenedUrl u, CancellationToken _) => u);

        var request = new ShortenRequest { Url = "https://example.com" };
        await _sut.ShortenAsync(request, "https://short.ly");

        created.Should().NotBeNull();
        created!.ExpiresAt.Should().BeNull();
    }
}
