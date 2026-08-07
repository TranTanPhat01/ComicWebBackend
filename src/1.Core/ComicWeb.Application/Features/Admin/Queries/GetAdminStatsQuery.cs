using ComicWeb.Application.Common.Interface;
using ComicWeb.Application.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;

namespace ComicWeb.Application.Features.Admin.Queries
{
    public record GetAdminStatsQuery : IRequest<AdminStatsDto>;

    public class GetAdminStatsQueryHandler : IRequestHandler<GetAdminStatsQuery, AdminStatsDto>
    {
        private readonly IApplicationDbContext _context;

        public GetAdminStatsQueryHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<AdminStatsDto> Handle(GetAdminStatsQuery request, CancellationToken cancellationToken)
        {
            var totalStories = await _context.Stories.CountAsync(cancellationToken);
            var totalChapters = await _context.Chapters.CountAsync(cancellationToken);
            var totalUsers = await _context.Users.CountAsync(cancellationToken);
            var totalLogs = await _context.SystemLogs.CountAsync(cancellationToken);
            var lockedChapters = await _context.Chapters.CountAsync(c => c.IsLocked, cancellationToken);
            var ongoingStories = await _context.Stories
                .CountAsync(s => s.Status == ComicWeb.Domain.Enums.StoryStatus.Published, cancellationToken);
            var totalClicks = await _context.AffiliateClicks.CountAsync(cancellationToken);

            return new AdminStatsDto(totalStories, totalChapters, totalUsers, totalLogs, lockedChapters, ongoingStories, totalClicks);
        }
    }
}
