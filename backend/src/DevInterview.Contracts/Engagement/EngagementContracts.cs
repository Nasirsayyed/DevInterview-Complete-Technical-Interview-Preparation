using DevInterview.Contracts.Common;
using DevInterview.Contracts.Content;
using DevInterview.Contracts.Learning;
using DevInterview.Contracts.Practice;

namespace DevInterview.Contracts.Engagement;

public sealed record LevelDto(int Level, string Title, int TotalXp, int CurrentLevelXp, int NextLevelXp, double Progress);

public sealed record ContinueLearningDto(Guid PathId, string PathTitle, Guid? LessonId, string? LessonTitle, int ProgressPercent);

public sealed record RecentQuestionDto(Guid QuestionId, string Title, string Technology, bool? IsCorrect, DateTime AttemptedAt);

public sealed record InterviewReminderDto(Guid Id, string Title, string? CompanyName, DateTime ScheduledAt, string? Notes);

public sealed record CreateInterviewReminderRequest(string Title, string? CompanyName, DateTime ScheduledAt, string? Notes);

public sealed record DashboardSummaryDto(
    string FirstName, string? ProfilePhotoUrl, string Tier, LevelDto Level, int CurrentStreak, int LongestStreak,
    int PreparationScore, int QuestionsAttempted, int CorrectAnswers, int IncorrectAnswers, int ChallengesCompleted,
    int MockInterviewsCompleted, int AverageMockScore, int LearningMinutes, int WeeklyMinutes, int WeeklyGoalMinutes,
    int TodayQuestions, int DailyQuestionGoal,
    IReadOnlyList<ContinueLearningDto> ContinueLearning, IReadOnlyList<LearningPathListItemDto> RecommendedPaths,
    IReadOnlyList<RecentQuestionDto> RecentQuestions, ChallengeListItemDto? DailyChallenge,
    IReadOnlyList<InterviewReminderDto> UpcomingReminders, int UnreadNotifications);

public sealed record ActivityDayDto(DateOnly Date, int Questions, int Correct, int Quizzes, int Challenges, int Lessons, int Minutes, int Xp);

public sealed record SkillProgressDto(Guid TechnologyId, string Technology, int Attempted, int Correct, decimal Accuracy, string Proficiency);

// ---------------- Analytics ----------------
public sealed record ScoreComponentsDto(int Questions, int Coding, int MockInterviews, int Learning);
public sealed record DifficultyPerformanceDto(string Difficulty, int Attempted, int Correct, decimal Accuracy);
public sealed record QuizStatsDto(int QuizzesTaken, decimal AveragePercentage, decimal PassRate);
public sealed record CodingStatsDto(int ChallengesAttempted, int ChallengesSolved, decimal SuccessRate, int Submissions);
public sealed record ScorePointDto(DateTime Date, int Score);
public sealed record MockStatsDto(int Completed, int AverageOverall, int AverageTechnical, int AverageCommunication, int AverageProblemSolving, IReadOnlyList<ScorePointDto> Trend);
public sealed record RecommendationDto(string Title, string Reason, string ActionType, Guid? TargetId);

public sealed record AnalyticsOverviewDto(
    int PreparationScore, ScoreComponentsDto Components, int TotalAttempted, decimal Accuracy,
    IReadOnlyList<DifficultyPerformanceDto> DifficultyBreakdown, QuizStatsDto Quizzes, CodingStatsDto Coding,
    MockStatsDto MockInterviews, int LearningMinutes, IReadOnlyList<TopicPerformanceDto> WeakTopics,
    IReadOnlyList<TopicPerformanceDto> StrongTopics, IReadOnlyList<RecommendationDto> Recommendations);

public sealed record CategoryPerformanceDto(Guid CategoryId, string Category, int Attempted, int Correct, decimal Accuracy);
public sealed record TechnologyAnalyticsDto(Guid TechnologyId, string Technology, int Attempted, int Correct, decimal Accuracy, IReadOnlyList<CategoryPerformanceDto> Categories);
public sealed record PeriodActivityDto(DateOnly PeriodStart, int Questions, int Correct, int Minutes, int Xp, int Challenges, int Quizzes);

// ---------------- Gamification ----------------
public sealed record AchievementDto(string Code, string Name, string Description, string IconKey, int XpReward, bool Earned, DateTime? AwardedAt);
public sealed record XpTransactionDto(int Amount, string SourceType, string Reason, DateTime CreatedAt);
public sealed record WeeklyGoalDto(int TargetMinutes, int CurrentMinutes, bool Achieved, DateOnly WeekStart);
public sealed record GamificationProfileDto(
    LevelDto Level, int CurrentStreak, int LongestStreak, WeeklyGoalDto WeeklyGoal, IReadOnlyList<AchievementDto> Achievements,
    IReadOnlyList<XpTransactionDto> RecentXp);
public sealed record LeaderboardEntryDto(int Rank, Guid UserId, string DisplayName, string? ProfilePhotoUrl, int Xp, int Level, bool IsCurrentUser);
public sealed record LeaderboardDto(string Period, IReadOnlyList<LeaderboardEntryDto> Entries, LeaderboardEntryDto? CurrentUser);

// ---------------- Notifications ----------------
public sealed record NotificationDto(Guid Id, string Category, string Title, string Body, string? DeepLink, DateTime CreatedAt, DateTime? ReadAt);
public sealed record NotificationPreferenceDto(string Category, bool Enabled, int PreferredHourLocal);
public sealed record UpdateNotificationPreferencesRequest(IReadOnlyList<NotificationPreferenceDto> Preferences);

// ---------------- Subscriptions ----------------
public sealed record SubscriptionPlanDto(
    Guid Id, string Code, string Name, string Description, string Tier, long PriceMicros, string Currency,
    int BillingPeriodMonths, string? GooglePlayProductId, string? GooglePlayBasePlanId, IReadOnlyList<string> Features);

public sealed record UsageLimitDto(string Feature, int Used, int? Limit);

public sealed record SubscriptionStatusDto(
    string Tier, string? Status, string? PlanName, DateTime? ExpiresAt, bool AutoRenewing, string? Provider,
    IReadOnlyList<UsageLimitDto> UsageToday);

public sealed record PurchaseHistoryItemDto(Guid Id, string PlanName, string Status, string Provider, DateTime StartsAt, DateTime ExpiresAt, string? OrderId);

public sealed record VerifyGooglePlayPurchaseRequest(string ProductId, string PurchaseToken);

// ---------------- Offline sync ----------------
public sealed record BookmarkChangeDto(Guid QuestionId, bool IsBookmarked, DateTime ClientUpdatedAt);
public sealed record NoteChangeDto(Guid QuestionId, string Content, bool IsDeleted, DateTime ClientUpdatedAt);
public sealed record SyncPushRequest(
    IReadOnlyList<BookmarkChangeDto>? Bookmarks, IReadOnlyList<NoteChangeDto>? Notes, IReadOnlyList<ReviewFlashcardRequest>? FlashcardReviews);
public sealed record SyncConflictDto(string EntityType, Guid EntityId, string Resolution);
public sealed record SyncPushResponse(int AppliedBookmarks, int AppliedNotes, int AppliedReviews, IReadOnlyList<SyncConflictDto> Conflicts, DateTime ServerTime);
public sealed record SyncPullResponse(IReadOnlyList<BookmarkStateDto> Bookmarks, IReadOnlyList<NoteStateDto> Notes, DateTime ServerTime);
