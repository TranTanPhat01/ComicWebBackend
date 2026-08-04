using ComicWeb.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
namespace ComicWeb.WebApi.Auth;

public sealed class ActiveUserRequirement : IAuthorizationRequirement;
public sealed class ActiveUserHandler(IAuthRepository repository) : AuthorizationHandler<ActiveUserRequirement> { protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ActiveUserRequirement requirement) { if (!int.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) return; var user = await repository.FindByIdAsync(id, CancellationToken.None); if (user is { IsActive: true }) context.Succeed(requirement); } }
