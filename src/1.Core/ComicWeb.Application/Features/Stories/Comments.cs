using ComicWeb.Application.Common.Exceptions;
using ComicWeb.Application.Common.Interface;
using ComicWeb.Application.Common.Interfaces;
using ComicWeb.Domain.Entities;
using ComicWeb.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ComicWeb.Application.Features.Stories;

// ─── DTOs ───
public sealed record CommentAuthorDto(int Id, string Username);
public sealed record CommentDto(
    int Id,
    int StoryId,
    int? ChapterId,
    CommentAuthorDto Author,
    int? ParentCommentId,
    string Content,
    string Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    bool IsDeleted,
    List<CommentDto> Replies);

// ─── Commands / Queries ───
public sealed record CreateCommentCommand(int UserId, int StoryId, int? ChapterId, int? ParentCommentId, string Content)
    : IRequest<CommentDto>;
public sealed record EditCommentCommand(int UserId, int CommentId, string NewContent)
    : IRequest<CommentDto>;
public sealed record DeleteCommentCommand(int UserId, int CommentId, bool IsAdmin)
    : IRequest;
public sealed record AdminModerateCommentCommand(int AdminUserId, int CommentId, CommentStatus Status)
    : IRequest;
public sealed record GetStoryCommentsQuery(int StoryId, int? ChapterId, int Page = 1, int PageSize = 20)
    : IRequest<PagedResult<CommentDto>>;

// ─── Handlers ───
public sealed class CreateCommentCommandHandler(IApplicationDbContext db, IDateTimeProvider clock)
    : IRequestHandler<CreateCommentCommand, CommentDto>
{
    public async Task<CommentDto> Handle(CreateCommentCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Content) || request.Content.Trim().Length > 2000)
            throw new AppException("INVALID_CONTENT", 400, "Invalid Content", "Comment content must be between 1 and 2000 characters.");

        var storyExists = await db.Stories.AnyAsync(s => s.Id == request.StoryId && s.DeletedAt == null, ct);
        if (!storyExists) throw new AppException("STORY_NOT_FOUND", 404, "Not Found", "Story was not found.");

        // Validate depth: parent must not itself have a parent (max 2 levels)
        if (request.ParentCommentId.HasValue)
        {
            var parent = await db.Comments
                .FirstOrDefaultAsync(c => c.Id == request.ParentCommentId.Value && c.DeletedAt == null, ct);
            if (parent is null) throw new AppException("COMMENT_NOT_FOUND", 404, "Not Found", "Parent comment was not found.");
            if (parent.ParentCommentId.HasValue) throw new AppException("NESTED_LIMIT_EXCEEDED", 400, "Bad Request", "Cannot reply to a reply (max 2 levels).");
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == request.UserId, ct)
            ?? throw new AppException("USER_NOT_FOUND", 404, "Not Found", "User was not found.");

        var now = clock.UtcNow;
        var comment = new Comment
        {
            StoryId = request.StoryId,
            ChapterId = request.ChapterId,
            UserId = request.UserId,
            ParentCommentId = request.ParentCommentId,
            Content = request.Content.Trim(),
            Status = CommentStatus.Active,
            CreateAt = now,
            UpdateAt = now
        };

        db.Comments.Add(comment);
        await db.SaveChangesAsync(ct);

        return new CommentDto(
            comment.Id, comment.StoryId, comment.ChapterId,
            new CommentAuthorDto(user.Id, user.Username),
            comment.ParentCommentId, comment.Content,
            comment.Status.ToString(), now, null, false, []);
    }
}

public sealed class EditCommentCommandHandler(IApplicationDbContext db, IDateTimeProvider clock)
    : IRequestHandler<EditCommentCommand, CommentDto>
{
    public async Task<CommentDto> Handle(EditCommentCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.NewContent) || request.NewContent.Trim().Length > 2000)
            throw new AppException("INVALID_CONTENT", 400, "Invalid Content", "Comment content must be between 1 and 2000 characters.");

        var comment = await db.Comments
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.Id == request.CommentId && c.DeletedAt == null, ct)
            ?? throw new AppException("COMMENT_NOT_FOUND", 404, "Not Found", "Comment was not found.");

        if (comment.UserId != request.UserId)
            throw new AppException("UNAUTHORIZED_ACTION", 403, "Forbidden", "You can only edit your own comments.");

        if (comment.Status == CommentStatus.HiddenByAdmin)
            throw new AppException("COMMENT_MODERATED", 400, "Bad Request", "Comment has been moderated and cannot be edited.");

        var now = clock.UtcNow;
        comment.Content = request.NewContent.Trim();
        comment.UpdateAt = now;

        await db.SaveChangesAsync(ct);

        return new CommentDto(
            comment.Id, comment.StoryId, comment.ChapterId,
            new CommentAuthorDto(comment.User.Id, comment.User.Username),
            comment.ParentCommentId, comment.Content,
            comment.Status.ToString(), comment.CreateAt, now, false, []);
    }
}

public sealed class DeleteCommentCommandHandler(IApplicationDbContext db, IDateTimeProvider clock)
    : IRequestHandler<DeleteCommentCommand>
{
    public async Task Handle(DeleteCommentCommand request, CancellationToken ct)
    {
        var comment = await db.Comments
            .FirstOrDefaultAsync(c => c.Id == request.CommentId && c.DeletedAt == null, ct)
            ?? throw new AppException("COMMENT_NOT_FOUND", 404, "Not Found", "Comment was not found.");

        if (!request.IsAdmin && comment.UserId != request.UserId)
            throw new AppException("UNAUTHORIZED_ACTION", 403, "Forbidden", "You can only delete your own comments.");

        comment.DeletedAt = clock.UtcNow;
        comment.UpdateAt = clock.UtcNow;

        await db.SaveChangesAsync(ct);
    }
}

public sealed class AdminModerateCommentCommandHandler(IApplicationDbContext db, IDateTimeProvider clock)
    : IRequestHandler<AdminModerateCommentCommand>
{
    public async Task Handle(AdminModerateCommentCommand request, CancellationToken ct)
    {
        var comment = await db.Comments
            .FirstOrDefaultAsync(c => c.Id == request.CommentId, ct)
            ?? throw new AppException("COMMENT_NOT_FOUND", 404, "Not Found", "Comment was not found.");

        comment.Status = request.Status;
        comment.UpdateAt = clock.UtcNow;

        await db.SaveChangesAsync(ct);
    }
}

public sealed class GetStoryCommentsQueryHandler(IReadOnlyApplicationDbContext db)
    : IRequestHandler<GetStoryCommentsQuery, PagedResult<CommentDto>>
{
    public async Task<PagedResult<CommentDto>> Handle(GetStoryCommentsQuery request, CancellationToken ct)
    {
        var query = db.Comments
            .Where(c => c.StoryId == request.StoryId
                     && c.DeletedAt == null
                     && c.ParentCommentId == null); // Only top-level

        if (request.ChapterId.HasValue)
            query = query.Where(c => c.ChapterId == request.ChapterId.Value);

        var total = await query.CountAsync(ct);

        var topLevel = await query
            .Include(c => c.User)
            .Include(c => c.Replies.Where(r => r.DeletedAt == null))
                .ThenInclude(r => r.User)
            .OrderByDescending(c => c.CreateAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        var items = topLevel.Select(c => MapComment(c)).ToList();
        var totalPages = (int)Math.Ceiling(total / (double)request.PageSize);
        return new PagedResult<CommentDto>(items, new PageMeta(request.Page, request.PageSize, total, totalPages));
    }

    private static CommentDto MapComment(Comment c) =>
        new(c.Id, c.StoryId, c.ChapterId,
            new CommentAuthorDto(c.User.Id, c.User.Username),
            c.ParentCommentId, c.Content,
            c.Status.ToString(), c.CreateAt, c.UpdateAt,
            c.DeletedAt.HasValue,
            c.Replies.OrderBy(r => r.CreateAt)
                .Select(r => MapComment(r)).ToList());
}
