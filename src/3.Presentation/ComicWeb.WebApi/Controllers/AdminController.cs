using ComicWeb.Application.Features.Admin.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ComicWeb.WebApi.Controllers
{
    [Authorize(Policy = "AdminOnly")]
    public class AdminController : BaseApiController
    {
        // GET api/admin/stats — Dashboard metrics
        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
            => Ok(await Mediator.Send(new GetAdminStatsQuery()));
    }
}
