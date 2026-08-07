using ComicWeb.Application.Features.Settings;
using ComicWeb.WebApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ComicWeb.WebApi.Controllers;

[Authorize(Policy = "AdminOnly")]
[Route("api/v1/admin/settings")]
public sealed class AdminSettingsController : BaseApiController
{
    [HttpGet]
    public async Task<ActionResult<ApiEnvelope<IReadOnlyList<SettingItemDto>>>> GetSettings()
    {
        var result = await Mediator.Send(new GetAdminSettingsQuery());
        return Ok(new ApiEnvelope<IReadOnlyList<SettingItemDto>>(result, HttpContext.TraceIdentifier));
    }

    [HttpPut]
    public async Task<ActionResult<ApiEnvelope<string>>> SaveSettings([FromBody] List<SettingItemDto> body)
    {
        await Mediator.Send(new SaveSettingsCommand(body));
        return Ok(new ApiEnvelope<string>("Settings updated successfully.", HttpContext.TraceIdentifier));
    }
}
