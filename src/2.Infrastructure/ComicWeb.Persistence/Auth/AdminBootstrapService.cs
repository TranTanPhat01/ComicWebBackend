using ComicWeb.Application.Common.Interfaces;
using ComicWeb.Application.Features.Auth;
using ComicWeb.Domain.Entities;
using ComicWeb.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
namespace ComicWeb.Persistence.Auth;

public sealed class AdminBootstrapService(IServiceScopeFactory scopes, IOptions<AdminBootstrapOptions> options, IHostEnvironment environment, ILogger<AdminBootstrapService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        var o = options.Value;
        if (!o.Enabled && !environment.IsProduction())
        {
            return;
        }

        using var scope = scopes.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IAuthRepository>();
        var context = scope.ServiceProvider.GetRequiredService<ComicWeb.Persistence.Contexts.ApplicationDbContext>();

        var adminUser = await context.Users.FirstOrDefaultAsync(u => u.Role == UserRole.Admin, ct);
        if (adminUser != null)
        {
            if (adminUser.MustChangePassword)
            {
                // Force delete the old unactivated admin to recreate with the latest configured password
                context.Users.Remove(adminUser);
                await context.SaveChangesAsync(ct);
                logger.LogInformation("Deleting existing unactivated admin user to synchronize password.");
            }
            else
            {
                logger.LogInformation("Administrator already exists and is activated. Skipping bootstrap.");
                return;
            }
        }

        if (!o.Enabled)
        {
            throw new InvalidOperationException("No administrator exists and bootstrap is disabled.");
        }

        if (string.IsNullOrWhiteSpace(o.Username) || string.IsNullOrWhiteSpace(o.Email) || string.IsNullOrWhiteSpace(o.Password))
        {
            throw new InvalidOperationException("No administrator exists and BootstrapAdmin configuration is incomplete.");
        }

        PasswordPolicy.EnsureValid(o.Password, o.Username, o.Email);
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var clock = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();

        try
        {
            await repo.AddUserAsync(new User(o.Username, o.Email, hasher.Hash(o.Password), UserRole.Admin, true, clock.UtcNow), ct);
            await repo.SaveChangesAsync(ct);
            logger.LogInformation("Bootstrap administrator created successfully.");
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException)
        {
            if (!await repo.HasAdministratorAsync(ct))
            {
                throw;
            }
        }
    }
    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
