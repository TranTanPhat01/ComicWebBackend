using ComicWeb.Application.Common.Interface;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ComicWeb.Application.Features.Stories.Commands
{
    public record DeleteChapterCommand(int Id) : IRequest<bool>;

    public class DeleteChapterCommandHandler : IRequestHandler<DeleteChapterCommand, bool>
    {
        private readonly IApplicationDbContext _context;

        public DeleteChapterCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<bool> Handle(DeleteChapterCommand request, CancellationToken cancellationToken)
        {
            var chapter = await _context.Chapters
                .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);

            if (chapter == null) return false;

            _context.Chapters.Remove(chapter);
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
    }
}
