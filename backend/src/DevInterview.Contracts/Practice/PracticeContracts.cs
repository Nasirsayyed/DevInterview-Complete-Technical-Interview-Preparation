using DevInterview.Contracts.Common;

namespace DevInterview.Contracts.Practice;

// ---------------- Quizzes ----------------
public sealed class QuizQuery : PageQuery
{
    public string? Mode { get; set; }
    public Guid? TechnologyId { get; set; }
    public string? Difficulty { get; set; }
}

public sealed record QuizListItemDto(
    Guid Id, string Title, string? Description, string Mode, int QuestionCount, int? TimeLimitSeconds,
    int PassingPercentage, decimal NegativeMarkingFraction, string? Difficulty, RefDto? Technology, bool IsPremium,
    int AttemptCount, decimal? BestPercentage);

public sealed record CreateQuizRequest(
    string? Title, string Mode, IReadOnlyList<Guid>? TechnologyIds, IReadOnlyList<Guid>? CategoryIds, string? Difficulty,
    int QuestionCount, int? TimeLimitSeconds, decimal NegativeMarkingFraction, int PassingPercentage);

public sealed record QuizQuestionDto(
    Guid QuestionId, int Order, string Title, string Body, string? CodeSnippet, string? CodeLanguage, string Type,
    bool AllowsMultipleAnswers, IReadOnlyList<Content.QuestionOptionDto> Options, decimal Points,
    IReadOnlyList<Guid> SelectedOptionIds, bool IsMarkedForReview);

public sealed record QuizAttemptDto(
    Guid AttemptId, Guid QuizId, string Title, string Status, DateTime StartedAt, DateTime? ExpiresAt, DateTime ServerNow,
    int? TimeLimitSeconds, decimal NegativeMarkingFraction, int PassingPercentage, IReadOnlyList<QuizQuestionDto> Questions);

public sealed record SaveQuizAnswerRequest(Guid QuestionId, IReadOnlyList<Guid>? SelectedOptionIds, bool IsMarkedForReview, int TimeSpentSeconds);

public sealed record SubmitQuizRequest(Guid AttemptId, IReadOnlyList<SaveQuizAnswerRequest>? Answers);

public sealed record ReviewOptionDto(Guid Id, string Text, bool IsCorrect, bool Selected, string? Explanation);

public sealed record QuizReviewItemDto(
    Guid QuestionId, int Order, string Title, string Body, string? CodeSnippet, IReadOnlyList<ReviewOptionDto> Options,
    bool? IsCorrect, bool Skipped, decimal PointsAwarded, string Explanation, string Category);

public sealed record TopicPerformanceDto(string Name, Guid? Id, int Correct, int Total, decimal Accuracy);

public sealed record QuizResultDto(
    Guid AttemptId, Guid QuizId, string QuizTitle, string Status, int TotalQuestions, int Attempted, int Correct,
    int Incorrect, int Skipped, decimal Accuracy, decimal Score, decimal MaxScore, decimal Percentage, bool Passed,
    int PassingPercentage, int TotalTimeSeconds, DateTime StartedAt, DateTime? SubmittedAt,
    IReadOnlyList<QuizReviewItemDto> Review, IReadOnlyList<TopicPerformanceDto> TopicBreakdown,
    IReadOnlyList<string> RecommendedTopics, int XpAwarded);

public sealed record QuizAttemptSummaryDto(
    Guid AttemptId, Guid QuizId, string QuizTitle, string Status, decimal Percentage, bool Passed, int Correct,
    int TotalQuestions, DateTime StartedAt, DateTime? SubmittedAt);

// ---------------- Coding ----------------
public sealed record SupportedLanguageDto(string Code, string DisplayName, string Version);

public sealed class ChallengeQuery : PageQuery
{
    public string? Category { get; set; }
    public string? Difficulty { get; set; }
    /// <summary>"solved", "unsolved", "attempted".</summary>
    public string? Status { get; set; }
}

public sealed record ChallengeListItemDto(
    Guid Id, string Title, string Slug, string Category, string Difficulty, bool IsPremium, bool IsSolved, bool IsAttempted,
    decimal AcceptanceRate);

public sealed record SampleTestCaseDto(Guid Id, string Input, string ExpectedOutput, string? Explanation);

public sealed record ChallengeDetailDto(
    Guid Id, string Title, string Slug, string Category, string Difficulty, string ProblemStatement,
    string InputDescription, string OutputDescription, string Constraints, string? ExpectedTimeComplexity,
    string? ExpectedSpaceComplexity, int HintCount, IReadOnlyList<SampleTestCaseDto> SampleTestCases,
    int HiddenTestCaseCount, IReadOnlyDictionary<string, string> StarterCode, IReadOnlyList<string> AllowedLanguages,
    int TimeLimitMs, int MemoryLimitMb, bool IsSolved, bool IsPremium, bool SolutionAvailable);

public sealed record ChallengeHintDto(int Index, int Total, string Text);

public sealed record ChallengeSolutionDto(Guid ChallengeId, string Explanation, string? ExpectedTimeComplexity, string? ExpectedSpaceComplexity);

public sealed record RunCodeRequest(Guid ChallengeId, string LanguageCode, string SourceCode, string? CustomInput);

public sealed record SubmitCodeRequest(Guid ChallengeId, string LanguageCode, string SourceCode, string? IdempotencyKey);

public sealed record TestResultDto(
    int Ordinal, bool IsSample, bool IsCustom, string? Input, string? ExpectedOutput, string? ActualOutput, string? ErrorOutput,
    bool Passed, string Status, int RuntimeMs);

public sealed record RunCodeResponse(string Status, string? CompileOutput, IReadOnlyList<TestResultDto> Results, int PassedCount, int TotalCount);

public sealed record SubmissionDto(
    Guid Id, Guid ChallengeId, string ChallengeTitle, string LanguageCode, string Kind, string Status, int PassedCount,
    int TotalCount, int? MaxRuntimeMs, DateTime CreatedAt, string? CompileOutput, IReadOnlyList<TestResultDto> Results,
    int XpAwarded, string? SourceCode);

public sealed class SubmissionQuery : PageQuery
{
    public Guid? ChallengeId { get; set; }
}
