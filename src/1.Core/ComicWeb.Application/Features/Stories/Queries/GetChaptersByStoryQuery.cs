using ComicWeb.Application.Common.Interface;
using ComicWeb.Application.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ComicWeb.Application.Features.Stories.Queries
{
    public record GetChaptersByStoryQuery(int StoryId) : IRequest<List<ChapterListDto>>;

    public class GetChaptersByStoryQueryHandler : IRequestHandler<GetChaptersByStoryQuery, List<ChapterListDto>>
    {
        private readonly IApplicationDbContext _context;

        public GetChaptersByStoryQueryHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<ChapterListDto>> Handle(GetChaptersByStoryQuery request, CancellationToken cancellationToken)
        {
            return await _context.Chapters
                .AsNoTracking()
                .Where(c => c.StoryId == request.StoryId)
                .OrderBy(c => c.ChapterNumber)
                .Select(c => new ChapterListDto(c.Id, c.ChapterNumber, c.Title ?? "", c.CreatedAt, c.IsLocked))
                .ToListAsync(cancellationToken);
        }
    }
}
