using ComicWeb.Application.Common.Interfaces;
using ComicWeb.Domain.Entities;
using ComicWeb.Domain.Enums;
using ComicWeb.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
namespace ComicWeb.Persistence.Auth;

public sealed class AuthRepository(ApplicationDbContext db) : IAuthRepository
{
    public Task<User?> FindByUsernameOrEmailAsync(string v, CancellationToken ct) => db.Users.FirstOrDefaultAsync(x => x.NormalizedUsername == v || x.NormalizedEmail == v, ct);
    public Task<User?> FindByIdAsync(int id, CancellationToken ct) => db.Users.FirstOrDefaultAsync(x => x.Id == id, ct);
    public Task<bool> HasAdministratorAsync(CancellationToken ct) => db.Users.AnyAsync(x => x.Role == UserRole.Admin, ct);
    public Task AddUserAsync(User u, CancellationToken ct) => db.Users.AddAsync(u, ct).AsTask();
    public Task<RefreshSession?> FindSessionByTokenHashAsync(string h, CancellationToken ct) => db.RefreshSessions.FirstOrDefaultAsync(x => x.TokenHash == h, ct);
    public Task AddSessionAsync(RefreshSession s, CancellationToken ct) => db.RefreshSessions.AddAsync(s, ct).AsTask();
    public async Task RevokeAllSessionsAsync(int id, DateTime now, string? ip, string reason, CancellationToken ct) { var sessions = await db.RefreshSessions.Where(x => x.UserId == id && x.RevokedAt == null).ToListAsync(ct); foreach (var s in sessions) s.Revoke(now, ip, reason); }
    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
