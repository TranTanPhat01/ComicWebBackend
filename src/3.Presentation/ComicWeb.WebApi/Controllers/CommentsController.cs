using ComicWeb.Application.Common.Interfaces;
using ComicWeb.Application.Features.Stories;
using ComicWeb.Domain.Enums;
using ComicWeb.WebApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace ComicWeb.WebApi.Controllers;

/// <summary>
/// Comments endpoints for public reading and authenticated interactions.
/// Public:         GET  /api/v1/stories/{storyId}/comments
/// Authenticated:  POST /DELETE /PUT
/// Admin:          PATCH /api/v1/admin/comments/{commentId}/moderate
/// </summary>
[ApiController]
public sealed class CommentsController(ICurrentUser currentUser) : BaseApiController
{
    public record CreateCommentRequest(int? ChapterId, int? ParentCommentId, string Content);
    public record EditCommentRequest(string Content);
    public record ModerateRequest(string Status);

    // ─── Public: Get comments ───
    [HttpGet("api/v1/stories/{storyId:int}/comments")]
    [AllowAnonymous]
    public async Task<IActionResult> GetComments(
        int storyId,
        [FromQuery] int? chapterId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await Mediator.Send(new GetStoryCommentsQuery(storyId, chapterId, page, pageSize));
        var meta = new
        {
            currentPage = result.Meta.Page,
            pageSize = result.Meta.PageSize,
            totalCount = result.Meta.TotalItems,
            totalPages = result.Meta.TotalPages
        };
        return Ok(new PagedApiEnvelope<CommentDto>(result.Items, meta, HttpContext.TraceIdentifier));
    }

    // ─── Authenticated: Create comment ───
    [HttpPost("api/v1/stories/{storyId:int}/comments")]
    [Authorize]
    public async Task<IActionResult> CreateComment(int storyId, [FromBody] CreateCommentRequest body)
    {
        var userId = currentUser.UserId ?? throw new System.UnauthorizedAccessException();
        var result = await Mediator.Send(new CreateCommentCommand(userId, storyId, body.ChapterId, body.ParentCommentId, body.Content));
        return Ok(new ApiEnvelope<CommentDto>(result, HttpContext.TraceIdentifier));
    }

    // ─── Authenticated: Edit own comment ───
    [HttpPut("api/v1/stories/{storyId:int}/comments/{commentId:int}")]
    [Authorize]
    public async Task<IActionResult> EditComment(int storyId, int commentId, [FromBody] EditCommentRequest body)
    {
        var userId = currentUser.UserId ?? throw new System.UnauthorizedAccessException();
        var result = await Mediator.Send(new EditCommentCommand(userId, commentId, body.Content));
        return Ok(new ApiEnvelope<CommentDto>(result, HttpContext.TraceIdentifier));
    }

    // ─── Authenticated: Delete own comment ───
    [HttpDelete("api/v1/stories/{storyId:int}/comments/{commentId:int}")]
    [Authorize]
    public async Task<IActionResult> DeleteComment(int storyId, int commentId)
    {
        var userId = currentUser.UserId ?? throw new System.UnauthorizedAccessException();
        var isAdmin = User.IsInRole("Admin");
        await Mediator.Send(new DeleteCommentCommand(userId, commentId, isAdmin));
        return Ok(new ApiEnvelope<string>("Comment deleted.", HttpContext.TraceIdentifier));
    }

    // ─── Admin: Moderate comment ───
    [HttpPatch("api/v1/admin/comments/{commentId:int}/moderate")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Moderate(int commentId, [FromBody] ModerateRequest body)
    {
        var adminId = currentUser.UserId ?? throw new System.UnauthorizedAccessException();
        if (!System.Enum.TryParse<CommentStatus>(body.Status, true, out var status))
            return BadRequest(new ApiEnvelope<string>("Invalid status value.", HttpContext.TraceIdentifier));

        await Mediator.Send(new AdminModerateCommentCommand(adminId, commentId, status));
        return Ok(new ApiEnvelope<string>("Comment moderated.", HttpContext.TraceIdentifier));
    }
}
