namespace ComicWeb.Application.Common.Interfaces;

/// <summary>Creates a stable URL-friendly slug from editor supplied text.</summary>
public interface ISlugGenerator
{
    string Generate(string value);
}
