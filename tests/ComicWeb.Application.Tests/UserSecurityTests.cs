using ComicWeb.Domain.Entities;
using ComicWeb.Domain.Enums;
namespace ComicWeb.Application.Tests;

public class UserSecurityTests
{
    [Fact] public void Locks_after_configured_failed_attempts() { var now = DateTime.UtcNow; var user = new User("reader", "reader@example.test", "hash", UserRole.User, false, now); for (var i = 0; i < 5; i++) user.RecordFailedLogin(now, 5, TimeSpan.FromMinutes(15)); Assert.True(user.IsLockedOut(now)); }
    [Fact] public void Password_change_clears_must_change_password() { var user = new User("admin", "admin@example.test", "old", UserRole.Admin, true, DateTime.UtcNow); user.ChangePassword("new", DateTime.UtcNow); Assert.False(user.MustChangePassword); }
}
