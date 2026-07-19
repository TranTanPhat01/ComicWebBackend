using ComicWeb.Application.Common.Interface;
using ComicWeb.Application.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ComicWeb.Application.Features.Admin.Queries
{
    public record GetSystemLogsQuery(int Page = 1, int PageSize = 20) : IRequest<List<SystemLogDto>>;

    public class GetSystemLogsQueryHandler : IRequestHandler<GetSystemLogsQuery, List<SystemLogDto>>
    {
        private readonly IApplicationDbContext _context;

        public GetSystemLogsQueryHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<SystemLogDto>> Handle(GetSystemLogsQuery request, CancellationToken cancellationToken)
        {
            return await _context.SystemLogs
                .AsNoTracking()
                .OrderByDescending(l => l.CreateAt)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(l => new SystemLogDto(l.Id, l.AdminAction, l.Details, l.IpAddress, l.CreateAt))
                .ToListAsync(cancellationToken);
        }
    }
}
