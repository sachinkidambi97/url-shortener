using FluentAssertions;
using Moq;
using UrlShortener.Api.Models;
using UrlShortener.Api.Models.Dtos;
using UrlShortener.Api.Repositories;
using UrlShortener.Api.Services;

namespace UrlShortener.Tests.Unit;

public sealed class AnalyticsServiceTests
{
    private readonly Mock<IClickEventRepository> _clickEventRepositoryMock = new();
    private readonly Mock<IUrlRepository> _urlRepositoryMock = new();
    private readonly AnalyticsService _sut;

    public AnalyticsServiceTests()
    {
        _sut = new AnalyticsService(_clickEventRepositoryMock.Object, _urlRepositoryMock.Object);
    }

    [Fact]
    public async Task RecordClickAsync_PersistsClickEventWithCorrectFields()
    {
        ClickEvent? captured = null;
        _clickEventRepositoryMock
            .Setup(r => r.RecordClickAsync(It.IsAny<ClickEvent>(), It.IsAny<CancellationToken>()))
            .Callback<ClickEvent, CancellationToken>((ce, _) => captured = ce)
            .Returns(Task.CompletedTask);

        await _sut.RecordClickAsync("abc1234", "1.2.3.4", "Mozilla/5.0", "https://google.com");

        captured.Should().NotBeNull();
        captured!.ShortCode.Should().Be("abc1234");
        captured.IpAddress.Should().Be("1.2.3.4");
        captured.UserAgent.Should().Be("Mozilla/5.0");
        captured.Referrer.Should().Be("https://google.com");
    }

    [Fact]
    public async Task RecordClickAsync_NullOptionalFields_PersistsWithNulls()
    {
        ClickEvent? captured = null;
        _clickEventRepositoryMock
            .Setup(r => r.RecordClickAsync(It.IsAny<ClickEvent>(), It.IsAny<CancellationToken>()))
            .Callback<ClickEvent, CancellationToken>((ce, _) => captured = ce)
            .Returns(Task.CompletedTask);

        await _sut.RecordClickAsync("abc1234", null, null, null);

        captured!.IpAddress.Should().BeNull();
        captured.UserAgent.Should().BeNull();
        captured.Referrer.Should().BeNull();
    }

    [Fact]
    public async Task GetStatsAsync_UnknownCode_ReturnsNull()
    {
        _urlRepositoryMock
            .Setup(r => r.GetByShortCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShortenedUrl?)null);

        var result = await _sut.GetStatsAsync("unknown");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetStatsAsync_KnownCode_ReturnsAggregatedStats()
    {
        var url = new ShortenedUrl { ShortCode = "abc1234", OriginalUrl = "https://example.com" };
        var clicks = new List<ClickEvent>
        {
            new() { ShortCode = "abc1234", IpAddress = "1.1.1.1", UserAgent = "UA", ClickedAt = DateTimeOffset.UtcNow },
            new() { ShortCode = "abc1234", IpAddress = "2.2.2.2", UserAgent = "UA2", ClickedAt = DateTimeOffset.UtcNow.AddMinutes(-5) }
        };

        _urlRepositoryMock
            .Setup(r => r.GetByShortCodeAsync("abc1234", It.IsAny<CancellationToken>()))
            .ReturnsAsync(url);
        _clickEventRepositoryMock
            .Setup(r => r.GetCountByShortCodeAsync("abc1234", It.IsAny<CancellationToken>()))
            .ReturnsAsync(2L);
        _clickEventRepositoryMock
            .Setup(r => r.GetByShortCodeAsync("abc1234", 1, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(clicks);

        var result = await _sut.GetStatsAsync("abc1234");

        result.Should().NotBeNull();
        result!.ShortCode.Should().Be("abc1234");
        result.OriginalUrl.Should().Be("https://example.com");
        result.TotalClicks.Should().Be(2L);
        result.Clicks.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetStatsAsync_NoClicks_ReturnsZeroCountAndEmptyList()
    {
        _urlRepositoryMock
            .Setup(r => r.GetByShortCodeAsync("abc1234", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ShortenedUrl { ShortCode = "abc1234", OriginalUrl = "https://example.com" });
        _clickEventRepositoryMock
            .Setup(r => r.GetCountByShortCodeAsync("abc1234", It.IsAny<CancellationToken>()))
            .ReturnsAsync(0L);
        _clickEventRepositoryMock
            .Setup(r => r.GetByShortCodeAsync("abc1234", 1, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ClickEvent>());

        var result = await _sut.GetStatsAsync("abc1234");

        result!.TotalClicks.Should().Be(0);
        result.Clicks.Should().BeEmpty();
    }
}
