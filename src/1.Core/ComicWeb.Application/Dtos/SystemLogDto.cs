using System;

namespace ComicWeb.Application.Dtos
{
    public record SystemLogDto(
        int Id,
        string? AdminAction,
        string? Details,
        string? IpAddress,
        DateTime CreatedAt
    );
}
