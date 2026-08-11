using ComicWeb.Application.Common.Interface;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ComicWeb.Application.Features.Stories.Commands
{
    public record UpdateChapterCommand(
        int Id,
        int ChapterNumber,
        string Title,
        string Content,
        string? AffiliateLink,
        bool IsLocked
    ) : IRequest<bool>;

    public class UpdateChapterCommandHandler : IRequestHandler<UpdateChapterCommand, bool>
    {
        private readonly IApplicationDbContext _context;

        public UpdateChapterCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<bool> Handle(UpdateChapterCommand request, CancellationToken cancellationToken)
        {
            var chapter = await _context.Chapters
                .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);

            if (chapter == null) return false;

            chapter.ChapterNumber = request.ChapterNumber;
            chapter.Title = request.Title;
            chapter.Content = request.Content;
            chapter.AffiliateLink = request.AffiliateLink;
            chapter.IsLocked = request.IsLocked;
            chapter.CreatedAt = chapter.CreatedAt; // giữ nguyên

            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
    }
}
