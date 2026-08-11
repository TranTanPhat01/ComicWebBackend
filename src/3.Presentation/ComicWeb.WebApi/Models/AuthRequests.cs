namespace ComicWeb.WebApi.Models;

public sealed record LoginRequest(string UsernameOrEmail, string Password);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword, string ConfirmPassword);
public sealed record RegisterRequest(string Username, string Email, string Password);
public sealed record ApiEnvelope<T>(T Data, string RequestId);
