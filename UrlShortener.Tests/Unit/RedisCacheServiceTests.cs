using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;
using UrlShortener.Api.Infrastructure;
using UrlShortener.Api.Services;

namespace UrlShortener.Tests.Unit;

public sealed class RedisCacheServiceTests
{
    private readonly Mock<IConnectionMultiplexer> _connectionMultiplexerMock = new();
    private readonly Mock<IDatabase> _databaseMock = new();
    private readonly Mock<ILogger<RedisCacheService>> _loggerMock = new();
    private readonly RedisCacheService _sut;

    public RedisCacheServiceTests()
    {
        _connectionMultiplexerMock
            .Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_databaseMock.Object);

        var cacheOptions = Options.Create(new CacheOptions { DefaultTtlHours = 1.0, KeyPrefix = "url:" });
        _sut = new RedisCacheService(_connectionMultiplexerMock.Object, cacheOptions, _loggerMock.Object);
    }

    [Fact]
    public async Task GetAsync_CachedValue_ReturnsString()
    {
        _databaseMock
            .Setup(d => d.StringGetAsync("mykey", It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)"myvalue");

        var result = await _sut.GetAsync("mykey");

        result.Should().Be("myvalue");
    }

    [Fact]
    public async Task GetAsync_MissingKey_ReturnsNull()
    {
        _databaseMock
            .Setup(d => d.StringGetAsync("missing", It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        var result = await _sut.GetAsync("missing");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_NonRetriableException_ReturnsNullAndLogsWarning()
    {
        // Use a non-retried exception type so Polly does not add delays.
        // The Polly policy only retries RedisConnectionException and RedisTimeoutException;
        // any other exception is caught by the outer try/catch and logs a warning.
        _databaseMock
            .Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new ObjectDisposedException("redis"));

        var result = await _sut.GetAsync("anykey");

        result.Should().BeNull();
        _loggerMock.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task SetAsync_SetsKeyWithDefaultTtl()
    {
        _databaseMock
            .Setup(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        await _sut.SetAsync("mykey", "myvalue");

        _databaseMock.Verify(
            d => d.StringSetAsync(
                "mykey",
                "myvalue",
                TimeSpan.FromHours(1.0),
                It.IsAny<bool>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()),
            Times.Once);
    }

    [Fact]
    public async Task SetAsync_WithCustomTtl_UsesCustomTtl()
    {
        var customTtl = TimeSpan.FromMinutes(30);
        _databaseMock
            .Setup(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        await _sut.SetAsync("mykey", "myvalue", customTtl);

        _databaseMock.Verify(
            d => d.StringSetAsync(
                "mykey",
                "myvalue",
                customTtl,
                It.IsAny<bool>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()),
            Times.Once);
    }

    [Fact]
    public async Task SetAsync_NonRetriableException_DoesNotPropagateAndLogsWarning()
    {
        // Use ObjectDisposedException which Polly does not retry, so no delays.
        _databaseMock
            .Setup(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new ObjectDisposedException("redis"));

        Func<Task> act = () => _sut.SetAsync("mykey", "myvalue");

        await act.Should().NotThrowAsync();
        _loggerMock.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task RemoveAsync_DeletesKey()
    {
        _databaseMock
            .Setup(d => d.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        await _sut.RemoveAsync("mykey");

        _databaseMock.Verify(
            d => d.KeyDeleteAsync("mykey", It.IsAny<CommandFlags>()),
            Times.Once);
    }

    [Fact]
    public async Task RemoveAsync_NonRetriableException_DoesNotPropagateAndLogsWarning()
    {
        // Use ObjectDisposedException which Polly does not retry, so no delays.
        _databaseMock
            .Setup(d => d.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new ObjectDisposedException("redis"));

        Func<Task> act = () => _sut.RemoveAsync("mykey");

        await act.Should().NotThrowAsync();
        _loggerMock.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
}
