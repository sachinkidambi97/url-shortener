using FluentAssertions;
using Moq;
using UrlShortener.Api.Models;
using UrlShortener.Api.Models.Dtos;
using UrlShortener.Api.Repositories;
using UrlShortener.Api.Services;

namespace UrlShortener.Tests.Unit;

public sealed class UrlShorteningServiceTests
{
    private readonly Mock<IUrlRepository> _urlRepositoryMock = new();
    private readonly UrlShorteningService _sut;

    public UrlShorteningServiceTests()
    {
        _sut = new UrlShorteningService(_urlRepositoryMock.Object);
    }

    [Fact]
    public async Task ShortenAsync_ValidHttpUrl_ReturnsShortCode()
    {
        _urlRepositoryMock
            .Setup(r => r.ExistsByShortCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _urlRepositoryMock
            .Setup(r => r.CreateAsync(It.IsAny<ShortenedUrl>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShortenedUrl u, CancellationToken _) => u);

        var request = new ShortenRequest { Url = "https://example.com" };
        var result = await _sut.ShortenAsync(request, "https://short.ly");

        result.Should().NotBeNull();
        result.ShortCode.Should().NotBeNullOrEmpty();
        result.ShortUrl.Should().StartWith("https://short.ly/");
        result.ShortUrl.Should().EndWith(result.ShortCode);
    }

    [Fact]
    public async Task ShortenAsync_ValidHttpUrlWithPath_ReturnsShortCode()
    {
        _urlRepositoryMock
            .Setup(r => r.ExistsByShortCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _urlRepositoryMock
            .Setup(r => r.CreateAsync(It.IsAny<ShortenedUrl>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShortenedUrl u, CancellationToken _) => u);

        var request = new ShortenRequest { Url = "http://example.com/path?q=1" };
        var result = await _sut.ShortenAsync(request, "https://short.ly");

        result.ShortCode.Should().HaveLength(7);
    }

    [Fact]
    public async Task ShortenAsync_InvalidUrl_ThrowsArgumentException()
    {
        var request = new ShortenRequest { Url = "not-a-url" };
        Func<Task> act = () => _sut.ShortenAsync(request, "https://short.ly");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ShortenAsync_FtpUrl_ThrowsArgumentException()
    {
        var request = new ShortenRequest { Url = "ftp://example.com/file" };
        Func<Task> act = () => _sut.ShortenAsync(request, "https://short.ly");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ShortenAsync_EmptyUrl_ThrowsArgumentException()
    {
        var request = new ShortenRequest { Url = "" };
        Func<Task> act = () => _sut.ShortenAsync(request, "https://short.ly");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ShortenAsync_CollisionOnFirstAttempt_RetriesAndSucceeds()
    {
        var callCount = 0;
        _urlRepositoryMock
            .Setup(r => r.ExistsByShortCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1; // first call returns true (exists), second returns false
            });
        _urlRepositoryMock
            .Setup(r => r.CreateAsync(It.IsAny<ShortenedUrl>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShortenedUrl u, CancellationToken _) => u);

        var request = new ShortenRequest { Url = "https://example.com" };
        var result = await _sut.ShortenAsync(request, "https://short.ly");

        result.Should().NotBeNull();
        callCount.Should().Be(2);
    }

    [Fact]
    public async Task ShortenAsync_AllCollisions_ThrowsInvalidOperationException()
    {
        _urlRepositoryMock
            .Setup(r => r.ExistsByShortCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true); // always exists

        var request = new ShortenRequest { Url = "https://example.com" };
        Func<Task> act = () => _sut.ShortenAsync(request, "https://short.ly");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*unique short code*");
    }

    [Fact]
    public async Task ShortenAsync_ShortCodeLength_IncreasesWithRetries()
    {
        var generatedCodes = new List<string>();
        _urlRepositoryMock
            .Setup(r => r.ExistsByShortCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string code, CancellationToken _) =>
            {
                generatedCodes.Add(code);
                return generatedCodes.Count < 3; // first 2 exist, 3rd succeeds
            });
        _urlRepositoryMock
            .Setup(r => r.CreateAsync(It.IsAny<ShortenedUrl>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShortenedUrl u, CancellationToken _) => u);

        var request = new ShortenRequest { Url = "https://example.com" };
        await _sut.ShortenAsync(request, "https://short.ly");

        generatedCodes.Should().HaveCount(3);
        generatedCodes[0].Should().HaveLength(7);
        generatedCodes[1].Should().HaveLength(8);
        generatedCodes[2].Should().HaveLength(9);
    }
}
