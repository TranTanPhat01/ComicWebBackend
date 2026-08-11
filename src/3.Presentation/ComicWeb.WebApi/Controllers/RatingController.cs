using ComicWeb.Application.Common.Interfaces;
using ComicWeb.Application.Features.Stories;
using ComicWeb.WebApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace ComicWeb.WebApi.Controllers;

/// <summary>
/// Rating endpoints for authenticated readers.
/// Route: /api/v1/me/ratings/{storyId}
/// </summary>
[Authorize]
[Route("api/v1/me/ratings")]
public sealed class RatingController(ICurrentUser currentUser) : BaseApiController
{
    public record RateRequest(int Score);

    [HttpPost("{storyId:int}")]
    public async Task<IActionResult> Rate(int storyId, [FromBody] RateRequest body)
    {
        var userId = currentUser.UserId ?? throw new System.UnauthorizedAccessException();
        var result = await Mediator.Send(new RateStoryCommand(userId, storyId, body.Score));
        return Ok(new ApiEnvelope<StoryRatingDto>(result, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{storyId:int}")]
    public async Task<IActionResult> DeleteRating(int storyId)
    {
        var userId = currentUser.UserId ?? throw new System.UnauthorizedAccessException();
        await Mediator.Send(new DeleteMyRatingCommand(userId, storyId));
        return Ok(new ApiEnvelope<string>("Rating removed.", HttpContext.TraceIdentifier));
    }

    [HttpGet("{storyId:int}")]
    public async Task<IActionResult> GetMyRating(int storyId)
    {
        var userId = currentUser.UserId ?? throw new System.UnauthorizedAccessException();
        var result = await Mediator.Send(new GetRatingAggregateQuery(storyId, userId));
        return Ok(new ApiEnvelope<RatingAggregateDto>(result, HttpContext.TraceIdentifier));
    }
}
