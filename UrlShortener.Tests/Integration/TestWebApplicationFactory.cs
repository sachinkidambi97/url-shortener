using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using UrlShortener.Api.Data;

namespace UrlShortener.Tests.Integration;

public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .WithDatabase("urlshortener_test")
        .WithUsername("testuser")
        .WithPassword("testpass")
        .Build();

    private readonly RedisContainer _redisContainer = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(
            _postgresContainer.StartAsync(),
            _redisContainer.StartAsync());
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await Task.WhenAll(
            _postgresContainer.DisposeAsync().AsTask(),
            _redisContainer.DisposeAsync().AsTask());
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _postgresContainer.GetConnectionString(),
                ["ConnectionStrings:Redis"] = _redisContainer.GetConnectionString(),
                ["RateLimiting:PermitLimit"] = "10000"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Replace EF Core DbContext to use test PostgreSQL container
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor is not null)
                services.Remove(descriptor);

            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(_postgresContainer.GetConnectionString()));

            // Replace Redis connection (allowAdmin for FLUSHALL in test cleanup)
            var redisDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IConnectionMultiplexer));
            if (redisDescriptor is not null)
                services.Remove(redisDescriptor);

            var redisConnStr = _redisContainer.GetConnectionString() + ",allowAdmin=true";
            services.AddSingleton<IConnectionMultiplexer>(_ =>
                ConnectionMultiplexer.Connect(redisConnStr));
        });

        builder.UseEnvironment("Test");
    }

    /// <summary>
    /// Creates an HttpClient that does NOT follow redirects.
    /// Useful for testing redirect endpoints.
    /// </summary>
    public HttpClient CreateNonRedirectingClient()
    {
        return CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    /// <summary>
    /// Resets database state by deleting all rows from all tables.
    /// Call at the start of each integration test that needs a clean slate.
    /// </summary>
    public async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.ClickEvents.ExecuteDeleteAsync();
        await db.ShortenedUrls.ExecuteDeleteAsync();
    }

    /// <summary>
    /// Flushes all Redis keys.
    /// </summary>
    public async Task ResetCacheAsync()
    {
        var redis = Services.GetRequiredService<IConnectionMultiplexer>();
        var endpoints = redis.GetEndPoints();
        var server = redis.GetServer(endpoints[0]);
        await server.FlushAllDatabasesAsync();
    }
}
