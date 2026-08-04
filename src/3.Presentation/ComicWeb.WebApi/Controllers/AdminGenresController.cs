using ComicWeb.Application.Features.Genres;
using ComicWeb.WebApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ComicWeb.WebApi.Controllers;

[ApiController]
[Route("api/v1/admin/genres")]
[Authorize(Policy = "AdminOnly")]
public sealed class AdminGenresController : BaseApiController
{
    [HttpGet]
    public async Task<ActionResult<ApiEnvelope<IReadOnlyList<GenreListItemDto>>>> List()
    {
        var result = await Mediator.Send(new GetAdminGenresQuery());
        return Ok(new ApiEnvelope<IReadOnlyList<GenreListItemDto>>(result, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    public async Task<ActionResult<ApiEnvelope<GenreListItemDto>>> Create(GenreUpsertRequest body)
    {
        var result = await Mediator.Send(new CreateGenreCommand(body.Name, body.Slug, body.Description, body.IsActive));
        return CreatedAtAction(nameof(List), new { }, new ApiEnvelope<GenreListItemDto>(result, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiEnvelope<GenreListItemDto>>> Update(int id, GenreUpsertRequest body)
    {
        var result = await Mediator.Send(new UpdateGenreCommand(id, body.Name, body.Slug, body.Description, body.IsActive));
        return Ok(new ApiEnvelope<GenreListItemDto>(result, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await Mediator.Send(new DeleteGenreCommand(id));
        return NoContent();
    }
}
