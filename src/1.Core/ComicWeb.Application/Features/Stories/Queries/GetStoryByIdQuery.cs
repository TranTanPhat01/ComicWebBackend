using AutoMapper;
using ComicWeb.Application.Common.Interface;
using ComicWeb.Application.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ComicWeb.Application.Features.Stories.Queries
{
    public record GetStoryByIdQuery(int Id) : IRequest<StoryDetailDto?>;

    public class GetStoryByIdQueryHandler : IRequestHandler<GetStoryByIdQuery, StoryDetailDto?>
    {
        private readonly IApplicationDbContext _context;
        private readonly IMapper _mapper;

        public GetStoryByIdQueryHandler(IApplicationDbContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }
        public async Task<StoryDetailDto?> Handle(GetStoryByIdQuery request, CancellationToken cancellationToken)
        {
            // Dùng Include để load eager-loading danh sách Chapters liên quan từ DB
            var story = await _context.Stories
                .Include(s => s.Chapters)
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);

            if (story == null) return null;

            // AutoMapper sẽ tự động map cả thực thể Story và danh sách Chapters con sang DTO tương ứng
            return _mapper.Map<StoryDetailDto>(story);
        }

    }
}
