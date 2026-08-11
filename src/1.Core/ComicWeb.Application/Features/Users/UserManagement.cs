using ComicWeb.Application.Common.Exceptions;
using ComicWeb.Application.Common.Interface;
using ComicWeb.Application.Common.Interfaces;
using ComicWeb.Application.Features.Auth;
using ComicWeb.Domain.Entities;
using ComicWeb.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ComicWeb.Application.Features.Users;

public record UserDetailsDto(
    int Id,
    string Username,
    string Email,
    UserRole Role,
    bool IsActive,
    int FailedLoginAttempts,
    DateTime? LockoutEndAt,
    DateTime? LastLoginAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record GetUsersQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    UserRole? Role = null,
    bool? IsActive = null) : IRequest<(IReadOnlyList<UserDetailsDto> Items, int TotalCount)>;

public record GetUserDetailQuery(int Id) : IRequest<UserDetailsDto>;

public record UpdateUserStatusCommand(int Id, bool IsActive, int AdminUserId) : IRequest;

public record UpdateUserRoleCommand(int Id, UserRole Role, int AdminUserId) : IRequest;

public class UserManagementHandler :
    IRequestHandler<GetUsersQuery, (IReadOnlyList<UserDetailsDto> Items, int TotalCount)>,
    IRequestHandler<GetUserDetailQuery, UserDetailsDto>,
    IRequestHandler<UpdateUserStatusCommand>,
    IRequestHandler<UpdateUserRoleCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly IAuditWriter _auditWriter;
    private readonly IDateTimeProvider _clock;

    public UserManagementHandler(IApplicationDbContext db, IAuditWriter auditWriter, IDateTimeProvider clock)
    {
        _db = db;
        _auditWriter = auditWriter;
        _clock = clock;
    }

    public async Task<(IReadOnlyList<UserDetailsDto> Items, int TotalCount)> Handle(GetUsersQuery request, CancellationToken ct)
    {
        var q = _db.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var searchUpper = request.Search.Trim().ToUpperInvariant();
            q = q.Where(x => x.NormalizedUsername.Contains(searchUpper) || x.NormalizedEmail.Contains(searchUpper));
        }

        if (request.Role.HasValue)
        {
            q = q.Where(x => x.Role == request.Role.Value);
        }

        if (request.IsActive.HasValue)
        {
            q = q.Where(x => x.IsActive == request.IsActive.Value);
        }

        var total = await q.CountAsync(ct);

        var list = await q
            .OrderByDescending(x => x.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(x => new UserDetailsDto(
                x.Id,
                x.Username,
                x.Email,
                x.Role,
                x.IsActive,
                x.FailedLoginAttempts,
                x.LockoutEndAt,
                x.LastLoginAt,
                x.CreatedAt,
                x.UpdatedAt
            ))
            .ToListAsync(ct);

        return (list, total);
    }

    public async Task<UserDetailsDto> Handle(GetUserDetailQuery request, CancellationToken ct)
    {
        var x = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.Id, ct);

        if (x == null)
        {
            throw new AppException("USER_NOT_FOUND", 404, "Not found", "User was not found.");
        }

        return new UserDetailsDto(
            x.Id,
            x.Username,
            x.Email,
            x.Role,
            x.IsActive,
            x.FailedLoginAttempts,
            x.LockoutEndAt,
            x.LastLoginAt,
            x.CreatedAt,
            x.UpdatedAt
        );
    }

    public async Task Handle(UpdateUserStatusCommand request, CancellationToken ct)
    {
        var admin = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == request.AdminUserId, ct);
        if (admin == null)
        {
            throw new AppException("UNAUTHORIZED", 401, "Unauthorized", "Admin user not found.");
        }

        var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        if (u == null)
        {
            throw new AppException("USER_NOT_FOUND", 404, "Not found", "User was not found.");
        }

        if (u.Id == request.AdminUserId)
        {
            throw new AppException("VALIDATION_ERROR", 400, "Validation failed", "Bạn không thể tự vô hiệu hoá tài khoản của chính mình.");
        }

        var oldStatus = u.IsActive;
        u.UpdateStatus(request.IsActive, _clock.UtcNow);
        await _db.SaveChangesAsync(ct);

        // Ghi log
        await _auditWriter.WriteAsync(new AuditEvent(
            Action: "USER_STATUS_UPDATED",
            EntityType: "User",
            EntityId: u.Id.ToString(),
            Result: "Success",
            Details: new { username = u.Username, oldStatus, newStatus = u.IsActive },
            ActorType: "Admin",
            ActorUserId: admin.Id,
            ActorUsername: admin.Username
        ), ct);
    }

    public async Task Handle(UpdateUserRoleCommand request, CancellationToken ct)
    {
        var admin = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == request.AdminUserId, ct);
        if (admin == null)
        {
            throw new AppException("UNAUTHORIZED", 401, "Unauthorized", "Admin user not found.");
        }

        var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        if (u == null)
        {
            throw new AppException("USER_NOT_FOUND", 404, "Not found", "User was not found.");
        }

        if (u.Id == request.AdminUserId)
        {
            throw new AppException("VALIDATION_ERROR", 400, "Validation failed", "Bạn không thể tự thay đổi quyền của chính mình.");
        }

        var oldRole = u.Role;
        u.UpdateRole(request.Role, _clock.UtcNow);
        await _db.SaveChangesAsync(ct);

        // Ghi log
        await _auditWriter.WriteAsync(new AuditEvent(
            Action: "USER_ROLE_UPDATED",
            EntityType: "User",
            EntityId: u.Id.ToString(),
            Result: "Success",
            Details: new { username = u.Username, oldRole = oldRole.ToString(), newRole = u.Role.ToString() },
            ActorType: "Admin",
            ActorUserId: admin.Id,
            ActorUsername: admin.Username
        ), ct);
    }
}
