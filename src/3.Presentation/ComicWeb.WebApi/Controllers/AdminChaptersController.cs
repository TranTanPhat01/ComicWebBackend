using ComicWeb.Application.Common.Exceptions;
using ComicWeb.Application.Features.Stories;
using ComicWeb.WebApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ComicWeb.WebApi.Controllers;

[ApiController]
[Route("api/v1/admin/stories/{storyId:int}/chapters")]
[Authorize(Policy = "AdminOnly")]
public sealed class AdminChaptersController : BaseApiController
{
    [HttpGet]
    public async Task<ActionResult<PagedApiEnvelope<AdminChapterDto>>> List(
        int storyId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] bool includeDeleted = false,
        [FromQuery] string sort = "chapterNumber",
        [FromQuery] bool desc = false)
    {
        var result = await Mediator.Send(new AdminChapterListQuery(storyId, page, pageSize, search, status, includeDeleted, sort, desc));
        return Ok(new PagedApiEnvelope<AdminChapterDto>(result.Items, result.Meta, RequestId()));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiEnvelope<AdminChapterDto>>> Get(int storyId, int id, [FromQuery] bool includeDeleted = false)
    {
        var result = await Mediator.Send(new AdminChapterByIdQuery(storyId, id, includeDeleted));
        return Ok(new ApiEnvelope<AdminChapterDto>(result, RequestId()));
    }

    [HttpPost]
    public async Task<ActionResult<ApiEnvelope<AdminChapterDto>>> Create(int storyId, ChapterUpsertRequest body)
    {
        var result = await Mediator.Send(new CreateAdminChapterCommand(storyId, body.ChapterNumber, body.Title, body.Slug, body.Content, body.IsLocked, body.AffiliateLink));
        return CreatedAtAction(nameof(Get), new { storyId, id = result.Id }, new ApiEnvelope<AdminChapterDto>(result, RequestId()));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiEnvelope<AdminChapterDto>>> Update(int storyId, int id, ChapterUpsertRequest body)
    {
        var result = await Mediator.Send(new UpdateAdminChapterCommand(storyId, id, body.ChapterNumber, body.Title, body.Slug, body.Content, RequireVersion(body.Version), body.IsLocked, body.AffiliateLink));
        return Ok(new ApiEnvelope<AdminChapterDto>(result, RequestId()));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int storyId, int id, [FromQuery] int? version)
    {
        await Mediator.Send(new DeleteAdminChapterCommand(storyId, id, RequireVersion(version)));
        return NoContent();
    }

    [HttpPost("{id:int}/restore")]
    public async Task<ActionResult<ApiEnvelope<AdminChapterDto>>> Restore(int storyId, int id, [FromQuery] int? version)
    {
        var result = await Mediator.Send(new RestoreAdminChapterCommand(storyId, id, RequireVersion(version)));
        return Ok(new ApiEnvelope<AdminChapterDto>(result, RequestId()));
    }

    private string RequestId() => HttpContext.TraceIdentifier;

    private static int RequireVersion(int? version) => version is >= 0
        ? version.Value
        : throw new AppException("VERSION_REQUIRED", 400, "Validation failed", "A non-negative version is required.");
}
