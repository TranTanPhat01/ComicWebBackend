using ComicWeb.Application.Common.Exceptions;
using ComicWeb.Application.Features.Stories;
using ComicWeb.WebApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ComicWeb.WebApi.Controllers;

[ApiController]
[Route("api/v1/admin/stories")]
[Authorize(Policy = "AdminOnly")]
public sealed class AdminStoriesController : BaseApiController
{
    [HttpGet]
    public async Task<ActionResult<PagedApiEnvelope<AdminStoryDto>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] bool includeDeleted = false,
        [FromQuery] string sort = "createdAt",
        [FromQuery] bool desc = true)
    {
        var result = await Mediator.Send(new AdminStoryListQuery(page, pageSize, search, status, includeDeleted, sort, desc));
        return Ok(new PagedApiEnvelope<AdminStoryDto>(result.Items, result.Meta, RequestId()));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiEnvelope<AdminStoryDto>>> Get(int id, [FromQuery] bool includeDeleted = false)
    {
        var result = await Mediator.Send(new AdminStoryByIdQuery(id, includeDeleted));
        return Ok(new ApiEnvelope<AdminStoryDto>(result, RequestId()));
    }

    [HttpPost]
    public async Task<ActionResult<ApiEnvelope<AdminStoryDto>>> Create(StoryUpsertRequest body)
    {
        var result = await Mediator.Send(new CreateAdminStoryCommand(body.Title, body.Slug, body.Description, body.CoverImageUrl, body.AuthorName, body.Genres));
        return CreatedAtAction(nameof(Get), new { id = result.Id }, new ApiEnvelope<AdminStoryDto>(result, RequestId()));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiEnvelope<AdminStoryDto>>> Update(int id, StoryUpsertRequest body)
    {
        var result = await Mediator.Send(new UpdateAdminStoryCommand(id, body.Title, body.Slug, body.Description, body.CoverImageUrl, body.AuthorName, body.Genres, RequireVersion(body.Version)));
        return Ok(new ApiEnvelope<AdminStoryDto>(result, RequestId()));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, [FromQuery] int? version)
    {
        await Mediator.Send(new DeleteAdminStoryCommand(id, RequireVersion(version)));
        return NoContent();
    }

    [HttpPost("{id:int}/restore")]
    public async Task<ActionResult<ApiEnvelope<AdminStoryDto>>> Restore(int id, [FromQuery] int? version)
    {
        var result = await Mediator.Send(new RestoreAdminStoryCommand(id, RequireVersion(version)));
        return Ok(new ApiEnvelope<AdminStoryDto>(result, RequestId()));
    }

    [HttpPost("{id:int}/publish")]
    public async Task<ActionResult<ApiEnvelope<PublicationDto>>> Publish(int id, VersionRequest body)
        => Ok(new ApiEnvelope<PublicationDto>(await Mediator.Send(new PublishStoryCommand(id, RequireVersion(body.Version))), RequestId()));

    [HttpPost("{id:int}/schedule")]
    public async Task<ActionResult<ApiEnvelope<AdminStoryDto>>> Schedule(int id, ScheduleRequest body)
    {
        var result = await Mediator.Send(new ScheduleStoryCommand(id, body.ScheduledAt, RequireVersion(body.Version)));
        return Ok(new ApiEnvelope<AdminStoryDto>(result, RequestId()));
    }

    [HttpPost("{id:int}/unpublish")]
    public async Task<ActionResult<ApiEnvelope<PublicationDto>>> Unpublish(int id, VersionRequest body)
        => Ok(new ApiEnvelope<PublicationDto>(await Mediator.Send(new UnpublishStoryCommand(id, RequireVersion(body.Version))), RequestId()));

    [HttpPost("{id:int}/hide")]
    public async Task<ActionResult<ApiEnvelope<PublicationDto>>> Hide(int id, VersionRequest body)
        => Ok(new ApiEnvelope<PublicationDto>(await Mediator.Send(new HideStoryCommand(id, RequireVersion(body.Version))), RequestId()));

    [HttpPost("{id:int}/complete")]
    public async Task<ActionResult<ApiEnvelope<PublicationDto>>> Complete(int id, VersionRequest body)
        => Ok(new ApiEnvelope<PublicationDto>(await Mediator.Send(new CompleteStoryCommand(id, RequireVersion(body.Version))), RequestId()));

    private string RequestId() => HttpContext.TraceIdentifier;

    private static int RequireVersion(int? version) => version is >= 0
        ? version.Value
        : throw new AppException("VERSION_REQUIRED", 400, "Validation failed", "A non-negative version is required.");
}
