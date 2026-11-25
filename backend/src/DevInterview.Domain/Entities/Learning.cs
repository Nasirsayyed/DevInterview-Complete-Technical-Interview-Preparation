using DevInterview.Domain.Common;
using DevInterview.Domain.Enums;

namespace DevInterview.Domain.Entities;

public class LearningPath : ContentEntity
{
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ExperienceLevel Level { get; set; }
    public int EstimatedHours { get; set; }
    public bool IsPremium { get; set; }
    public bool IsPublished { get; set; }
    public int DisplayOrder { get; set; }
    public Guid? TechnologyId { get; set; }
    public Technology? Technology { get; set; }
    public string? ExternalKey { get; set; }
    public ICollection<LearningModule> Modules { get; set; } = new List<LearningModule>();
}

public class LearningModule : BaseEntity
{
    public Guid PathId { get; set; }
    public LearningPath Path { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DisplayOrder { get; set; }
    public ICollection<LearningLesson> Lessons { get; set; } = new List<LearningLesson>();
}

public class LearningLesson : BaseEntity
{
    public Guid ModuleId { get; set; }
    public LearningModule Module { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    /// <summary>Markdown lesson content (subtopics are headings inside the content).</summary>
    public string Content { get; set; } = string.Empty;
    public LessonType Type { get; set; }
    public int DisplayOrder { get; set; }
    public int EstimatedMinutes { get; set; } = 10;
    public string? ExerciseInstructions { get; set; }
    public Guid? QuizId { get; set; }
    public Quiz? Quiz { get; set; }
    public ICollection<LessonQuestion> Questions { get; set; } = new List<LessonQuestion>();
}

public class LessonQuestion
{
    public Guid LessonId { get; set; }
    public LearningLesson Lesson { get; set; } = null!;
    public Guid QuestionId { get; set; }
    public Question Question { get; set; } = null!;
    public int DisplayOrder { get; set; }
}

public class UserLearningProgress : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid LessonId { get; set; }
    public LearningLesson Lesson { get; set; } = null!;
    public Guid PathId { get; set; }
    public ProgressStatus Status { get; set; }
    public int TimeSpentSeconds { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class Flashcard : ContentEntity
{
    public Guid? TechnologyId { get; set; }
    public Technology? Technology { get; set; }
    public Guid? CategoryId { get; set; }
    public Category? Category { get; set; }
    public string Front { get; set; } = string.Empty;
    public string Back { get; set; } = string.Empty;
    public Difficulty Difficulty { get; set; }
    public bool IsPublished { get; set; } = true;
    public string? ExternalKey { get; set; }
}

/// <summary>Per-user spaced repetition state for a flashcard (SM-2).</summary>
public class FlashcardReview : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid FlashcardId { get; set; }
    public Flashcard Flashcard { get; set; } = null!;
    public double EaseFactor { get; set; } = 2.5;
    public int IntervalDays { get; set; }
    public int Repetitions { get; set; }
    public DateTime DueAt { get; set; }
    public DateTime? LastReviewedAt { get; set; }
    public FlashcardGrade? LastGrade { get; set; }
    public int Lapses { get; set; }
    public bool IsBookmarked { get; set; }
    public int TotalReviews { get; set; }
}

/// <summary>Immutable review event; ClientReviewId makes offline replays idempotent.</summary>
public class FlashcardReviewLog : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid FlashcardId { get; set; }
    public Guid ClientReviewId { get; set; }
    public FlashcardGrade Grade { get; set; }
    public DateTime ReviewedAt { get; set; }
}
