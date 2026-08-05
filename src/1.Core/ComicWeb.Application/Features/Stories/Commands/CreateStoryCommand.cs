using ComicWeb.Application.Common.Interface;
using ComicWeb.Application.Common.Interfaces;
using ComicWeb.Domain.Entities;
using ComicWeb.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ComicWeb.Application.Features.Stories.Commands
{
    public record CreateStoryCommand(
        string Title,
        string Description,
        string CoverImageUrl,
        string AuthorName = ""
    ) : IRequest<int>;

    public class CreateStoryCommandHandler : IRequestHandler<CreateStoryCommand, int>
    {
        private readonly IApplicationDbContext _context;
        private readonly ISlugGenerator _slugGenerator;

        public CreateStoryCommandHandler(IApplicationDbContext context, ISlugGenerator slugGenerator)
        {
            _context = context;
            _slugGenerator = slugGenerator;
        }

        public async Task<int> Handle(CreateStoryCommand request, CancellationToken cancellationToken)
        {
            var slugBasis = _slugGenerator.Generate(request.Title);
            if (string.IsNullOrWhiteSpace(slugBasis))
            {
                slugBasis = "truyen-moi";
            }

            var uniqueSlug = slugBasis;
            var num = 1;
            while (await _context.Stories.AnyAsync(x => x.Slug == uniqueSlug, cancellationToken))
            {
                num++;
                uniqueSlug = $"{slugBasis}-{num}";
            }

            var story = new Story
            {
                Title = request.Title,
                Slug = uniqueSlug,
                Description = request.Description,
                CoverImageUrl = request.CoverImageUrl,
                AuthorName = request.AuthorName,
                Status = StoryStatus.Draft
            };

            _context.Stories.Add(story);
            await _context.SaveChangesAsync(cancellationToken);

            return story.Id;
        }
    }
}
