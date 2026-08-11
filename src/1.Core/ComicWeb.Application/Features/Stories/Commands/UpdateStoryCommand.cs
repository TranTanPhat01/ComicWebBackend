using ComicWeb.Application.Common.Interface;
using ComicWeb.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ComicWeb.Application.Features.Stories.Commands
{
    public record UpdateStoryCommand(
        int Id,
        string Title,
        string Description,
        string CoverImageUrl,
        StoryStatus Status
    ) : IRequest<bool>;

    public class UpdateStoryCommandHandler : IRequestHandler<UpdateStoryCommand, bool>
    {
        private readonly IApplicationDbContext _context;

        public UpdateStoryCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<bool> Handle(UpdateStoryCommand request, CancellationToken cancellationToken)
        {
            var story = await _context.Stories
                .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);

            if (story == null) return false;

            story.Title = request.Title;
            story.Description = request.Description;
            story.CoverImageUrl = request.CoverImageUrl;
            story.Status = request.Status;
            story.UpdateAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
    }
}
