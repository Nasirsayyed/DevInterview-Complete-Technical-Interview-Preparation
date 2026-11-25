using DevInterview.Contracts.Common;

namespace DevInterview.Contracts.Learning;

public sealed record LearningPathListItemDto(
    Guid Id, string Title, string Slug, string Description, string Level, int EstimatedHours, int ModuleCount,
    int LessonCount, int CompletedLessons, int ProgressPercent, bool IsPremium, RefDto? Technology);

public sealed record LessonSummaryDto(Guid Id, string Title, string Type, int EstimatedMinutes, string Status);

public sealed record LearningModuleDto(Guid Id, string Title, string? Description, int Order, IReadOnlyList<LessonSummaryDto> Lessons, int CompletedLessons);

public sealed record LearningPathDetailDto(
    Guid Id, string Title, string Slug, string Description, string Level, int EstimatedHours, bool IsPremium,
    RefDto? Technology, int LessonCount, int CompletedLessons, int ProgressPercent, Guid? NextLessonId,
    IReadOnlyList<LearningModuleDto> Modules);

public sealed record LessonDetailDto(
    Guid Id, Guid PathId, string PathTitle, Guid ModuleId, string ModuleTitle, string Title, string Content, string Type,
    int EstimatedMinutes, string? ExerciseInstructions, Guid? QuizId, IReadOnlyList<Content.QuestionListItemDto> Questions,
    string Status, Guid? PreviousLessonId, Guid? NextLessonId);

public sealed record UpdateLessonProgressRequest(Guid LessonId, string Status, int TimeSpentSeconds);

public sealed record LessonProgressDto(Guid LessonId, string Status, int PathProgressPercent, int XpAwarded);

// ---------------- Flashcards ----------------
public sealed class FlashcardQuery
{
    public Guid? TechnologyId { get; set; }
    public bool DueOnly { get; set; } = true;
    public bool BookmarkedOnly { get; set; }
    public int Limit { get; set; } = 20;
}

public sealed record FlashcardDto(
    Guid Id, string Front, string Back, RefDto? Technology, RefDto? Category, string Difficulty, bool IsBookmarked,
    DateTime? DueAt, int Repetitions, int IntervalDays, bool IsNew, string? LastGrade);

public sealed record FlashcardDeckDto(Guid? TechnologyId, string Name, int TotalCards, int DueCount, int NewCount, int MasteredCount);

public sealed record ReviewFlashcardRequest(Guid FlashcardId, string Grade, Guid ClientReviewId, DateTime? ReviewedAt);

public sealed record FlashcardReviewResultDto(Guid FlashcardId, DateTime NextDueAt, int IntervalDays, double EaseFactor, int XpAwarded, bool Duplicate);

public sealed record BookmarkFlashcardRequest(bool IsBookmarked);
