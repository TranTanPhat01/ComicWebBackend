namespace ComicWeb.Application.Common.Interfaces;

public interface IPublicCacheKeyFactory
{
    string CreateStoryListKey(int page, int pageSize, string? query, string? author, string sort);
    string CreateStoryDetailKey(string storySlug);
    string CreateChapterListKey(string storySlug, int page, int pageSize, string sort);
    string CreateChapterDetailKey(string storySlug, string chapterSlug);
}
