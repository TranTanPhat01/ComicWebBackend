using ComicWeb.Application.Common.Exceptions;
using ComicWeb.Application.Common.Interface;
using ComicWeb.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ComicWeb.Application.Features.Stories;

public sealed record TrackAffiliateClickCommand(
    int ChapterId,
    string? IpAddress,
    string? UserAgent,
    string? Referrer) : IRequest;

public sealed class TrackAffiliateClickHandler : IRequestHandler<TrackAffiliateClickCommand>
{
    private readonly IApplicationDbContext _db;

    public TrackAffiliateClickHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task Handle(TrackAffiliateClickCommand request, CancellationToken ct)
    {
        // Lấy thông tin chapter để biết StoryId
        var chapter = await _db.Chapters.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.ChapterId, ct);
            
        if (chapter == null)
        {
            throw new AppException("CHAPTER_NOT_FOUND", 404, "Not found", "Chapter was not found.");
        }

        var click = new AffiliateClick
        {
            ChapterId = request.ChapterId,
            StoryId = chapter.StoryId,
            IpAddress = request.IpAddress,
            UserAgent = request.UserAgent,
            Referrer = request.Referrer,
            ClickedAt = DateTime.UtcNow
        };

        _db.AffiliateClicks.Add(click);
        await _db.SaveChangesAsync(ct);
    }
}
