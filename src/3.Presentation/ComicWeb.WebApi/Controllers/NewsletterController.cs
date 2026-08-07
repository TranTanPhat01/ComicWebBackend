using ComicWeb.Application.Features.Newsletter;
using ComicWeb.WebApi.Models;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace ComicWeb.WebApi.Controllers;

[ApiController]
[Route("api/v1/newsletter")]
public sealed class NewsletterController : BaseApiController
{
    public record SubscribeRequest(string Email);
    public record UnsubscribeRequest(string Email);

    [HttpPost("subscribe")]
    public async Task<IActionResult> Subscribe([FromBody] SubscribeRequest body)
    {
        await Mediator.Send(new SubscribeNewsletterCommand(body.Email));
        return Ok(new ApiEnvelope<string>("Subscribed to newsletter successfully.", HttpContext.TraceIdentifier));
    }

    [HttpPost("unsubscribe")]
    public async Task<IActionResult> Unsubscribe([FromBody] UnsubscribeRequest body)
    {
        await Mediator.Send(new UnsubscribeNewsletterCommand(body.Email));
        return Ok(new ApiEnvelope<string>("Unsubscribed from newsletter successfully.", HttpContext.TraceIdentifier));
    }
}
