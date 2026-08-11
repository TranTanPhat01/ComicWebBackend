using ComicWeb.Application.Features.Stories.Commands;
using ComicWeb.Application.Features.Stories.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ComicWeb.WebApi.Controllers
{
    public class StoriesController : BaseApiController
    {
        // ── PUBLIC ────────────────────────────────────────────────────

        [HttpGet]
        public async Task<IActionResult> GetAll()
            => Ok(await Mediator.Send(new GetStoriesQuery()));

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var story = await Mediator.Send(new GetStoryByIdQuery(id));
            return story == null
                ? NotFound(new { message = $"Không tìm thấy truyện Id = {id}" })
                : Ok(story);
        }

        [HttpGet("{id:int}/chapters")]
        public async Task<IActionResult> GetChapters(int id)
            => Ok(await Mediator.Send(new GetChaptersByStoryQuery(id)));

        // ── ADMIN (cần JWT role=Admin) ─────────────────────────────

        [HttpPost]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> Create([FromBody] CreateStoryCommand command)
        {
            var id = await Mediator.Send(command);
            return Ok(new { id, message = "Tạo truyện thành công!" });
        }

        [HttpPut("{id:int}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateStoryCommand command)
        {
            if (id != command.Id)
                return BadRequest(new { message = "Id không khớp." });

            var ok = await Mediator.Send(command);
            return ok
                ? Ok(new { message = "Cập nhật truyện thành công!" })
                : NotFound(new { message = $"Không tìm thấy truyện Id = {id}" });
        }

        [HttpDelete("{id:int}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> Delete(int id)
        {
            var ok = await Mediator.Send(new DeleteStoryCommand(id));
            return ok
                ? Ok(new { message = "Xóa truyện thành công!" })
                : NotFound(new { message = $"Không tìm thấy truyện Id = {id}" });
        }
    }
}
