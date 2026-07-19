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
    public record GetStoriesQuery : IRequest<List<StoryDto>>;

    public class GetStoriesQueryHandler : IRequestHandler<GetStoriesQuery, List<StoryDto>>
    {
        private readonly IApplicationDbContext _context;
        private readonly IMapper _mapper; // Inject AutoMapper vào đây

        public GetStoriesQueryHandler(IApplicationDbContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public async Task<List<StoryDto>> Handle(GetStoriesQuery request, CancellationToken cancellationToken)
        {
            // Lấy danh sách entity từ DB (sử dụng AsNoTracking để tối ưu tốc độ đọc)
            var stories = await _context.Stories
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            // Dùng AutoMapper chuyển đổi hàng loạt Entity sang DTO gọn nhẹ chỉ với 1 dòng
            return _mapper.Map<List<StoryDto>>(stories);
        }
    }
}
