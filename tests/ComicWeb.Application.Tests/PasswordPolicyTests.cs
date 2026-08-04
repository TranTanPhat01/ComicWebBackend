using ComicWeb.Application.Common.Exceptions;
using ComicWeb.Application.Features.Auth;
namespace ComicWeb.Application.Tests;

public class PasswordPolicyTests
{
    [Fact] public void Accepts_strong_password() => PasswordPolicy.EnsureValid("StrongPass2026!", "admin", "admin@example.test");
    [Theory][InlineData("Password123!")][InlineData("Admin123!")][InlineData("short1!A")] public void Rejects_weak_password(string password) => Assert.Throws<AppException>(() => PasswordPolicy.EnsureValid(password, "admin", "admin@example.test"));
}
