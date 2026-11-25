using DevInterview.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevInterview.Infrastructure.Persistence.Configurations;

internal sealed class LearningPathConfiguration : IEntityTypeConfiguration<LearningPath>
{
    public void Configure(EntityTypeBuilder<LearningPath> b)
    {
        b.ToTable("LearningPaths");
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.Slug).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(4000);
        b.Property(x => x.ExternalKey).HasMaxLength(120);
        b.HasIndex(x => x.Slug).IsUnique();
        b.HasIndex(x => new { x.IsPublished, x.IsActive, x.DisplayOrder });
        b.HasOne(x => x.Technology).WithMany().HasForeignKey(x => x.TechnologyId).OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class LearningModuleConfiguration : IEntityTypeConfiguration<LearningModule>
{
    public void Configure(EntityTypeBuilder<LearningModule> b)
    {
        b.ToTable("LearningModules");
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(2000);
        b.HasIndex(x => new { x.PathId, x.DisplayOrder });
        b.HasOne(x => x.Path).WithMany(x => x.Modules).HasForeignKey(x => x.PathId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class LearningLessonConfiguration : IEntityTypeConfiguration<LearningLesson>
{
    public void Configure(EntityTypeBuilder<LearningLesson> b)
    {
        b.ToTable("LearningLessons");
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.Content).Unbounded();
        b.Property(x => x.ExerciseInstructions).Unbounded();
        b.Property(x => x.Type).AsString();
        b.HasIndex(x => new { x.ModuleId, x.DisplayOrder });
        b.HasOne(x => x.Module).WithMany(x => x.Lessons).HasForeignKey(x => x.ModuleId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Quiz).WithMany().HasForeignKey(x => x.QuizId).OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class LessonQuestionConfiguration : IEntityTypeConfiguration<LessonQuestion>
{
    public void Configure(EntityTypeBuilder<LessonQuestion> b)
    {
        b.ToTable("LessonQuestions");
        b.HasKey(x => new { x.LessonId, x.QuestionId });
        b.HasOne(x => x.Lesson).WithMany(x => x.Questions).HasForeignKey(x => x.LessonId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Question).WithMany().HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class UserLearningProgressConfiguration : IEntityTypeConfiguration<UserLearningProgress>
{
    public void Configure(EntityTypeBuilder<UserLearningProgress> b)
    {
        b.ToTable("UserLearningProgress");
        b.Property(x => x.Status).AsString();
        b.HasIndex(x => new { x.UserId, x.LessonId }).IsUnique();
        b.HasIndex(x => new { x.UserId, x.PathId, x.Status });
        b.HasOne(x => x.Lesson).WithMany().HasForeignKey(x => x.LessonId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
    }
}

internal sealed class FlashcardConfiguration : IEntityTypeConfiguration<Flashcard>
{
    public void Configure(EntityTypeBuilder<Flashcard> b)
    {
        b.ToTable("Flashcards");
        b.Property(x => x.Front).HasMaxLength(1000).IsRequired();
        b.Property(x => x.Back).HasMaxLength(4000).IsRequired();
        b.Property(x => x.ExternalKey).HasMaxLength(120);
        b.HasIndex(x => x.ExternalKey).IsUnique().HasFilter("[ExternalKey] IS NOT NULL");
        b.HasIndex(x => new { x.IsPublished, x.IsActive, x.TechnologyId });
        b.HasOne(x => x.Technology).WithMany().HasForeignKey(x => x.TechnologyId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.NoAction);
    }
}

internal sealed class FlashcardReviewConfiguration : IEntityTypeConfiguration<FlashcardReview>
{
    public void Configure(EntityTypeBuilder<FlashcardReview> b)
    {
        b.ToTable("FlashcardReviews");
        b.Property(x => x.LastGrade).HasConversion<string>().HasMaxLength(20);
        b.HasIndex(x => new { x.UserId, x.FlashcardId }).IsUnique();
        b.HasIndex(x => new { x.UserId, x.DueAt });
        b.HasOne(x => x.Flashcard).WithMany().HasForeignKey(x => x.FlashcardId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
    }
}

internal sealed class FlashcardReviewLogConfiguration : IEntityTypeConfiguration<FlashcardReviewLog>
{
    public void Configure(EntityTypeBuilder<FlashcardReviewLog> b)
    {
        b.ToTable("FlashcardReviewLogs");
        b.Property(x => x.Grade).AsString();
        b.HasIndex(x => new { x.UserId, x.ClientReviewId }).IsUnique();
        b.HasIndex(x => new { x.UserId, x.FlashcardId });
    }
}

internal sealed class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> b)
    {
        b.ToTable("Companies");
        b.Property(x => x.Name).HasMaxLength(120).IsRequired();
        b.Property(x => x.Slug).HasMaxLength(120).IsRequired();
        b.Property(x => x.Overview).Unbounded();
        b.Property(x => x.Industry).HasMaxLength(100);
        b.Property(x => x.LogoUrl).HasMaxLength(500);
        b.Property(x => x.WebsiteUrl).HasMaxLength(300);
        b.Property(x => x.OverviewSource).AsString();
        b.HasIndex(x => x.Slug).IsUnique();
    }
}

internal sealed class CompanyInterviewRoundConfiguration : IEntityTypeConfiguration<CompanyInterviewRound>
{
    public void Configure(EntityTypeBuilder<CompanyInterviewRound> b)
    {
        b.ToTable("CompanyInterviewRounds");
        b.Property(x => x.Name).HasMaxLength(120);
        b.Property(x => x.Description).HasMaxLength(4000);
        b.Property(x => x.Source).AsString();
        b.Property(x => x.SourceReference).HasMaxLength(500);
        b.HasOne(x => x.Company).WithMany(x => x.Rounds).HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class CompanyTopicConfiguration : IEntityTypeConfiguration<CompanyTopic>
{
    public void Configure(EntityTypeBuilder<CompanyTopic> b)
    {
        b.ToTable("CompanyTopics");
        b.Property(x => x.Name).HasMaxLength(120);
        b.Property(x => x.Description).HasMaxLength(1000);
        b.HasOne(x => x.Company).WithMany(x => x.Topics).HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Technology).WithMany().HasForeignKey(x => x.TechnologyId).OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class CompanyQuestionCollectionConfiguration : IEntityTypeConfiguration<CompanyQuestionCollection>
{
    public void Configure(EntityTypeBuilder<CompanyQuestionCollection> b)
    {
        b.ToTable("CompanyQuestionCollections");
        b.Property(x => x.Title).HasMaxLength(200);
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.Kind).AsString();
        b.Property(x => x.Source).AsString();
        b.Property(x => x.SourceReference).HasMaxLength(500);
        b.HasOne(x => x.Company).WithMany(x => x.Collections).HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class CompanyCollectionQuestionConfiguration : IEntityTypeConfiguration<CompanyCollectionQuestion>
{
    public void Configure(EntityTypeBuilder<CompanyCollectionQuestion> b)
    {
        b.ToTable("CompanyCollectionQuestions");
        b.HasKey(x => new { x.CollectionId, x.QuestionId });
        b.HasOne(x => x.Collection).WithMany(x => x.Questions).HasForeignKey(x => x.CollectionId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Question).WithMany().HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class CompanyCollectionChallengeConfiguration : IEntityTypeConfiguration<CompanyCollectionChallenge>
{
    public void Configure(EntityTypeBuilder<CompanyCollectionChallenge> b)
    {
        b.ToTable("CompanyCollectionChallenges");
        b.HasKey(x => new { x.CollectionId, x.ChallengeId });
        b.HasOne(x => x.Collection).WithMany(x => x.Challenges).HasForeignKey(x => x.CollectionId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Challenge).WithMany().HasForeignKey(x => x.ChallengeId).OnDelete(DeleteBehavior.Cascade);
    }
}
