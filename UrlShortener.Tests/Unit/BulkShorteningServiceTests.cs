using FluentAssertions;
using Moq;
using UrlShortener.Api.Models;
using UrlShortener.Api.Models.Dtos;
using UrlShortener.Api.Repositories;
using UrlShortener.Api.Services;

namespace UrlShortener.Tests.Unit;

public sealed class BulkShorteningServiceTests
{
    private readonly Mock<IUrlRepository> _urlRepositoryMock = new();
    private readonly Mock<ICacheService> _cacheServiceMock = new();
    private readonly UrlShorteningService _sut;

    public BulkShorteningServiceTests()
    {
        _sut = new UrlShorteningService(_urlRepositoryMock.Object, _cacheServiceMock.Object);

        _urlRepositoryMock
            .Setup(r => r.ExistsByShortCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _urlRepositoryMock
            .Setup(r => r.CreateAsync(It.IsAny<ShortenedUrl>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShortenedUrl u, CancellationToken _) => u);
    }

    [Fact]
    public async Task BulkShortenAsync_AllValidUrls_ReturnsAllSuccessResults()
    {
        var request = new BulkShortenRequest
        {
            Items =
            [
                new ShortenRequest { Url = "https://example.com" },
                new ShortenRequest { Url = "https://another.com" }
            ]
        };

        var response = await _sut.BulkShortenAsync(request, "https://short.ly", userId: 1);

        response.Results.Should().HaveCount(2);
        response.Results.Should().AllSatisfy(r =>
        {
            r.Success.Should().BeTrue();
            r.ShortCode.Should().NotBeNullOrEmpty();
            r.ShortUrl.Should().StartWith("https://short.ly/");
            r.Error.Should().BeNull();
        });
        response.Results[0].Url.Should().Be("https://example.com");
        response.Results[1].Url.Should().Be("https://another.com");
    }

    [Fact]
    public async Task BulkShortenAsync_MixedValidAndInvalidUrls_ReturnsPartialSuccess()
    {
        var request = new BulkShortenRequest
        {
            Items =
            [
                new ShortenRequest { Url = "https://valid.com" },
                new ShortenRequest { Url = "not-a-url" },
                new ShortenRequest { Url = "https://also-valid.com" }
            ]
        };

        var response = await _sut.BulkShortenAsync(request, "https://short.ly", userId: 1);

        response.Results.Should().HaveCount(3);
        response.Results[0].Success.Should().BeTrue();
        response.Results[0].Url.Should().Be("https://valid.com");
        response.Results[1].Success.Should().BeFalse();
        response.Results[1].Url.Should().Be("not-a-url");
        response.Results[1].Error.Should().NotBeNullOrEmpty();
        response.Results[2].Success.Should().BeTrue();
        response.Results[2].Url.Should().Be("https://also-valid.com");
    }

    [Fact]
    public async Task BulkShortenAsync_EmptyList_ReturnsEmptyResults()
    {
        var request = new BulkShortenRequest { Items = [] };

        var response = await _sut.BulkShortenAsync(request, "https://short.ly", userId: 1);

        response.Results.Should().BeEmpty();
    }

    [Fact]
    public async Task BulkShortenAsync_ExactlyOneHundredUrls_Succeeds()
    {
        var items = Enumerable.Range(1, 100)
            .Select(i => new ShortenRequest { Url = $"https://example{i}.com" })
            .ToList();
        var request = new BulkShortenRequest { Items = items };

        var response = await _sut.BulkShortenAsync(request, "https://short.ly", userId: 1);

        response.Results.Should().HaveCount(100);
        response.Results.Should().AllSatisfy(r => r.Success.Should().BeTrue());
    }

    [Fact]
    public async Task BulkShortenAsync_OverHundredUrls_ThrowsArgumentException()
    {
        var items = Enumerable.Range(1, 101)
            .Select(i => new ShortenRequest { Url = $"https://example{i}.com" })
            .ToList();
        var request = new BulkShortenRequest { Items = items };

        Func<Task> act = () => _sut.BulkShortenAsync(request, "https://short.ly", userId: 1);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*100*");
    }

    [Fact]
    public async Task BulkShortenAsync_AllInvalidUrls_ReturnsAllFailedResults()
    {
        var request = new BulkShortenRequest
        {
            Items =
            [
                new ShortenRequest { Url = "not-a-url" },
                new ShortenRequest { Url = "ftp://files.example.com/file" }
            ]
        };

        var response = await _sut.BulkShortenAsync(request, "https://short.ly", userId: 1);

        response.Results.Should().HaveCount(2);
        response.Results.Should().AllSatisfy(r =>
        {
            r.Success.Should().BeFalse();
            r.Error.Should().NotBeNullOrEmpty();
            r.ShortCode.Should().BeNull();
            r.ShortUrl.Should().BeNull();
        });
    }

    [Fact]
    public async Task BulkShortenAsync_FailedItemDoesNotBlockOtherItems()
    {
        // Arrange: first URL will fail (invalid), second should still succeed
        var request = new BulkShortenRequest
        {
            Items =
            [
                new ShortenRequest { Url = "bad-url" },
                new ShortenRequest { Url = "https://good.com" }
            ]
        };

        var response = await _sut.BulkShortenAsync(request, "https://short.ly", userId: 1);

        response.Results[0].Success.Should().BeFalse();
        response.Results[1].Success.Should().BeTrue();
        _urlRepositoryMock.Verify(r => r.CreateAsync(It.IsAny<ShortenedUrl>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BulkShortenAsync_NullUserId_IsAllowed()
    {
        var request = new BulkShortenRequest
        {
            Items = [new ShortenRequest { Url = "https://example.com" }]
        };

        var response = await _sut.BulkShortenAsync(request, "https://short.ly", userId: null);

        response.Results.Should().HaveCount(1);
        response.Results[0].Success.Should().BeTrue();
    }

    [Fact]
    public async Task BulkShortenAsync_PreservesInputUrlInResult()
    {
        const string inputUrl = "https://example.com/path?foo=bar&baz=qux";
        var request = new BulkShortenRequest
        {
            Items = [new ShortenRequest { Url = inputUrl }]
        };

        var response = await _sut.BulkShortenAsync(request, "https://short.ly", userId: 1);

        response.Results[0].Url.Should().Be(inputUrl);
    }
}
