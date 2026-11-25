using DevInterview.Domain.Common;
using DevInterview.Domain.Enums;

namespace DevInterview.Domain.Entities;

public class Technology : ContentEntity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>Logical grouping shown in the Technology Explorer, e.g. "Languages", ".NET", "Frontend".</summary>
    public string Group { get; set; } = "General";
    public string? IconKey { get; set; }
    public int DisplayOrder { get; set; }
    public ICollection<Category> Categories { get; set; } = new List<Category>();
}

public class Category : ContentEntity
{
    public Guid TechnologyId { get; set; }
    public Technology Technology { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DisplayOrder { get; set; }
    public ICollection<Topic> Topics { get; set; } = new List<Topic>();
}

public class Topic : ContentEntity
{
    public Guid CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
}

public class Tag : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
}

public class Question : ContentEntity
{
    public Guid TechnologyId { get; set; }
    public Technology Technology { get; set; } = null!;
    public Guid CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public Guid? TopicId { get; set; }
    public Topic? Topic { get; set; }

    public string Title { get; set; } = string.Empty;
    /// <summary>Markdown body of the question.</summary>
    public string Body { get; set; } = string.Empty;
    public string? CodeSnippet { get; set; }
    public string? CodeLanguage { get; set; }
    public QuestionType Type { get; set; }
    public Difficulty Difficulty { get; set; }
    public int EstimatedSeconds { get; set; } = 120;
    public ContentStatus Status { get; set; } = ContentStatus.Draft;
    public bool IsPremium { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime? PublishedAt { get; set; }
    /// <summary>Stable external key used by the seed/import pipeline to upsert idempotently.</summary>
    public string? ExternalKey { get; set; }

    public QuestionAnswer? Answer { get; set; }
    public ICollection<QuestionOption> Options { get; set; } = new List<QuestionOption>();
    public ICollection<QuestionTag> Tags { get; set; } = new List<QuestionTag>();

    /// <summary>Objective questions have options and can be auto-graded.</summary>
    public bool IsObjective => Type is QuestionType.MultipleChoice or QuestionType.SingleChoice
        or QuestionType.MultipleSelect or QuestionType.TrueFalse
        || (Type is QuestionType.OutputPrediction or QuestionType.Debugging or QuestionType.CodeBased && Options.Count > 0);
}

public class QuestionOption : BaseEntity
{
    public Guid QuestionId { get; set; }
    public Question Question { get; set; } = null!;
    public string Text { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
    public int DisplayOrder { get; set; }
    public string? Explanation { get; set; }
}

public class QuestionAnswer : BaseEntity
{
    public Guid QuestionId { get; set; }
    public Question Question { get; set; } = null!;
    /// <summary>Concise answer shown on "Show Answer".</summary>
    public string ShortAnswer { get; set; } = string.Empty;
    /// <summary>Markdown explanation shown on "Show Detailed Explanation".</summary>
    public string DetailedExplanation { get; set; } = string.Empty;
    /// <summary>Expert reference answer used by the AI answer evaluator.</summary>
    public string? ReferenceAnswer { get; set; }
}

public class QuestionTag
{
    public Guid QuestionId { get; set; }
    public Question Question { get; set; } = null!;
    public Guid TagId { get; set; }
    public Tag Tag { get; set; } = null!;
}

public class QuestionBookmark : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid QuestionId { get; set; }
    public Question Question { get; set; } = null!;
    /// <summary>Client timestamp of the last change, used for last-writer-wins offline sync.</summary>
    public DateTime ClientUpdatedAt { get; set; }
    public bool IsRemoved { get; set; }
}

public class QuestionNote : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid QuestionId { get; set; }
    public Question Question { get; set; } = null!;
    public string Content { get; set; } = string.Empty;
    public DateTime ClientUpdatedAt { get; set; }
    public bool IsRemoved { get; set; }
}

public class QuestionReport : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid QuestionId { get; set; }
    public Question Question { get; set; } = null!;
    public string Reason { get; set; } = string.Empty;
    public string? Details { get; set; }
    public QuestionReportStatus Status { get; set; }
    public string? ResolutionNote { get; set; }
}

/// <summary>A user's practice attempt on a single question (outside of quizzes).</summary>
public class QuestionAttempt : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid QuestionId { get; set; }
    public Question Question { get; set; } = null!;
    public Guid TechnologyId { get; set; }
    public Guid CategoryId { get; set; }
    public Difficulty Difficulty { get; set; }
    /// <summary>Null for subjective questions that were self-assessed as unknown.</summary>
    public bool? IsCorrect { get; set; }
    public string SelectedOptionIds { get; set; } = string.Empty;
    public int TimeSpentSeconds { get; set; }
    public Guid? QuizAttemptId { get; set; }
}

public class SavedCollection : BaseEntity
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ICollection<SavedCollectionItem> Items { get; set; } = new List<SavedCollectionItem>();
}

public class SavedCollectionItem : BaseEntity
{
    public Guid CollectionId { get; set; }
    public SavedCollection Collection { get; set; } = null!;
    public SavedItemType ItemType { get; set; }
    public Guid ItemId { get; set; }
}
