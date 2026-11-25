using DevInterview.Contracts.Common;

namespace DevInterview.Contracts.Admin;

public sealed record TrendPointDto(DateOnly Date, int Value);

public sealed record AdminDashboardDto(
    int TotalUsers, int ActiveUsers7d, int NewUsers7d, int PremiumUsers, int TotalQuestions, int PublishedQuestions,
    int DraftQuestions, int TotalChallenges, int QuizAttempts7d, int Submissions7d, int MockInterviews7d, int AiCalls7d,
    decimal AiCost7d, int OpenReports, IReadOnlyList<TrendPointDto> SignupsTrend, IReadOnlyList<TrendPointDto> ActivityTrend);

// ---- Users ----
public sealed class AdminUserQuery : PageQuery
{
    public string? Role { get; set; }
    public bool? IsActive { get; set; }
}

public sealed record AdminUserDto(
    Guid Id, string Email, string FullName, IReadOnlyList<string> Roles, bool IsActive, bool EmailConfirmed,
    DateTime CreatedAt, DateTime? LastLoginAt, string Tier, DateTime? PremiumExpiresAt);

public sealed record UpdateUserRolesRequest(IReadOnlyList<string> Roles);
public sealed record SetUserActiveRequest(bool IsActive);
public sealed record GrantSubscriptionRequest(Guid PlanId, int Months, string? Notes);

// ---- Taxonomy ----
public sealed record TechnologyUpsertRequest(string Name, string? Slug, string? Description, string Group, string? IconKey, int DisplayOrder, bool IsActive);
public sealed record CategoryUpsertRequest(Guid TechnologyId, string Name, string? Slug, string? Description, int DisplayOrder, bool IsActive);
public sealed record TopicUpsertRequest(Guid CategoryId, string Name, string? Slug, int DisplayOrder, bool IsActive);
public sealed record AdminTechnologyDto(Guid Id, string Name, string Slug, string? Description, string Group, string? IconKey, int DisplayOrder, bool IsActive, int CategoryCount, int QuestionCount);
public sealed record AdminCategoryDto(Guid Id, Guid TechnologyId, string TechnologyName, string Name, string Slug, string? Description, int DisplayOrder, bool IsActive, int QuestionCount, IReadOnlyList<AdminTopicDto> Topics);
public sealed record AdminTopicDto(Guid Id, Guid CategoryId, string Name, string Slug, int DisplayOrder, bool IsActive);

// ---- Questions ----
public sealed class AdminQuestionQuery : PageQuery
{
    public Guid? TechnologyId { get; set; }
    public Guid? CategoryId { get; set; }
    public string? Status { get; set; }
    public string? Difficulty { get; set; }
    public string? Type { get; set; }
}

public sealed record OptionUpsertDto(string Text, bool IsCorrect, string? Explanation);

public sealed record QuestionUpsertRequest(
    Guid TechnologyId, Guid CategoryId, Guid? TopicId, string Title, string Body, string? CodeSnippet, string? CodeLanguage,
    string Type, string Difficulty, int EstimatedSeconds, bool IsPremium, string Status, IReadOnlyList<string>? Tags,
    IReadOnlyList<OptionUpsertDto>? Options, string ShortAnswer, string DetailedExplanation, string? ReferenceAnswer);

public sealed record AdminOptionDto(Guid Id, string Text, bool IsCorrect, string? Explanation, int DisplayOrder);

public sealed record AdminQuestionDto(
    Guid Id, Guid TechnologyId, string TechnologyName, Guid CategoryId, string CategoryName, Guid? TopicId, string Title,
    string Body, string? CodeSnippet, string? CodeLanguage, string Type, string Difficulty, int EstimatedSeconds,
    bool IsPremium, string Status, IReadOnlyList<string> Tags, IReadOnlyList<AdminOptionDto> Options, string ShortAnswer,
    string DetailedExplanation, string? ReferenceAnswer, DateTime CreatedAt, DateTime UpdatedAt, DateTime? PublishedAt);

public sealed record AdminQuestionListItemDto(
    Guid Id, string Title, string TechnologyName, string CategoryName, string Type, string Difficulty, string Status,
    bool IsPremium, DateTime UpdatedAt);

public sealed record BulkStatusRequest(IReadOnlyList<Guid> Ids, string Status);

public sealed record ImportErrorDto(int Row, string Message);
public sealed record ImportResultDto(int Created, int Updated, int Skipped, IReadOnlyList<ImportErrorDto> Errors, bool DryRun);

// ---- Question reports ----
public sealed record QuestionReportDto(Guid Id, Guid QuestionId, string QuestionTitle, string Reason, string? Details, string Status, string? ResolutionNote, string ReporterEmail, DateTime CreatedAt);
public sealed record ResolveReportRequest(string Status, string? ResolutionNote);

// ---- Quizzes ----
public sealed record QuizUpsertRequest(
    string Title, string? Description, string Mode, int? TimeLimitSeconds, int PassingPercentage,
    decimal NegativeMarkingFraction, bool IsPublished, bool IsPremium, Guid? TechnologyId, string? Difficulty,
    IReadOnlyList<Guid> QuestionIds);

public sealed record AdminQuizDto(
    Guid Id, string Title, string? Description, string Mode, int? TimeLimitSeconds, int PassingPercentage,
    decimal NegativeMarkingFraction, bool IsPublished, bool IsPremium, Guid? TechnologyId, string? Difficulty,
    IReadOnlyList<Guid> QuestionIds, int AttemptCount, DateTime UpdatedAt);

// ---- Coding challenges ----
public sealed record TestCaseUpsertDto(Guid? Id, string Input, string ExpectedOutput, bool IsSample, string? Explanation);

public sealed record ChallengeUpsertRequest(
    string Title, string? Slug, string Category, string Difficulty, string ProblemStatement, string InputDescription,
    string OutputDescription, string Constraints, string? ExpectedTimeComplexity, string? ExpectedSpaceComplexity,
    IReadOnlyList<string>? Hints, string SolutionExplanation, bool IsPremium, string Status, int TimeLimitMs,
    int MemoryLimitMb, IReadOnlyList<string> AllowedLanguages, IReadOnlyDictionary<string, string>? StarterCode,
    IReadOnlyList<TestCaseUpsertDto>? TestCases);

public sealed record AdminChallengeDto(
    Guid Id, string Title, string Slug, string Category, string Difficulty, string ProblemStatement, string InputDescription,
    string OutputDescription, string Constraints, string? ExpectedTimeComplexity, string? ExpectedSpaceComplexity,
    IReadOnlyList<string> Hints, string SolutionExplanation, bool IsPremium, string Status, int TimeLimitMs,
    int MemoryLimitMb, IReadOnlyList<string> AllowedLanguages, IReadOnlyDictionary<string, string> StarterCode,
    IReadOnlyList<TestCaseUpsertDto> TestCases, DateTime UpdatedAt);

public sealed record AdminChallengeListItemDto(Guid Id, string Title, string Category, string Difficulty, string Status, bool IsPremium, int TestCaseCount, int SubmissionCount);

// ---- Learning ----
public sealed record LessonUpsertDto(Guid? Id, string Title, string Content, string Type, int EstimatedMinutes, string? ExerciseInstructions, Guid? QuizId, IReadOnlyList<Guid>? QuestionIds);
public sealed record ModuleUpsertDto(Guid? Id, string Title, string? Description, IReadOnlyList<LessonUpsertDto> Lessons);
public sealed record LearningPathUpsertRequest(
    string Title, string? Slug, string Description, string Level, int EstimatedHours, bool IsPremium, bool IsPublished,
    int DisplayOrder, Guid? TechnologyId, IReadOnlyList<ModuleUpsertDto> Modules);
public sealed record AdminLearningPathDto(
    Guid Id, string Title, string Slug, string Description, string Level, int EstimatedHours, bool IsPremium,
    bool IsPublished, int DisplayOrder, Guid? TechnologyId, IReadOnlyList<ModuleUpsertDto> Modules, int EnrolledUsers);
public sealed record AdminLearningPathListItemDto(Guid Id, string Title, string Level, bool IsPublished, bool IsPremium, int ModuleCount, int LessonCount);

// ---- Flashcards ----
public sealed record FlashcardUpsertRequest(Guid? TechnologyId, Guid? CategoryId, string Front, string Back, string Difficulty, bool IsPublished);
public sealed record AdminFlashcardDto(Guid Id, Guid? TechnologyId, string? TechnologyName, Guid? CategoryId, string Front, string Back, string Difficulty, bool IsPublished);
public sealed class AdminFlashcardQuery : PageQuery { public Guid? TechnologyId { get; set; } }

// ---- Companies ----
public sealed record RoundUpsertDto(string Name, string Description, string Source, string? SourceReference);
public sealed record CompanyTopicUpsertDto(string Name, string? Description, Guid? TechnologyId);
public sealed record CollectionUpsertDto(Guid? Id, string Title, string? Description, string Kind, string Source, string? SourceReference, IReadOnlyList<Guid>? QuestionIds, IReadOnlyList<Guid>? ChallengeIds);
public sealed record CompanyUpsertRequest(
    string Name, string? Slug, string Overview, string OverviewSource, string? Industry, string? LogoUrl, string? WebsiteUrl,
    string DifficultyLevel, int DisplayOrder, bool IsPremium, bool IsActive, IReadOnlyList<RoundUpsertDto>? Rounds,
    IReadOnlyList<CompanyTopicUpsertDto>? Topics, IReadOnlyList<CollectionUpsertDto>? Collections);
public sealed record AdminCompanyDto(
    Guid Id, string Name, string Slug, string Overview, string OverviewSource, string? Industry, string? LogoUrl,
    string? WebsiteUrl, string DifficultyLevel, int DisplayOrder, bool IsPremium, bool IsActive,
    IReadOnlyList<RoundUpsertDto> Rounds, IReadOnlyList<CompanyTopicUpsertDto> Topics, IReadOnlyList<CollectionUpsertDto> Collections);

// ---- AI ----
public sealed record AiPromptDto(Guid Id, string Key, int Version, string SystemPrompt, string? Notes, bool IsActive, DateTime UpdatedAt);
public sealed record AiPromptUpsertRequest(string Key, string SystemPrompt, string? Notes, bool IsActive);
public sealed record AiProviderSettingDto(Guid Id, string ProviderName, string Model, bool IsEnabled, int Priority, decimal InputCostPerMillionTokens, decimal OutputCostPerMillionTokens, bool ApiKeyConfigured);
public sealed record AiProviderSettingUpsertRequest(string ProviderName, string Model, bool IsEnabled, int Priority, decimal InputCostPerMillionTokens, decimal OutputCostPerMillionTokens);
public sealed record AiUsageSummaryDto(int Calls, int FailedCalls, long PromptTokens, long CompletionTokens, decimal EstimatedCostUsd, IReadOnlyList<AiUsageByFeatureDto> ByFeature);
public sealed record AiUsageByFeatureDto(string Feature, int Calls, decimal EstimatedCostUsd, int AverageLatencyMs);

// ---- Subscriptions ----
public sealed record SubscriptionPlanUpsertRequest(
    string Code, string Name, string Description, string Tier, long PriceMicros, string Currency, int BillingPeriodMonths,
    string? GooglePlayProductId, string? GooglePlayBasePlanId, IReadOnlyList<string> Features, bool IsActive, int DisplayOrder);
public sealed record AdminSubscriptionDto(Guid Id, Guid UserId, string UserEmail, string PlanName, string Status, string Provider, DateTime StartsAt, DateTime ExpiresAt, bool AutoRenewing, string? OrderId);

// ---- Notifications ----
public sealed record BroadcastNotificationRequest(string Title, string Body, string Category, string? DeepLink, string Audience, bool SendPush);
public sealed record BroadcastResultDto(Guid BroadcastId, int Recipients, int PushQueued);

// ---- Reports / audit / settings ----
public sealed class AuditLogQuery : PageQuery
{
    public string? EntityType { get; set; }
    public Guid? UserId { get; set; }
}
public sealed record AuditLogDto(Guid Id, Guid? UserId, string? UserEmail, string Action, string EntityType, string? EntityId, string? Details, string? IpAddress, DateTime CreatedAt);
public sealed record AppSettingDto(string Key, string Value, string? Description);
public sealed record ContentReportDto(
    IReadOnlyList<TechnologyContentStatDto> Technologies, IReadOnlyList<HardestQuestionDto> HardestQuestions,
    IReadOnlyList<ChallengeStatDto> Challenges);
public sealed record TechnologyContentStatDto(string Technology, int Questions, int Attempts, decimal Accuracy);
public sealed record HardestQuestionDto(Guid QuestionId, string Title, int Attempts, decimal Accuracy);
public sealed record ChallengeStatDto(Guid ChallengeId, string Title, int Submissions, decimal AcceptanceRate);
