using System.Threading;
using System.Threading.Tasks;

namespace ComicWeb.Application.Common.Interfaces;

public sealed class ScheduledPublishingResult
{
    public int StoriesScanned { get; set; }
    public int StoriesPublished { get; set; }
    public int StoriesSkipped { get; set; }
    public int StoriesFailed { get; set; }
    public int ChaptersScanned { get; set; }
    public int ChaptersPublished { get; set; }
    public int ChaptersSkipped { get; set; }
    public int ChaptersFailed { get; set; }
}

public interface IScheduledPublishingService
{
    Task<ScheduledPublishingResult> PublishDueContentAsync(CancellationToken cancellationToken = default);
}
