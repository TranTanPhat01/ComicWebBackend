using System.Text.RegularExpressions;
using ComicWeb.Application.Common.Exceptions;
namespace ComicWeb.Application.Features.Auth;

public static class PasswordPolicy
{
    private static readonly string[] Common = ["Password123!", "Admin123!", "123456"];
    public static void EnsureValid(string password, string username, string email)
    {
        var valid = password.Length >= 12 && password.Any(char.IsUpper) && password.Any(char.IsLower) && password.Any(char.IsDigit)
            && password.Any(c => !char.IsLetterOrDigit(c)) && !Common.Contains(password, StringComparer.OrdinalIgnoreCase)
            && !password.Equals(username, StringComparison.OrdinalIgnoreCase) && !password.Equals(email, StringComparison.OrdinalIgnoreCase);
        if (!valid) throw new AppException("PASSWORD_POLICY_FAILED", 400, "Password policy failed", "Mật khẩu không đáp ứng chính sách bảo mật.");
    }
}
