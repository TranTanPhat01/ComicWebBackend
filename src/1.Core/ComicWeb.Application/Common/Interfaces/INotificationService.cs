using System.Threading;
using System.Threading.Tasks;

namespace ComicWeb.Application.Common.Interfaces;

public interface INotificationService
{
    /// <summary>
    /// Generates notifications for all users following the specified story.
    /// </summary>
    Task CreateNewChapterNotificationAsync(int storyId, string storyTitle, int chapterId, string chapterTitle, CancellationToken cancellationToken);
}
