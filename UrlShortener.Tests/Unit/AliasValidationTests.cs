using FluentAssertions;
using Moq;
using UrlShortener.Api.Models;
using UrlShortener.Api.Models.Dtos;
using UrlShortener.Api.Repositories;
using UrlShortener.Api.Services;

namespace UrlShortener.Tests.Unit;

/// <summary>
/// Tests for alias validation in UrlShorteningService (Brownfield Phase 5).
/// </summary>
public sealed class AliasValidationTests
{
    private readonly Mock<IUrlRepository> _urlRepositoryMock = new();
    private readonly Mock<ICacheService> _cacheServiceMock = new();
    private readonly UrlShorteningService _sut;

    public AliasValidationTests()
    {
        _urlRepositoryMock
            .Setup(r => r.CreateAsync(It.IsAny<ShortenedUrl>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShortenedUrl u, CancellationToken _) => u);

        _sut = new UrlShorteningService(_urlRepositoryMock.Object, _cacheServiceMock.Object);
    }

    [Fact]
    public async Task ShortenAsync_ValidAlias_UsesAliasAsShortCode()
    {
        _urlRepositoryMock
            .Setup(r => r.ExistsByShortCodeAsync("my-link", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var request = new ShortenRequest { Url = "https://example.com", Alias = "my-link" };
        var result = await _sut.ShortenAsync(request, "https://short.ly");

        result.ShortCode.Should().Be("my-link");
        result.ShortUrl.Should().Be("https://short.ly/my-link");
    }

    [Theory]
    [InlineData("ab")]         // too short (< 3 chars)
    [InlineData("a")]          // way too short
    [InlineData("")]           // empty
    public async Task ShortenAsync_AliasTooShort_ThrowsArgumentException(string alias)
    {
        var request = new ShortenRequest { Url = "https://example.com", Alias = alias };
        Func<Task> act = () => _sut.ShortenAsync(request, "https://short.ly");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ShortenAsync_AliasTooLong_ThrowsArgumentException()
    {
        string longAlias = new('a', 31); // 31 chars > 30 max
        var request = new ShortenRequest { Url = "https://example.com", Alias = longAlias };
        Func<Task> act = () => _sut.ShortenAsync(request, "https://short.ly");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData("my alias")]       // space not allowed
    [InlineData("my_alias")]       // underscore not allowed
    [InlineData("alias!")]         // exclamation not allowed
    [InlineData("alias.com")]      // dot not allowed
    public async Task ShortenAsync_AliasInvalidChars_ThrowsArgumentException(string alias)
    {
        var request = new ShortenRequest { Url = "https://example.com", Alias = alias };
        Func<Task> act = () => _sut.ShortenAsync(request, "https://short.ly");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData("abc")]                      // minimum length 3
    [InlineData("abc-def")]                  // hyphens OK
    [InlineData("ABC123")]                   // mixed case alphanumeric OK
    [InlineData("my-custom-link-2026")]      // longer with hyphens
    public async Task ShortenAsync_ValidAliasFormats_Succeed(string alias)
    {
        _urlRepositoryMock
            .Setup(r => r.ExistsByShortCodeAsync(alias, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var request = new ShortenRequest { Url = "https://example.com", Alias = alias };
        var result = await _sut.ShortenAsync(request, "https://short.ly");

        result.ShortCode.Should().Be(alias);
    }

    [Fact]
    public async Task ShortenAsync_DuplicateAlias_ThrowsInvalidOperationException()
    {
        _urlRepositoryMock
            .Setup(r => r.ExistsByShortCodeAsync("taken", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var request = new ShortenRequest { Url = "https://example.com", Alias = "taken" };
        Func<Task> act = () => _sut.ShortenAsync(request, "https://short.ly");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already taken*");
    }

    [Fact]
    public async Task ShortenAsync_NoAlias_GeneratesRandomCode()
    {
        _urlRepositoryMock
            .Setup(r => r.ExistsByShortCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var request = new ShortenRequest { Url = "https://example.com" }; // no alias
        var result = await _sut.ShortenAsync(request, "https://short.ly");

        result.ShortCode.Should().HaveLength(7);
    }

    [Fact]
    public async Task ShortenAsync_AliasStored_OnEntity()
    {
        ShortenedUrl? created = null;
        _urlRepositoryMock
            .Setup(r => r.ExistsByShortCodeAsync("myalias", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _urlRepositoryMock
            .Setup(r => r.CreateAsync(It.IsAny<ShortenedUrl>(), It.IsAny<CancellationToken>()))
            .Callback<ShortenedUrl, CancellationToken>((u, _) => created = u)
            .ReturnsAsync((ShortenedUrl u, CancellationToken _) => u);

        var request = new ShortenRequest { Url = "https://example.com", Alias = "myalias" };
        await _sut.ShortenAsync(request, "https://short.ly");

        created.Should().NotBeNull();
        created!.Alias.Should().Be("myalias");
        created.ShortCode.Should().Be("myalias");
    }
}
