using System;

namespace ComicWeb.Application.Dtos
{
    public record AdminStatsDto(
        int TotalStories,
        int TotalChapters,
        int TotalUsers,
        int TotalLogs,
        int LockedChapters,
        int OngoingStories,
        int TotalAffiliateClicks
    );
}
