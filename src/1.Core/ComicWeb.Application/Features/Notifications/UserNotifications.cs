using ComicWeb.Application.Common.Exceptions;
using ComicWeb.Application.Common.Interface;
using ComicWeb.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ComicWeb.Application.Features.Notifications;

public record UserNotificationDto(
    int Id,
    int? StoryId,
    string? StorySlug,
    string? StoryTitle,
    int? ChapterId,
    string? ChapterSlug,
    string Message,
    bool IsRead,
    DateTime CreateAt
);

public record GetUserNotificationsQuery(int UserId, int Page = 1, int PageSize = 20) : IRequest<(IReadOnlyList<UserNotificationDto> Items, int TotalCount)>;
public record MarkNotificationAsReadCommand(int UserId, int NotificationId) : IRequest;
public record MarkAllNotificationsAsReadCommand(int UserId) : IRequest;

public class UserNotificationsHandler :
    IRequestHandler<GetUserNotificationsQuery, (IReadOnlyList<UserNotificationDto> Items, int TotalCount)>,
    IRequestHandler<MarkNotificationAsReadCommand>,
    IRequestHandler<MarkAllNotificationsAsReadCommand>
{
    private readonly IApplicationDbContext _db;

    public UserNotificationsHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(IReadOnlyList<UserNotificationDto> Items, int TotalCount)> Handle(GetUserNotificationsQuery request, CancellationToken ct)
    {
        var query = _db.UserNotifications
            .AsNoTracking()
            .Where(x => x.UserId == request.UserId);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(x => x.CreateAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(x => new UserNotificationDto(
                x.Id,
                x.StoryId,
                x.Story != null ? x.Story.Slug : null,
                x.Story != null ? x.Story.Title : null,
                x.ChapterId,
                x.Chapter != null ? x.Chapter.Slug : null,
                x.Message,
                x.IsRead,
                x.CreateAt
            ))
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task Handle(MarkNotificationAsReadCommand request, CancellationToken ct)
    {
        var notification = await _db.UserNotifications
            .FirstOrDefaultAsync(x => x.Id == request.NotificationId && x.UserId == request.UserId, ct);

        if (notification == null)
        {
            throw new AppException("NOTIFICATION_NOT_FOUND", 404, "Notification not found", "Notification was not found.");
        }

        notification.IsRead = true;
        notification.UpdateAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
    }

    public async Task Handle(MarkAllNotificationsAsReadCommand request, CancellationToken ct)
    {
        var unreadNotifications = await _db.UserNotifications
            .Where(x => x.UserId == request.UserId && !x.IsRead)
            .ToListAsync(ct);

        if (unreadNotifications.Count == 0)
        {
            return;
        }

        foreach (var n in unreadNotifications)
        {
            n.IsRead = true;
            n.UpdateAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
    }
}
