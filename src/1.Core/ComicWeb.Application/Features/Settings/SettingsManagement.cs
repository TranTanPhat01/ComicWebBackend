using ComicWeb.Application.Common.Exceptions;
using ComicWeb.Application.Common.Interface;
using ComicWeb.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ComicWeb.Application.Features.Settings;

public sealed record SettingItemDto(string Key, string Value, string? Description);
public sealed record GetPublicSettingsQuery() : IRequest<IReadOnlyList<SettingItemDto>>;
public sealed record GetAdminSettingsQuery() : IRequest<IReadOnlyList<SettingItemDto>>;
public sealed record SaveSettingsCommand(List<SettingItemDto> Settings) : IRequest;

public sealed class SettingsManagementHandler :
    IRequestHandler<GetPublicSettingsQuery, IReadOnlyList<SettingItemDto>>,
    IRequestHandler<GetAdminSettingsQuery, IReadOnlyList<SettingItemDto>>,
    IRequestHandler<SaveSettingsCommand>
{
    private readonly IApplicationDbContext _db;
    
    private static readonly string[] AllowedKeys = new[]
    {
        "GlobalHeadScripts",
        "GlobalBodyScripts",
        "CustomMetaTags"
    };

    public SettingsManagementHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<SettingItemDto>> Handle(GetPublicSettingsQuery request, CancellationToken ct)
    {
        // Public settings chỉ trả về các keys scripts an toàn
        var settings = await _db.SystemSettings.AsNoTracking()
            .Where(x => AllowedKeys.Contains(x.Key))
            .Select(x => new SettingItemDto(x.Key, x.Value, x.Description))
            .ToListAsync(ct);

        // Đảm bảo trả về đủ các key rỗng nếu DB chưa được seed
        foreach (var key in AllowedKeys)
        {
            if (!settings.Any(x => x.Key == key))
            {
                ((List<SettingItemDto>)settings).Add(new SettingItemDto(key, "", ""));
            }
        }

        return settings;
    }

    public async Task<IReadOnlyList<SettingItemDto>> Handle(GetAdminSettingsQuery request, CancellationToken ct)
    {
        var settings = await _db.SystemSettings.AsNoTracking()
            .Select(x => new SettingItemDto(x.Key, x.Value, x.Description))
            .ToListAsync(ct);

        // Thêm các key mặc định nếu chưa có
        var settingsList = settings.ToList();
        foreach (var key in AllowedKeys)
        {
            if (!settingsList.Any(x => x.Key == key))
            {
                settingsList.Add(new SettingItemDto(key, "", "Global script/meta tag insert key."));
            }
        }

        return settingsList;
    }

    public async Task Handle(SaveSettingsCommand request, CancellationToken ct)
    {
        foreach (var item in request.Settings)
        {
            var key = item.Key.Trim();
            if (string.IsNullOrWhiteSpace(key)) continue;

            var setting = await _db.SystemSettings
                .FirstOrDefaultAsync(x => x.Key == key, ct);

            if (setting == null)
            {
                setting = new SystemSetting
                {
                    Key = key,
                    Value = item.Value ?? "",
                    Description = item.Description ?? ""
                };
                _db.SystemSettings.Add(setting);
            }
            else
            {
                setting.Value = item.Value ?? "";
                if (item.Description != null)
                {
                    setting.Description = item.Description;
                }
            }
        }

        await _db.SaveChangesAsync(ct);
    }
}
