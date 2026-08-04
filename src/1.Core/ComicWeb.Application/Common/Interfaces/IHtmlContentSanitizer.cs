namespace ComicWeb.Application.Common.Interfaces;

public interface IHtmlContentSanitizer
{
    string Sanitize(string? html);
    bool IsMeaningful(string? html);
}
