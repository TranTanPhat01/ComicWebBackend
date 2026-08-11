using ComicWeb.Application.Common.Interfaces;
using ComicWeb.Application.Features.Users;
using ComicWeb.Domain.Enums;
using ComicWeb.WebApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ComicWeb.WebApi.Controllers;

[Authorize(Policy = "AdminOnly")]
[Route("api/v1/admin/users")]
public sealed class AdminUsersController(ICurrentUser currentUser) : BaseApiController
{
    public record UpdateStatusRequest(bool IsActive);
    public record UpdateRoleRequest(UserRole Role);

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] UserRole? role = null,
        [FromQuery] bool? isActive = null)
    {
        var result = await Mediator.Send(new GetUsersQuery(page, pageSize, search, role, isActive));
        
        var meta = new
        {
            currentPage = page,
            pageSize = pageSize,
            totalCount = result.TotalCount,
            totalPages = (int)System.Math.Ceiling(result.TotalCount / (double)pageSize)
        };

        return Ok(new PagedApiEnvelope<UserDetailsDto>(result.Items, meta, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Detail(int id)
    {
        var result = await Mediator.Send(new GetUserDetailQuery(id));
        return Ok(new ApiEnvelope<UserDetailsDto>(result, HttpContext.TraceIdentifier));
    }

    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateStatusRequest body)
    {
        var adminId = currentUser.UserId ?? throw new System.UnauthorizedAccessException();
        await Mediator.Send(new UpdateUserStatusCommand(id, body.IsActive, adminId));
        return Ok(new ApiEnvelope<string>("User status updated successfully.", HttpContext.TraceIdentifier));
    }

    [HttpPatch("{id:int}/role")]
    public async Task<IActionResult> UpdateRole(int id, [FromBody] UpdateRoleRequest body)
    {
        var adminId = currentUser.UserId ?? throw new System.UnauthorizedAccessException();
        await Mediator.Send(new UpdateUserRoleCommand(id, body.Role, adminId));
        return Ok(new ApiEnvelope<string>("User role updated successfully.", HttpContext.TraceIdentifier));
    }
}
