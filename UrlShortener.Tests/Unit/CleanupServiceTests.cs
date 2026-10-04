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

/// <summary>
/// Tests for ExpiredUrlCleanupService (Brownfield Phase 5).
/// </summary>
public sealed class CleanupServiceTests
{
    private readonly Mock<IUrlRepository> _urlRepositoryMock = new();
    private readonly Mock<ICacheService> _cacheServiceMock = new();
    private readonly IOptions<CacheOptions> _cacheOptions = Options.Create(new CacheOptions { KeyPrefix = "url:", DefaultTtlHours = 1.0 });
    private readonly Mock<ILogger<ExpiredUrlCleanupService>> _loggerMock = new();

    private ExpiredUrlCleanupService CreateSut()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_urlRepositoryMock.Object);
        services.AddSingleton(_cacheServiceMock.Object);
        var serviceProvider = services.BuildServiceProvider();
        return new ExpiredUrlCleanupService(serviceProvider, _cacheOptions, _loggerMock.Object);
    }

    [Fact]
    public async Task CleanupAsync_WithExpiredUrls_DeletesFromDbAndEvictsCache()
    {
        var expiredUrls = new List<ShortenedUrl>
        {
            new() { ShortCode = "expired1", OriginalUrl = "https://example.com/1", ExpiresAt = DateTimeOffset.UtcNow.AddHours(-1) },
            new() { ShortCode = "expired2", OriginalUrl = "https://example.com/2", ExpiresAt = DateTimeOffset.UtcNow.AddHours(-2) }
        };

        _urlRepositoryMock
            .Setup(r => r.GetExpiredAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expiredUrls);
        _urlRepositoryMock
            .Setup(r => r.DeleteRangeAsync(It.IsAny<IEnumerable<ShortenedUrl>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _cacheServiceMock
            .Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = CreateSut();

        // Use CancellationToken to trigger the cleanup immediately via reflection
        using var cts = new CancellationTokenSource();

        // Access the private method via InvokeCleanupDirectly
        await InvokeCleanupAsync(sut, cts.Token);

        _cacheServiceMock.Verify(c => c.RemoveAsync("url:expired1", It.IsAny<CancellationToken>()), Times.Once);
        _cacheServiceMock.Verify(c => c.RemoveAsync("url:expired2", It.IsAny<CancellationToken>()), Times.Once);
        _urlRepositoryMock.Verify(r => r.DeleteRangeAsync(
            It.Is<IEnumerable<ShortenedUrl>>(urls => urls.Count() == 2),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CleanupAsync_NoExpiredUrls_DoesNotDeleteAnything()
    {
        _urlRepositoryMock
            .Setup(r => r.GetExpiredAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ShortenedUrl>());

        var sut = CreateSut();
        using var cts = new CancellationTokenSource();
        await InvokeCleanupAsync(sut, cts.Token);

        _urlRepositoryMock.Verify(r => r.DeleteRangeAsync(It.IsAny<IEnumerable<ShortenedUrl>>(), It.IsAny<CancellationToken>()), Times.Never);
        _cacheServiceMock.Verify(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CleanupAsync_RepositoryThrows_DoesNotPropagateException()
    {
        _urlRepositoryMock
            .Setup(r => r.GetExpiredAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("DB error"));

        var sut = CreateSut();
        using var cts = new CancellationTokenSource();

        // Should not throw
        Func<Task> act = () => InvokeCleanupAsync(sut, cts.Token);
        await act.Should().NotThrowAsync();
    }

    // Helper to invoke the private CleanupExpiredUrlsAsync via reflection
    private static async Task InvokeCleanupAsync(ExpiredUrlCleanupService sut, CancellationToken cancellationToken)
    {
        var method = typeof(ExpiredUrlCleanupService)
            .GetMethod("CleanupExpiredUrlsAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?? throw new InvalidOperationException("Method CleanupExpiredUrlsAsync not found.");

        await (Task)method.Invoke(sut, [cancellationToken])!;
    }
}
