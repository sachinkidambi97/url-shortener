using Microsoft.Extensions.Options;
using Polly;
using StackExchange.Redis;
using UrlShortener.Api.Infrastructure;

namespace UrlShortener.Api.Services;

public sealed class RedisCacheService(
    IConnectionMultiplexer connectionMultiplexer,
    IOptions<CacheOptions> options,
    ILogger<RedisCacheService> logger) : ICacheService
{
    private readonly IDatabase _database = connectionMultiplexer.GetDatabase();
    private readonly TimeSpan _defaultTtl = TimeSpan.FromHours(options.Value.DefaultTtlHours);
    private readonly IAsyncPolicy _retryPolicy = ResiliencePolicies.CreateRedisRetryPolicy(logger);

    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var value = await _retryPolicy.ExecuteAsync(() =>
                _database.StringGetAsync(key))
                .ConfigureAwait(false);
            if (value.IsNullOrEmpty)
            {
                logger.LogDebug("Cache miss for key {Key}", key);
                return null;
            }
            return value.ToString();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Redis GET failed for key {Key} after retries. Falling back to database.", key);
            return null;
        }
    }

    public async Task SetAsync(string key, string value, TimeSpan? ttl = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var expiry = ttl ?? _defaultTtl;
            await _retryPolicy.ExecuteAsync(() =>
                _database.StringSetAsync(key, value, expiry))
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Redis SET failed for key {Key} after retries. Cache not populated.", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await _retryPolicy.ExecuteAsync(() =>
                _database.KeyDeleteAsync(key))
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Redis DELETE failed for key {Key} after retries.", key);
        }
    }
}
