using Npgsql;
using Polly;
using Polly.Retry;
using StackExchange.Redis;

namespace UrlShortener.Api.Infrastructure;

/// <summary>
/// Defines Polly retry policies for transient failures in PostgreSQL and Redis.
/// </summary>
public static class ResiliencePolicies
{
    /// <summary>
    /// Retry policy for transient PostgreSQL exceptions: 3 retries with exponential backoff.
    /// Handles NpgsqlException (network/timeout) and SocketException.
    /// </summary>
    public static IAsyncPolicy CreateDatabaseRetryPolicy(ILogger logger)
    {
        return Policy
            .Handle<NpgsqlException>(ex => ex.IsTransient)
            .Or<TimeoutException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetry: (exception, timeSpan, attempt, _) =>
                {
                    logger.LogWarning(exception,
                        "Database transient failure. Retrying attempt {Attempt} after {Delay}s.",
                        attempt, timeSpan.TotalSeconds);
                });
    }

    /// <summary>
    /// Retry policy for transient Redis exceptions: 3 retries with exponential backoff.
    /// Handles RedisConnectionException and RedisTimeoutException.
    /// </summary>
    public static IAsyncPolicy CreateRedisRetryPolicy(ILogger logger)
    {
        return Policy
            .Handle<RedisConnectionException>()
            .Or<RedisTimeoutException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetry: (exception, timeSpan, attempt, _) =>
                {
                    logger.LogWarning(exception,
                        "Redis transient failure. Retrying attempt {Attempt} after {Delay}s.",
                        attempt, timeSpan.TotalSeconds);
                });
    }
}
