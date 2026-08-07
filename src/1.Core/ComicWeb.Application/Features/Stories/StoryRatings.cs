using ComicWeb.Application.Common.Exceptions;
using ComicWeb.Application.Common.Interface;
using ComicWeb.Application.Common.Interfaces;
using ComicWeb.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ComicWeb.Application.Features.Stories;

// ─── DTOs ───
public sealed record StoryRatingDto(int StoryId, int Score, DateTime UpdatedAt);
public sealed record RatingAggregateDto(double AverageRating, int RatingCount, int? MyRating);

// ─── Commands / Queries ───
public sealed record RateStoryCommand(int UserId, int StoryId, int Score) : IRequest<StoryRatingDto>;
public sealed record DeleteMyRatingCommand(int UserId, int StoryId) : IRequest;
public sealed record GetRatingAggregateQuery(int StoryId, int? UserId) : IRequest<RatingAggregateDto>;

// ─── Handlers ───
public sealed class RateStoryCommandHandler(IApplicationDbContext db, IDateTimeProvider clock)
    : IRequestHandler<RateStoryCommand, StoryRatingDto>
{
    public async Task<StoryRatingDto> Handle(RateStoryCommand request, CancellationToken ct)
    {
        if (request.Score < 1 || request.Score > 5)
            throw new AppException("INVALID_SCORE", 400, "Invalid Score", "Score must be between 1 and 5.");

        var storyExists = await db.Stories.AnyAsync(s => s.Id == request.StoryId && s.DeletedAt == null, ct);
        if (!storyExists)
            throw new AppException("STORY_NOT_FOUND", 404, "Not Found", "Story was not found.");

        var existing = await db.StoryRatings
            .FirstOrDefaultAsync(r => r.UserId == request.UserId && r.StoryId == request.StoryId, ct);

        var now = clock.UtcNow;
        if (existing is null)
        {
            var rating = new StoryRating
            {
                UserId = request.UserId,
                StoryId = request.StoryId,
                Score = request.Score,
                UpdateAt = now,
                CreateAt = now
            };
            db.StoryRatings.Add(rating);
        }
        else
        {
            existing.Score = request.Score;
            existing.UpdateAt = now;
        }

        await db.SaveChangesAsync(ct);
        return new StoryRatingDto(request.StoryId, request.Score, now);
    }
}

public sealed class DeleteMyRatingCommandHandler(IApplicationDbContext db)
    : IRequestHandler<DeleteMyRatingCommand>
{
    public async Task Handle(DeleteMyRatingCommand request, CancellationToken ct)
    {
        var rating = await db.StoryRatings
            .FirstOrDefaultAsync(r => r.UserId == request.UserId && r.StoryId == request.StoryId, ct);

        if (rating is not null)
        {
            db.StoryRatings.Remove(rating);
            await db.SaveChangesAsync(ct);
        }
    }
}

public sealed class GetRatingAggregateQueryHandler(IReadOnlyApplicationDbContext db)
    : IRequestHandler<GetRatingAggregateQuery, RatingAggregateDto>
{
    public async Task<RatingAggregateDto> Handle(GetRatingAggregateQuery request, CancellationToken ct)
    {
        var ratings = await db.StoryRatings
            .Where(r => r.StoryId == request.StoryId)
            .Select(r => new { r.UserId, r.Score })
            .ToListAsync(ct);

        var count = ratings.Count;
        var avg = count > 0 ? ratings.Average(r => r.Score) : 0.0;
        var myRating = request.UserId.HasValue
            ? ratings.FirstOrDefault(r => r.UserId == request.UserId.Value)?.Score
            : null;

        return new RatingAggregateDto(Math.Round(avg, 1), count, myRating);
    }
}
