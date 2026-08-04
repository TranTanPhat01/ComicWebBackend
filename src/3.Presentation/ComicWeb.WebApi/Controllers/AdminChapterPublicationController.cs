using ComicWeb.Application.Common.Exceptions;
using ComicWeb.Application.Features.Stories;
using ComicWeb.WebApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ComicWeb.WebApi.Controllers;

[ApiController]
[Route("api/v1/admin/chapters")]
[Authorize(Policy = "AdminOnly")]
public sealed class AdminChapterPublicationController : BaseApiController
{
    [HttpPost("{id:int}/publish")]
    public async Task<ActionResult<ApiEnvelope<PublicationDto>>> Publish(int id, VersionRequest body)
        => Ok(new ApiEnvelope<PublicationDto>(await Mediator.Send(new PublishChapterCommand(id, Version(body.Version))), HttpContext.TraceIdentifier));

    [HttpPost("{id:int}/schedule")]
    public async Task<ActionResult<ApiEnvelope<AdminChapterDto>>> Schedule(int id, ScheduleRequest body)
    {
        var result = await Mediator.Send(new ScheduleChapterCommand(id, body.ScheduledAt, Version(body.Version)));
        return Ok(new ApiEnvelope<AdminChapterDto>(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:int}/unpublish")]
    public async Task<ActionResult<ApiEnvelope<PublicationDto>>> Unpublish(int id, VersionRequest body)
        => Ok(new ApiEnvelope<PublicationDto>(await Mediator.Send(new UnpublishChapterCommand(id, Version(body.Version))), HttpContext.TraceIdentifier));

    [HttpPost("{id:int}/hide")]
    public async Task<ActionResult<ApiEnvelope<PublicationDto>>> Hide(int id, VersionRequest body)
        => Ok(new ApiEnvelope<PublicationDto>(await Mediator.Send(new HideChapterCommand(id, Version(body.Version))), HttpContext.TraceIdentifier));

    private static int Version(int? value) => value is >= 0
        ? value.Value
        : throw new AppException("VALIDATION_ERROR", 400, "Validation failed", "A non-negative version is required.");
}
