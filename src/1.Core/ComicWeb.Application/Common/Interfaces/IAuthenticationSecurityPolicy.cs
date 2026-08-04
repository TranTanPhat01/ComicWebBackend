namespace ComicWeb.Application.Common.Interfaces;

public interface IAuthenticationSecurityPolicy
{
    int MaxFailedLoginAttempts { get; }
    TimeSpan LockoutDuration { get; }
}
