using ComicWeb.Application.Common.Interface;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ComicWeb.Application.Features.Stories.Commands
{
    public record DeleteStoryCommand(int Id) : IRequest<bool>;

    public class DeleteStoryCommandHandler : IRequestHandler<DeleteStoryCommand, bool>
    {
        private readonly IApplicationDbContext _context;

        public DeleteStoryCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<bool> Handle(DeleteStoryCommand request, CancellationToken cancellationToken)
        {
            var story = await _context.Stories
                .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);

            if (story == null) return false;

            story.SoftDelete(DateTime.UtcNow);
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
    }
}
