using ComicWeb.Application.Common.Interfaces;
using ComicWeb.Application.Features.Users;
using ComicWeb.Application.Features.Notifications;
using ComicWeb.WebApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ComicWeb.WebApi.Controllers;

[Authorize]
[Route("api/v1/me")]
public sealed class MeController(ICurrentUser currentUser) : BaseApiController
{
    public record UpsertHistoryRequest(int StoryId, int ChapterId);
    public record MergeRequest(List<int> Follows, List<HistoryMergeItem> Histories);

    // --- Follows API ---
    [HttpGet("follows")]
    public async Task<IActionResult> GetFollowedStories([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var userId = currentUser.UserId ?? throw new System.UnauthorizedAccessException();
        var result = await Mediator.Send(new GetFollowedStoriesQuery(userId, page, pageSize));
        
        var meta = new
        {
            currentPage = page,
            pageSize = pageSize,
            totalCount = result.TotalCount,
            totalPages = (int)System.Math.Ceiling(result.TotalCount / (double)pageSize)
        };

        return Ok(new PagedApiEnvelope<FollowedStoryDto>(result.Items, meta, HttpContext.TraceIdentifier));
    }

    [HttpPost("follows/{storyId:int}")]
    public async Task<IActionResult> FollowStory(int storyId)
    {
        var userId = currentUser.UserId ?? throw new System.UnauthorizedAccessException();
        await Mediator.Send(new FollowStoryCommand(userId, storyId));
        return Ok(new ApiEnvelope<string>("Story followed successfully.", HttpContext.TraceIdentifier));
    }

    [HttpDelete("follows/{storyId:int}")]
    public async Task<IActionResult> UnfollowStory(int storyId)
    {
        var userId = currentUser.UserId ?? throw new System.UnauthorizedAccessException();
        await Mediator.Send(new UnfollowStoryCommand(userId, storyId));
        return Ok(new ApiEnvelope<string>("Story unfollowed successfully.", HttpContext.TraceIdentifier));
    }

    // --- History API ---
    [HttpGet("history")]
    public async Task<IActionResult> GetReadingHistory([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var userId = currentUser.UserId ?? throw new System.UnauthorizedAccessException();
        var result = await Mediator.Send(new GetReadingHistoryQuery(userId, page, pageSize));

        var meta = new
        {
            currentPage = page,
            pageSize = pageSize,
            totalCount = result.TotalCount,
            totalPages = (int)System.Math.Ceiling(result.TotalCount / (double)pageSize)
        };

        return Ok(new PagedApiEnvelope<ReadingHistoryDto>(result.Items, meta, HttpContext.TraceIdentifier));
    }

    [HttpPut("history")]
    public async Task<IActionResult> UpsertHistory([FromBody] UpsertHistoryRequest body)
    {
        var userId = currentUser.UserId ?? throw new System.UnauthorizedAccessException();
        await Mediator.Send(new UpsertReadingHistoryCommand(userId, body.StoryId, body.ChapterId));
        return Ok(new ApiEnvelope<string>("Reading history updated successfully.", HttpContext.TraceIdentifier));
    }

    [HttpDelete("history/{storyId:int}")]
    public async Task<IActionResult> DeleteHistory(int storyId)
    {
        var userId = currentUser.UserId ?? throw new System.UnauthorizedAccessException();
        await Mediator.Send(new DeleteReadingHistoryCommand(userId, storyId));
        return Ok(new ApiEnvelope<string>("Reading history deleted successfully.", HttpContext.TraceIdentifier));
    }

    // --- Merge API ---
    [HttpPost("merge")]
    public async Task<IActionResult> Merge([FromBody] MergeRequest body)
    {
        var userId = currentUser.UserId ?? throw new System.UnauthorizedAccessException();
        await Mediator.Send(new MergeUserActivitiesCommand(userId, body.Follows, body.Histories));
        return Ok(new ApiEnvelope<string>("User activities merged successfully.", HttpContext.TraceIdentifier));
    }

    // --- Notifications API ---
    [HttpGet("notifications")]
    public async Task<IActionResult> GetNotifications([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var userId = currentUser.UserId ?? throw new System.UnauthorizedAccessException();
        var result = await Mediator.Send(new GetUserNotificationsQuery(userId, page, pageSize));

        var meta = new
        {
            currentPage = page,
            pageSize = pageSize,
            totalCount = result.TotalCount,
            totalPages = (int)System.Math.Ceiling(result.TotalCount / (double)pageSize)
        };

        return Ok(new PagedApiEnvelope<UserNotificationDto>(result.Items, meta, HttpContext.TraceIdentifier));
    }

    [HttpPut("notifications/{id:int}/read")]
    public async Task<IActionResult> MarkAsRead(int id)
    {
        var userId = currentUser.UserId ?? throw new System.UnauthorizedAccessException();
        await Mediator.Send(new MarkNotificationAsReadCommand(userId, id));
        return Ok(new ApiEnvelope<string>("Notification marked as read successfully.", HttpContext.TraceIdentifier));
    }

    [HttpPut("notifications/read-all")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var userId = currentUser.UserId ?? throw new System.UnauthorizedAccessException();
        await Mediator.Send(new MarkAllNotificationsAsReadCommand(userId));
        return Ok(new ApiEnvelope<string>("All notifications marked as read successfully.", HttpContext.TraceIdentifier));
    }
}
