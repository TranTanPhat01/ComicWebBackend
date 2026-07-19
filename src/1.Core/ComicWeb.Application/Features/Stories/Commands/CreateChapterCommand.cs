using ComicWeb.Application.Common.Interface;
using ComicWeb.Domain.Entities;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ComicWeb.Application.Features.Stories.Commands
{
    public record CreateChapterCommand(
    int StoryId,
    int ChapterNumber,
    string Title,
    string Content,
    string? AffiliateLink,
    bool IsLocked
    ) : IRequest<int>;

    public class CreateChapterCommandHandler : IRequestHandler<CreateChapterCommand, int>
    {
        private readonly IApplicationDbContext _context;

        public CreateChapterCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<int> Handle(CreateChapterCommand request, CancellationToken cancellationToken)
        {
            var chapter = new Chapter
            {
                StoryId = request.StoryId,
                ChapterNumber = request.ChapterNumber,
                Title = request.Title,
                Content = request.Content,
                AffiliateLink = request.AffiliateLink,
                IsLocked = request.IsLocked,
                CreatedAt = DateTime.UtcNow
            };

            _context.Chapters.Add(chapter);
            await _context.SaveChangesAsync(cancellationToken);

            return chapter.Id;
        }
    }
 }
