using Microsoft.EntityFrameworkCore;
using Polly;
using UrlShortener.Api.Data;
using UrlShortener.Api.Infrastructure;
using UrlShortener.Api.Models;

namespace UrlShortener.Api.Repositories;

public sealed class UserRepository(AppDbContext dbContext, ILogger<UserRepository> logger) : IUserRepository
{
    private readonly IAsyncPolicy _retryPolicy = ResiliencePolicies.CreateDatabaseRetryPolicy(logger);

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        => await _retryPolicy.ExecuteAsync(() =>
            dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Email == email, cancellationToken))
            .ConfigureAwait(false);

    public async Task<User> CreateAsync(User user, CancellationToken cancellationToken = default)
    {
        user.CreatedAt = DateTimeOffset.UtcNow;
        dbContext.Users.Add(user);
        await _retryPolicy.ExecuteAsync(() =>
            dbContext.SaveChangesAsync(cancellationToken))
            .ConfigureAwait(false);
        return user;
    }

    public async Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default)
        => await _retryPolicy.ExecuteAsync(() =>
            dbContext.Users.AnyAsync(u => u.Email == email, cancellationToken))
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<ShortenedUrl>> GetUrlsByUserIdAsync(int userId, CancellationToken cancellationToken = default)
        => await _retryPolicy.ExecuteAsync(() =>
            dbContext.ShortenedUrls
                .AsNoTracking()
                .Where(u => u.UserId == userId)
                .OrderByDescending(u => u.CreatedAt)
                .ToListAsync(cancellationToken))
            .ConfigureAwait(false);
}
