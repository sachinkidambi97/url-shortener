using FluentAssertions;
using Moq;
using UrlShortener.Api.Exceptions;
using UrlShortener.Api.Models;
using UrlShortener.Api.Models.Dtos;
using UrlShortener.Api.Repositories;
using UrlShortener.Api.Services;

namespace UrlShortener.Tests.Unit;

public sealed class UrlEditDeleteServiceTests
{
    private readonly Mock<IUrlRepository> _urlRepositoryMock = new();
    private readonly Mock<ICacheService> _cacheServiceMock = new();
    private readonly UrlShorteningService _sut;

    public UrlEditDeleteServiceTests()
    {
        _sut = new UrlShorteningService(_urlRepositoryMock.Object, _cacheServiceMock.Object);
    }

    // ---- UpdateUrlAsync tests ----

    [Fact]
    public async Task UpdateUrlAsync_ValidOwner_UpdatesUrlAndEvictsCache()
    {
        var existing = new ShortenedUrl
        {
            Id = 1,
            ShortCode = "abc1234",
            OriginalUrl = "https://old.example.com",
            UserId = 42
        };

        _urlRepositoryMock
            .Setup(r => r.GetByShortCodeAsync("abc1234", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _urlRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<ShortenedUrl>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShortenedUrl u, CancellationToken _) => u);

        var result = await _sut.UpdateUrlAsync("abc1234", "https://new.example.com", 42, "http://localhost");

        result.ShortCode.Should().Be("abc1234");
        result.ShortUrl.Should().Be("http://localhost/abc1234");

        _urlRepositoryMock.Verify(r => r.UpdateAsync(
            It.Is<ShortenedUrl>(u => u.OriginalUrl == "https://new.example.com"),
            It.IsAny<CancellationToken>()), Times.Once);

        _cacheServiceMock.Verify(c => c.RemoveAsync("abc1234", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateUrlAsync_CodeNotFound_ThrowsKeyNotFoundException()
    {
        _urlRepositoryMock
            .Setup(r => r.GetByShortCodeAsync("notexist", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShortenedUrl?)null);

        Func<Task> act = () => _sut.UpdateUrlAsync("notexist", "https://new.example.com", 42, "http://localhost");

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task UpdateUrlAsync_WrongOwner_ThrowsForbiddenAccessException()
    {
        var existing = new ShortenedUrl
        {
            Id = 1,
            ShortCode = "abc1234",
            OriginalUrl = "https://old.example.com",
            UserId = 99 // owned by user 99
        };

        _urlRepositoryMock
            .Setup(r => r.GetByShortCodeAsync("abc1234", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        // User 42 tries to update user 99's URL
        Func<Task> act = () => _sut.UpdateUrlAsync("abc1234", "https://new.example.com", 42, "http://localhost");

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task UpdateUrlAsync_NullOwner_ThrowsForbiddenAccessException()
    {
        // URL created before auth (UserId = null)
        var existing = new ShortenedUrl
        {
            Id = 1,
            ShortCode = "abc1234",
            OriginalUrl = "https://old.example.com",
            UserId = null
        };

        _urlRepositoryMock
            .Setup(r => r.GetByShortCodeAsync("abc1234", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        Func<Task> act = () => _sut.UpdateUrlAsync("abc1234", "https://new.example.com", 42, "http://localhost");

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task UpdateUrlAsync_InvalidNewUrl_ThrowsArgumentException()
    {
        var existing = new ShortenedUrl
        {
            Id = 1,
            ShortCode = "abc1234",
            OriginalUrl = "https://old.example.com",
            UserId = 42
        };

        _urlRepositoryMock
            .Setup(r => r.GetByShortCodeAsync("abc1234", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        Func<Task> act = () => _sut.UpdateUrlAsync("abc1234", "not-a-url", 42, "http://localhost");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task UpdateUrlAsync_FtpUrl_ThrowsArgumentException()
    {
        var existing = new ShortenedUrl
        {
            Id = 1,
            ShortCode = "abc1234",
            OriginalUrl = "https://old.example.com",
            UserId = 42
        };

        _urlRepositoryMock
            .Setup(r => r.GetByShortCodeAsync("abc1234", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        Func<Task> act = () => _sut.UpdateUrlAsync("abc1234", "ftp://files.example.com", 42, "http://localhost");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ---- DeleteUrlAsync tests ----

    [Fact]
    public async Task DeleteUrlAsync_ValidOwner_DeletesAndEvictsCache()
    {
        var existing = new ShortenedUrl
        {
            Id = 1,
            ShortCode = "abc1234",
            OriginalUrl = "https://old.example.com",
            UserId = 42
        };

        _urlRepositoryMock
            .Setup(r => r.GetByShortCodeAsync("abc1234", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _urlRepositoryMock
            .Setup(r => r.DeleteAsync(It.IsAny<ShortenedUrl>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _sut.DeleteUrlAsync("abc1234", 42);

        _urlRepositoryMock.Verify(r => r.DeleteAsync(
            It.Is<ShortenedUrl>(u => u.ShortCode == "abc1234"),
            It.IsAny<CancellationToken>()), Times.Once);

        _cacheServiceMock.Verify(c => c.RemoveAsync("abc1234", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteUrlAsync_CodeNotFound_ThrowsKeyNotFoundException()
    {
        _urlRepositoryMock
            .Setup(r => r.GetByShortCodeAsync("notexist", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShortenedUrl?)null);

        Func<Task> act = () => _sut.DeleteUrlAsync("notexist", 42);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task DeleteUrlAsync_WrongOwner_ThrowsForbiddenAccessException()
    {
        var existing = new ShortenedUrl
        {
            Id = 1,
            ShortCode = "abc1234",
            OriginalUrl = "https://old.example.com",
            UserId = 99
        };

        _urlRepositoryMock
            .Setup(r => r.GetByShortCodeAsync("abc1234", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        Func<Task> act = () => _sut.DeleteUrlAsync("abc1234", 42);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task DeleteUrlAsync_NullOwner_ThrowsForbiddenAccessException()
    {
        // URL created before auth
        var existing = new ShortenedUrl
        {
            Id = 1,
            ShortCode = "abc1234",
            OriginalUrl = "https://old.example.com",
            UserId = null
        };

        _urlRepositoryMock
            .Setup(r => r.GetByShortCodeAsync("abc1234", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        Func<Task> act = () => _sut.DeleteUrlAsync("abc1234", 42);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }
}
