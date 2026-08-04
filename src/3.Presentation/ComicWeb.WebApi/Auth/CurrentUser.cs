using System.Security.Claims;
using ComicWeb.Application.Common.Interfaces;
namespace ComicWeb.WebApi.Auth;

public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser { public int? UserId => int.TryParse(accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null; }
