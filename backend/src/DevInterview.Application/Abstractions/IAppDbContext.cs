using DevInterview.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace DevInterview.Application.Abstractions;

/// <summary>Unit of work over the relational store. Implemented by Infrastructure's AppDbContext.</summary>
public interface IAppDbContext
{
    DbSet<AppUser> Users { get; }
    DbSet<AppRole> Roles { get; }
    DbSet<AppUserRole> UserRoles { get; }
    DbSet<UserProfile> UserProfiles { get; }
    DbSet<UserPreferredTechnology> UserPreferredTechnologies { get; }
    DbSet<UserPreference> UserPreferences { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<DeviceToken> DeviceTokens { get; }
    DbSet<NotificationPreference> NotificationPreferences { get; }
    DbSet<InterviewReminder> InterviewReminders { get; }

    DbSet<Technology> Technologies { get; }
    DbSet<Category> Categories { get; }
    DbSet<Topic> Topics { get; }
    DbSet<Tag> Tags { get; }
    DbSet<Question> Questions { get; }
    DbSet<QuestionOption> QuestionOptions { get; }
    DbSet<QuestionAnswer> QuestionAnswers { get; }
    DbSet<QuestionTag> QuestionTags { get; }
    DbSet<QuestionBookmark> QuestionBookmarks { get; }
    DbSet<QuestionNote> QuestionNotes { get; }
    DbSet<QuestionReport> QuestionReports { get; }
    DbSet<QuestionAttempt> QuestionAttempts { get; }
    DbSet<SavedCollection> SavedCollections { get; }
    DbSet<SavedCollectionItem> SavedCollectionItems { get; }

    DbSet<Quiz> Quizzes { get; }
    DbSet<QuizQuestion> QuizQuestions { get; }
    DbSet<QuizAttempt> QuizAttempts { get; }
    DbSet<QuizAttemptAnswer> QuizAttemptAnswers { get; }

    DbSet<SupportedLanguage> SupportedLanguages { get; }
    DbSet<CodingChallenge> CodingChallenges { get; }
    DbSet<ChallengeStarterCode> ChallengeStarterCodes { get; }
    DbSet<ChallengeTestCase> ChallengeTestCases { get; }
    DbSet<CodeSubmission> CodeSubmissions { get; }
    DbSet<CodeSubmissionResult> CodeSubmissionResults { get; }

    DbSet<MockInterview> MockInterviews { get; }
    DbSet<MockInterviewQuestion> MockInterviewQuestions { get; }
    DbSet<MockInterviewAnswer> MockInterviewAnswers { get; }
    DbSet<MockInterviewEvaluation> MockInterviewEvaluations { get; }
    DbSet<AnswerEvaluation> AnswerEvaluations { get; }
    DbSet<CodeReview> CodeReviews { get; }
    DbSet<AiUsageLog> AiUsageLogs { get; }
    DbSet<AiPromptTemplate> AiPromptTemplates { get; }
    DbSet<AiProviderSetting> AiProviderSettings { get; }

    DbSet<LearningPath> LearningPaths { get; }
    DbSet<LearningModule> LearningModules { get; }
    DbSet<LearningLesson> LearningLessons { get; }
    DbSet<LessonQuestion> LessonQuestions { get; }
    DbSet<UserLearningProgress> UserLearningProgress { get; }
    DbSet<Flashcard> Flashcards { get; }
    DbSet<FlashcardReview> FlashcardReviews { get; }
    DbSet<FlashcardReviewLog> FlashcardReviewLogs { get; }

    DbSet<Company> Companies { get; }
    DbSet<CompanyInterviewRound> CompanyInterviewRounds { get; }
    DbSet<CompanyTopic> CompanyTopics { get; }
    DbSet<CompanyQuestionCollection> CompanyQuestionCollections { get; }
    DbSet<CompanyCollectionQuestion> CompanyCollectionQuestions { get; }
    DbSet<CompanyCollectionChallenge> CompanyCollectionChallenges { get; }

    DbSet<UserActivityLog> UserActivityLogs { get; }
    DbSet<UserPerformanceStat> UserPerformanceStats { get; }
    DbSet<Achievement> Achievements { get; }
    DbSet<UserAchievement> UserAchievements { get; }
    DbSet<UserStreak> UserStreaks { get; }
    DbSet<UserXpTransaction> UserXpTransactions { get; }
    DbSet<UsageCounter> UsageCounters { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<AppSetting> AppSettings { get; }
    DbSet<SubscriptionPlan> SubscriptionPlans { get; }
    DbSet<UserSubscription> UserSubscriptions { get; }

    DatabaseFacade Database { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    /// <summary>Detaches all tracked entries (used to recover after a failed save).</summary>
    void ResetTracking();
}
