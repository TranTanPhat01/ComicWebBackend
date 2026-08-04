using ComicWeb.Application.Common.Interface;
using ComicWeb.Domain.Entities;
using ComicWeb.Domain.Enums;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ComicWeb.Application.Features.Stories.Commands
{
    public record CreateStoryCommand(string Title, string Description, string CoverImageUrl) : IRequest<int>;

    public class CreateStoryCommandHandler : IRequestHandler<CreateStoryCommand, int>
    {
        private readonly IApplicationDbContext _context;

        public CreateStoryCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<int> Handle(CreateStoryCommand request, CancellationToken cancellationToken)
        {
            var story = new Story
            {
                Title = request.Title,
                Description = request.Description,
                CoverImageUrl = request.CoverImageUrl,

                // XÓA DÒNG CŨ: Status = "Đang tiến hành"
                // SỬA THÀNH DÒNG MỚI:
                Status = StoryStatus.Draft
            };

            _context.Stories.Add(story);
            await _context.SaveChangesAsync(cancellationToken);

            return story.Id;
        }
    }
}
