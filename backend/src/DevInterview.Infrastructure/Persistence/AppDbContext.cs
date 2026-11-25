using DevInterview.Application.Abstractions;
using DevInterview.Domain.Common;
using DevInterview.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DevInterview.Infrastructure.Persistence;

public class AppDbContext : IdentityDbContext<AppUser, AppRole, Guid, IdentityUserClaim<Guid>, AppUserRole,
    IdentityUserLogin<Guid>, IdentityRoleClaim<Guid>, IdentityUserToken<Guid>>, IAppDbContext
{
    private readonly IClock _clock;

    public AppDbContext(DbContextOptions<AppDbContext> options, IClock clock) : base(options) => _clock = clock;

    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<UserPreferredTechnology> UserPreferredTechnologies => Set<UserPreferredTechnology>();
    public DbSet<UserPreference> UserPreferences => Set<UserPreference>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<DeviceToken> DeviceTokens => Set<DeviceToken>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<InterviewReminder> InterviewReminders => Set<InterviewReminder>();
    public DbSet<Technology> Technologies => Set<Technology>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Topic> Topics => Set<Topic>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<QuestionOption> QuestionOptions => Set<QuestionOption>();
    public DbSet<QuestionAnswer> QuestionAnswers => Set<QuestionAnswer>();
    public DbSet<QuestionTag> QuestionTags => Set<QuestionTag>();
    public DbSet<QuestionBookmark> QuestionBookmarks => Set<QuestionBookmark>();
    public DbSet<QuestionNote> QuestionNotes => Set<QuestionNote>();
    public DbSet<QuestionReport> QuestionReports => Set<QuestionReport>();
    public DbSet<QuestionAttempt> QuestionAttempts => Set<QuestionAttempt>();
    public DbSet<SavedCollection> SavedCollections => Set<SavedCollection>();
    public DbSet<SavedCollectionItem> SavedCollectionItems => Set<SavedCollectionItem>();
    public DbSet<Quiz> Quizzes => Set<Quiz>();
    public DbSet<QuizQuestion> QuizQuestions => Set<QuizQuestion>();
    public DbSet<QuizAttempt> QuizAttempts => Set<QuizAttempt>();
    public DbSet<QuizAttemptAnswer> QuizAttemptAnswers => Set<QuizAttemptAnswer>();
    public DbSet<SupportedLanguage> SupportedLanguages => Set<SupportedLanguage>();
    public DbSet<CodingChallenge> CodingChallenges => Set<CodingChallenge>();
    public DbSet<ChallengeStarterCode> ChallengeStarterCodes => Set<ChallengeStarterCode>();
    public DbSet<ChallengeTestCase> ChallengeTestCases => Set<ChallengeTestCase>();
    public DbSet<CodeSubmission> CodeSubmissions => Set<CodeSubmission>();
    public DbSet<CodeSubmissionResult> CodeSubmissionResults => Set<CodeSubmissionResult>();
    public DbSet<MockInterview> MockInterviews => Set<MockInterview>();
    public DbSet<MockInterviewQuestion> MockInterviewQuestions => Set<MockInterviewQuestion>();
    public DbSet<MockInterviewAnswer> MockInterviewAnswers => Set<MockInterviewAnswer>();
    public DbSet<MockInterviewEvaluation> MockInterviewEvaluations => Set<MockInterviewEvaluation>();
    public DbSet<AnswerEvaluation> AnswerEvaluations => Set<AnswerEvaluation>();
    public DbSet<CodeReview> CodeReviews => Set<CodeReview>();
    public DbSet<AiUsageLog> AiUsageLogs => Set<AiUsageLog>();
    public DbSet<AiPromptTemplate> AiPromptTemplates => Set<AiPromptTemplate>();
    public DbSet<AiProviderSetting> AiProviderSettings => Set<AiProviderSetting>();
    public DbSet<LearningPath> LearningPaths => Set<LearningPath>();
    public DbSet<LearningModule> LearningModules => Set<LearningModule>();
    public DbSet<LearningLesson> LearningLessons => Set<LearningLesson>();
    public DbSet<LessonQuestion> LessonQuestions => Set<LessonQuestion>();
    public DbSet<UserLearningProgress> UserLearningProgress => Set<UserLearningProgress>();
    public DbSet<Flashcard> Flashcards => Set<Flashcard>();
    public DbSet<FlashcardReview> FlashcardReviews => Set<FlashcardReview>();
    public DbSet<FlashcardReviewLog> FlashcardReviewLogs => Set<FlashcardReviewLog>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<CompanyInterviewRound> CompanyInterviewRounds => Set<CompanyInterviewRound>();
    public DbSet<CompanyTopic> CompanyTopics => Set<CompanyTopic>();
    public DbSet<CompanyQuestionCollection> CompanyQuestionCollections => Set<CompanyQuestionCollection>();
    public DbSet<CompanyCollectionQuestion> CompanyCollectionQuestions => Set<CompanyCollectionQuestion>();
    public DbSet<CompanyCollectionChallenge> CompanyCollectionChallenges => Set<CompanyCollectionChallenge>();
    public DbSet<UserActivityLog> UserActivityLogs => Set<UserActivityLog>();
    public DbSet<UserPerformanceStat> UserPerformanceStats => Set<UserPerformanceStat>();
    public DbSet<Achievement> Achievements => Set<Achievement>();
    public DbSet<UserAchievement> UserAchievements => Set<UserAchievement>();
    public DbSet<UserStreak> UserStreaks => Set<UserStreak>();
    public DbSet<UserXpTransaction> UserXpTransactions => Set<UserXpTransaction>();
    public DbSet<UsageCounter> UsageCounters => Set<UsageCounter>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<UserSubscription> UserSubscriptions => Set<UserSubscription>();

    DbSet<AppUser> IAppDbContext.Users => Users;
    DbSet<AppRole> IAppDbContext.Roles => Roles;
    DbSet<AppUserRole> IAppDbContext.UserRoles => UserRoles;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Ids are generated client-side (Guid.NewGuid() in BaseEntity). Marking them as never database-generated makes EF
        // treat new entities discovered through navigations as Added rather than existing rows.
        foreach (var entity in builder.Model.GetEntityTypes().Where(e => typeof(BaseEntity).IsAssignableFrom(e.ClrType)))
            builder.Entity(entity.ClrType).Property(nameof(BaseEntity.Id)).ValueGeneratedNever();
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<decimal>().HavePrecision(18, 4);
        configurationBuilder.Properties<string>().HaveMaxLength(4000);
        // Integration tests run on SQLite, which cannot aggregate or order by decimal; store as REAL there.
        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
            configurationBuilder.Properties<decimal>().HaveConversion<double>();
    }

    public void ResetTracking() => ChangeTracker.Clear();

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampEntities();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        StampEntities();
        return base.SaveChanges();
    }

    private void StampEntities()
    {
        var now = _clock.UtcNow;
        foreach (var entry in ChangeTracker.Entries())
        {
            switch (entry.Entity)
            {
                case BaseEntity e when entry.State == EntityState.Added:
                    if (e.CreatedAt == default) e.CreatedAt = now;
                    e.UpdatedAt = now;
                    break;
                case BaseEntity e when entry.State == EntityState.Modified:
                    e.UpdatedAt = now;
                    break;
                case AppUser u when entry.State == EntityState.Added:
                    if (u.CreatedAt == default) u.CreatedAt = now;
                    u.UpdatedAt = now;
                    break;
                case AppUser u when entry.State == EntityState.Modified:
                    u.UpdatedAt = now;
                    break;
            }
        }
    }
}
