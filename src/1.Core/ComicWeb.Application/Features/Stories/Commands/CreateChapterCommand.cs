using ComicWeb.Application.Common.Interface;
using ComicWeb.Application.Common.Interfaces;
using ComicWeb.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
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
        private readonly ISlugGenerator _slugGenerator;

        public CreateChapterCommandHandler(IApplicationDbContext context, ISlugGenerator slugGenerator)
        {
            _context = context;
            _slugGenerator = slugGenerator;
        }

        public async Task<int> Handle(CreateChapterCommand request, CancellationToken cancellationToken)
        {
            var slugBasis = _slugGenerator.Generate(request.Title);
            if (string.IsNullOrWhiteSpace(slugBasis))
            {
                slugBasis = $"chuong-{request.ChapterNumber}";
            }

            var uniqueSlug = slugBasis;
            var num = 1;
            while (await _context.Chapters.AnyAsync(x => x.StoryId == request.StoryId && x.Slug == uniqueSlug, cancellationToken))
            {
                num++;
                uniqueSlug = $"{slugBasis}-{num}";
            }

            var chapter = new Chapter
            {
                StoryId = request.StoryId,
                ChapterNumber = request.ChapterNumber,
                Title = request.Title,
                Slug = uniqueSlug,
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
