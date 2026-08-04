using ComicWeb.Application.Common.Interfaces;
namespace ComicWeb.Persistence.Auth;

public sealed class SystemDateTimeProvider : IDateTimeProvider { public DateTime UtcNow => DateTime.UtcNow; }
