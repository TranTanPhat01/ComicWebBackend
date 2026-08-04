using ComicWeb.Domain.Entities;

namespace ComicWeb.Application.Common.Interfaces;

public interface IAuthRepository
{
    Task<User?> FindByUsernameOrEmailAsync(string normalizedValue, CancellationToken cancellationToken);
    Task<User?> FindByIdAsync(int id, CancellationToken cancellationToken);
    Task<bool> HasAdministratorAsync(CancellationToken cancellationToken);
    Task AddUserAsync(User user, CancellationToken cancellationToken);
    Task<RefreshSession?> FindSessionByTokenHashAsync(string tokenHash, CancellationToken cancellationToken);
    Task AddSessionAsync(RefreshSession session, CancellationToken cancellationToken);
    Task RevokeAllSessionsAsync(int userId, DateTime now, string? ip, string reason, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
