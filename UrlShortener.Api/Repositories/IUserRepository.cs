using UrlShortener.Api.Models;

namespace UrlShortener.Api.Repositories;

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<User> CreateAsync(User user, CancellationToken cancellationToken = default);
    Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ShortenedUrl>> GetUrlsByUserIdAsync(int userId, CancellationToken cancellationToken = default);
}
