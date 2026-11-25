using DevInterview.Contracts.Common;

namespace DevInterview.Contracts.Content;

public sealed record TechnologyDto(
    Guid Id, string Name, string Slug, string? Description, string Group, string? IconKey, int QuestionCount, int CategoryCount);

public sealed record TopicDto(Guid Id, string Name, string Slug);

public sealed record CategoryDto(
    Guid Id, Guid TechnologyId, string Name, string Slug, string? Description, int QuestionCount, IReadOnlyList<TopicDto> Topics);

public sealed class QuestionQuery : PageQuery
{
    public Guid? TechnologyId { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid? TopicId { get; set; }
    public string? Difficulty { get; set; }
    public string? Type { get; set; }
    public string? Tag { get; set; }
    public bool BookmarkedOnly { get; set; }
    /// <summary>Optional: "unattempted", "incorrect", "correct".</summary>
    public string? AttemptFilter { get; set; }
}

public sealed record QuestionListItemDto(
    Guid Id, string Title, RefDto Technology, RefDto Category, string Difficulty, string Type, int EstimatedSeconds,
    IReadOnlyList<string> Tags, bool IsPremium, bool IsBookmarked, bool? LastAttemptCorrect);

public sealed record QuestionOptionDto(Guid Id, string Text, int DisplayOrder);

public sealed record QuestionDetailDto(
    Guid Id, string Title, string Body, string? CodeSnippet, string? CodeLanguage,
    RefDto Technology, RefDto Category, RefDto? Topic, string Difficulty, string Type, int EstimatedSeconds,
    IReadOnlyList<string> Tags, IReadOnlyList<QuestionOptionDto> Options, bool IsObjective, bool AllowsMultipleAnswers,
    bool IsBookmarked, string? Note, bool IsPremium, Guid? PreviousId, Guid? NextId, string ShareText);

public sealed record OptionExplanationDto(Guid OptionId, bool IsCorrect, string? Explanation);

public sealed record QuestionAnswerDto(
    Guid QuestionId, string ShortAnswer, string DetailedExplanation, IReadOnlyList<Guid> CorrectOptionIds,
    IReadOnlyList<OptionExplanationDto> Options);

public sealed record AttemptQuestionRequest(IReadOnlyList<Guid>? SelectedOptionIds, int TimeSpentSeconds, bool? SelfAssessedCorrect);

public sealed record AttemptQuestionResult(bool? IsCorrect, IReadOnlyList<Guid> CorrectOptionIds, int XpAwarded, QuestionAnswerDto Answer);

public sealed record SaveNoteRequest(string Content);

public sealed record NoteDto(Guid QuestionId, string QuestionTitle, string Technology, string Content, DateTime UpdatedAt);

public sealed record ReportQuestionRequest(string Reason, string? Details);

public sealed record BookmarkStateDto(Guid QuestionId, bool IsBookmarked, DateTime UpdatedAt);

public sealed record NoteStateDto(Guid QuestionId, string Content, bool IsDeleted, DateTime UpdatedAt);

// ---- Companies ----
public sealed record CompanyListItemDto(
    Guid Id, string Name, string Slug, string? Industry, string? LogoUrl, string DifficultyLevel, bool IsPremium,
    int TopicCount, int CollectionCount);

public sealed record InterviewRoundDto(int Order, string Name, string Description, string Source, string? SourceReference);
public sealed record CompanyTopicDto(Guid Id, string Name, string? Description, RefDto? Technology);
public sealed record CompanyCollectionDto(
    Guid Id, string Title, string? Description, string Kind, string Source, string? SourceReference, int QuestionCount, int ChallengeCount);

public sealed record CompanyDetailDto(
    Guid Id, string Name, string Slug, string Overview, string OverviewSource, string? Industry, string? WebsiteUrl,
    string DifficultyLevel, bool IsPremium, IReadOnlyList<InterviewRoundDto> Rounds, IReadOnlyList<CompanyTopicDto> Topics,
    IReadOnlyList<CompanyCollectionDto> Collections, string Disclaimer);

public sealed record CompanyCollectionDetailDto(
    CompanyCollectionDto Collection, string CompanyName, IReadOnlyList<QuestionListItemDto> Questions,
    IReadOnlyList<Practice.ChallengeListItemDto> Challenges, string Disclaimer);

// ---- Collections / saved content ----
public sealed record CollectionDto(Guid Id, string Name, string? Description, int ItemCount, DateTime CreatedAt);
public sealed record CollectionItemDto(Guid Id, string ItemType, Guid ItemId, string Title, string? Subtitle, DateTime AddedAt);
public sealed record CollectionDetailDto(Guid Id, string Name, string? Description, IReadOnlyList<CollectionItemDto> Items);
public sealed record CreateCollectionRequest(string Name, string? Description);
public sealed record AddCollectionItemRequest(string ItemType, Guid ItemId);

// ---- Search ----
public sealed class SearchQuery : PageQuery
{
    public string Q { get; set; } = string.Empty;
    /// <summary>question, technology, category, challenge, learningPath, flashcard, note (null = all).</summary>
    public string? Type { get; set; }
    public Guid? TechnologyId { get; set; }
    public string? Difficulty { get; set; }
}

public sealed record SearchHitDto(string Type, Guid Id, string Title, string? Subtitle, string? Technology, string? Difficulty, Guid? ParentId);

public sealed record SearchResultsDto(string Query, PagedResult<SearchHitDto> Hits, IReadOnlyDictionary<string, int> CountsByType);
