namespace ComicWeb.Domain.Constants;

public static class AuditActions
{
    public const string StoryCreated = "STORY_CREATED";
    public const string StoryUpdated = "STORY_UPDATED";
    public const string StoryPublished = "STORY_PUBLISHED";
    public const string StoryUnpublished = "STORY_UNPUBLISHED";
    public const string StoryHidden = "STORY_HIDDEN";
    public const string StoryCompleted = "STORY_COMPLETED";
    public const string StoryScheduled = "STORY_SCHEDULED";
    public const string StoryDeleted = "STORY_DELETED";
    public const string StoryRestored = "STORY_RESTORED";

    public const string ChapterCreated = "CHAPTER_CREATED";
    public const string ChapterUpdated = "CHAPTER_UPDATED";
    public const string ChapterPublished = "CHAPTER_PUBLISHED";
    public const string ChapterUnpublished = "CHAPTER_UNPUBLISHED";
    public const string ChapterHidden = "CHAPTER_HIDDEN";
    public const string ChapterScheduled = "CHAPTER_SCHEDULED";
    public const string ChapterDeleted = "CHAPTER_DELETED";
    public const string ChapterRestored = "CHAPTER_RESTORED";

    public const string LoginSucceeded = "LOGIN_SUCCEEDED";
    public const string LoginFailed = "LOGIN_FAILED";
    public const string PasswordChanged = "PASSWORD_CHANGED";
    public const string AccountLocked = "ACCOUNT_LOCKED";

    public const string ScheduledStoryPublished = "SCHEDULED_STORY_PUBLISHED";
    public const string ScheduledChapterPublished = "SCHEDULED_CHAPTER_PUBLISHED";
}
