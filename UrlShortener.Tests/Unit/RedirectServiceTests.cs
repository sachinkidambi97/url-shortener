using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using UrlShortener.Api.Infrastructure;
using UrlShortener.Api.Models;
using UrlShortener.Api.Repositories;
using UrlShortener.Api.Services;

namespace UrlShortener.Tests.Unit;

public sealed class RedirectServiceTests
{
    private readonly Mock<ICacheService> _cacheServiceMock = new();
    private readonly Mock<IUrlRepository> _urlRepositoryMock = new();
    private readonly Mock<IAnalyticsService> _analyticsServiceMock = new();
    private readonly RedirectService _sut;

    public RedirectServiceTests()
    {
        var cacheOptions = Options.Create(new CacheOptions { KeyPrefix = "url:", DefaultTtlHours = 1.0 });
        var logger = new Mock<ILogger<RedirectService>>();

        _analyticsServiceMock
            .Setup(a => a.RecordClickAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var scopeFactory = new Mock<IServiceScopeFactory>();
        var scope = new Mock<IServiceScope>();
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(sp => sp.GetService(typeof(IAnalyticsService))).Returns(_analyticsServiceMock.Object);
        scope.Setup(s => s.ServiceProvider).Returns(serviceProvider.Object);
        scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);

        _sut = new RedirectService(
            _cacheServiceMock.Object,
            _urlRepositoryMock.Object,
            _analyticsServiceMock.Object,
            scopeFactory.Object,
            cacheOptions,
            logger.Object);
    }

    [Fact]
    public async Task GetOriginalUrlAsync_CacheHit_ReturnsCachedUrl()
    {
        _cacheServiceMock
            .Setup(c => c.GetAsync("url:abc1234", It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://example.com");

        var result = await _sut.GetOriginalUrlAsync("abc1234", "1.2.3.4", "Mozilla/5.0", null);

        result.OriginalUrl.Should().Be("https://example.com");
        result.IsExpired.Should().BeFalse();
        _urlRepositoryMock.Verify(r => r.GetByShortCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetOriginalUrlAsync_CacheMiss_QueriesDbAndPopulatesCache()
    {
        _cacheServiceMock
            .Setup(c => c.GetAsync("url:abc1234", It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        _urlRepositoryMock
            .Setup(r => r.GetByShortCodeAsync("abc1234", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ShortenedUrl { ShortCode = "abc1234", OriginalUrl = "https://example.com" });

        var result = await _sut.GetOriginalUrlAsync("abc1234", "1.2.3.4", "Mozilla/5.0", null);

        result.OriginalUrl.Should().Be("https://example.com");
        result.IsExpired.Should().BeFalse();
        _cacheServiceMock.Verify(
            c => c.SetAsync("url:abc1234", "https://example.com", null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetOriginalUrlAsync_UnknownCode_ReturnsNullNotExpired()
    {
        _cacheServiceMock
            .Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        _urlRepositoryMock
            .Setup(r => r.GetByShortCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShortenedUrl?)null);

        var result = await _sut.GetOriginalUrlAsync("unknown", null, null, null);

        result.OriginalUrl.Should().BeNull();
        result.IsExpired.Should().BeFalse();
    }

    [Fact]
    public async Task GetOriginalUrlAsync_CacheMiss_PopulatesCacheWithCorrectKey()
    {
        const string shortCode = "xyz9876";
        _cacheServiceMock
            .Setup(c => c.GetAsync($"url:{shortCode}", It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        _urlRepositoryMock
            .Setup(r => r.GetByShortCodeAsync(shortCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ShortenedUrl { ShortCode = shortCode, OriginalUrl = "https://example.com/page" });

        await _sut.GetOriginalUrlAsync(shortCode, null, null, null);

        _cacheServiceMock.Verify(
            c => c.SetAsync($"url:{shortCode}", "https://example.com/page", null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetOriginalUrlAsync_ExpiredUrl_ReturnsIsExpiredTrue()
    {
        _cacheServiceMock
            .Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        _urlRepositoryMock
            .Setup(r => r.GetByShortCodeAsync("expired", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ShortenedUrl
            {
                ShortCode = "expired",
                OriginalUrl = "https://example.com",
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(-1)
            });

        var result = await _sut.GetOriginalUrlAsync("expired", null, null, null);

        result.IsExpired.Should().BeTrue();
        result.OriginalUrl.Should().BeNull();
        _cacheServiceMock.Verify(
            c => c.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetOriginalUrlAsync_UrlNotYetExpired_ReturnsUrlNormally()
    {
        _cacheServiceMock
            .Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        _urlRepositoryMock
            .Setup(r => r.GetByShortCodeAsync("active", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ShortenedUrl
            {
                ShortCode = "active",
                OriginalUrl = "https://example.com",
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
            });

        var result = await _sut.GetOriginalUrlAsync("active", null, null, null);

        result.IsExpired.Should().BeFalse();
        result.OriginalUrl.Should().Be("https://example.com");
    }
}
