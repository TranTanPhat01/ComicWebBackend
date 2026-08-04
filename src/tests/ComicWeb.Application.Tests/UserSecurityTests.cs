using ComicWeb.Domain.Entities;
using ComicWeb.Domain.Enums;
namespace ComicWeb.Application.Tests;
public class UserSecurityTests
{
 [Fact] public void Locks_after_five_failed_attempts(){var now=DateTime.UtcNow;var user=new User("reader","reader@example.test","hash",UserRole.User,false,now);for(var i=0;i<5;i++)user.RecordFailedLogin(now,5,TimeSpan.FromMinutes(15));Assert.True(user.IsLockedOut(now));}
 [Fact] public void Successful_login_resets_lock_counter(){var now=DateTime.UtcNow;var user=new User("reader","reader@example.test","hash",UserRole.User,false,now);user.RecordFailedLogin(now,5,TimeSpan.FromMinutes(15));user.RecordSuccessfulLogin(now);Assert.Equal(0,user.FailedLoginAttempts);Assert.Null(user.LockoutEndAt);}
 [Fact] public void User_role_is_not_admin_by_default(){var user=new User("reader","reader@example.test","hash",UserRole.User,false,DateTime.UtcNow);Assert.Equal(UserRole.User,user.Role);}
}
