namespace ComicWeb.Application.Common.Interfaces;

public interface IAuditDetailsSerializer
{
    string? Serialize(object? details);
}
