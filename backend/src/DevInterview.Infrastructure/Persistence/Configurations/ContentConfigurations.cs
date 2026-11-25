using DevInterview.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevInterview.Infrastructure.Persistence.Configurations;

internal sealed class TechnologyConfiguration : IEntityTypeConfiguration<Technology>
{
    public void Configure(EntityTypeBuilder<Technology> b)
    {
        b.ToTable("Technologies");
        b.Property(x => x.Name).HasMaxLength(80).IsRequired();
        b.Property(x => x.Slug).HasMaxLength(80).IsRequired();
        b.Property(x => x.Group).HasMaxLength(60);
        b.Property(x => x.IconKey).HasMaxLength(40);
        b.Property(x => x.Description).HasMaxLength(1000);
        b.HasIndex(x => x.Slug).IsUnique();
        b.HasIndex(x => new { x.IsActive, x.DisplayOrder });
    }
}

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> b)
    {
        b.ToTable("Categories");
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.Slug).HasMaxLength(100).IsRequired();
        b.Property(x => x.Description).HasMaxLength(1000);
        b.HasIndex(x => new { x.TechnologyId, x.Slug }).IsUnique();
        b.HasOne(x => x.Technology).WithMany(x => x.Categories).HasForeignKey(x => x.TechnologyId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class TopicConfiguration : IEntityTypeConfiguration<Topic>
{
    public void Configure(EntityTypeBuilder<Topic> b)
    {
        b.ToTable("Topics");
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.Slug).HasMaxLength(100).IsRequired();
        b.HasIndex(x => new { x.CategoryId, x.Slug }).IsUnique();
        b.HasOne(x => x.Category).WithMany(x => x.Topics).HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> b)
    {
        b.ToTable("Tags");
        b.Property(x => x.Name).HasMaxLength(50).IsRequired();
        b.Property(x => x.Slug).HasMaxLength(60).IsRequired();
        b.HasIndex(x => x.Slug).IsUnique();
    }
}

internal sealed class QuestionConfiguration : IEntityTypeConfiguration<Question>
{
    public void Configure(EntityTypeBuilder<Question> b)
    {
        b.ToTable("Questions");
        b.Property(x => x.Title).HasMaxLength(300).IsRequired();
        b.Property(x => x.Body).Unbounded().IsRequired();
        b.Property(x => x.CodeSnippet).Unbounded();
        b.Property(x => x.CodeLanguage).HasMaxLength(30);
        b.Property(x => x.Type).AsString();
        b.Property(x => x.Status).AsString();
        b.Property(x => x.ExternalKey).HasMaxLength(120);
        b.HasIndex(x => x.ExternalKey).IsUnique().HasFilter("[ExternalKey] IS NOT NULL");
        // Covering index for the main listing filter (published, by technology/category/difficulty).
        b.HasIndex(x => new { x.Status, x.IsActive, x.IsDeleted, x.TechnologyId, x.CategoryId, x.Difficulty });
        b.HasIndex(x => new { x.CategoryId, x.Difficulty, x.Title });
        b.HasIndex(x => x.Title);
        b.Ignore(x => x.IsObjective);
        b.HasOne(x => x.Technology).WithMany().HasForeignKey(x => x.TechnologyId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Topic).WithMany().HasForeignKey(x => x.TopicId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Answer).WithOne(x => x.Question).HasForeignKey<QuestionAnswer>(x => x.QuestionId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class QuestionOptionConfiguration : IEntityTypeConfiguration<QuestionOption>
{
    public void Configure(EntityTypeBuilder<QuestionOption> b)
    {
        b.ToTable("QuestionOptions");
        b.Property(x => x.Text).HasMaxLength(1000).IsRequired();
        b.Property(x => x.Explanation).HasMaxLength(2000);
        b.HasIndex(x => new { x.QuestionId, x.DisplayOrder });
        b.HasOne(x => x.Question).WithMany(x => x.Options).HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class QuestionAnswerConfiguration : IEntityTypeConfiguration<QuestionAnswer>
{
    public void Configure(EntityTypeBuilder<QuestionAnswer> b)
    {
        b.ToTable("QuestionAnswers");
        b.HasIndex(x => x.QuestionId).IsUnique();
        b.Property(x => x.ShortAnswer).HasMaxLength(4000);
        b.Property(x => x.DetailedExplanation).Unbounded();
        b.Property(x => x.ReferenceAnswer).Unbounded();
    }
}

internal sealed class QuestionTagConfiguration : IEntityTypeConfiguration<QuestionTag>
{
    public void Configure(EntityTypeBuilder<QuestionTag> b)
    {
        b.ToTable("QuestionTags");
        b.HasKey(x => new { x.QuestionId, x.TagId });
        b.HasIndex(x => x.TagId);
        b.HasOne(x => x.Question).WithMany(x => x.Tags).HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Tag).WithMany().HasForeignKey(x => x.TagId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class QuestionBookmarkConfiguration : IEntityTypeConfiguration<QuestionBookmark>
{
    public void Configure(EntityTypeBuilder<QuestionBookmark> b)
    {
        b.ToTable("QuestionBookmarks");
        b.HasIndex(x => new { x.UserId, x.QuestionId }).IsUnique();
        b.HasIndex(x => new { x.UserId, x.UpdatedAt });
        b.HasOne(x => x.Question).WithMany().HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class QuestionNoteConfiguration : IEntityTypeConfiguration<QuestionNote>
{
    public void Configure(EntityTypeBuilder<QuestionNote> b)
    {
        b.ToTable("QuestionNotes");
        b.Property(x => x.Content).Unbounded();
        b.HasIndex(x => new { x.UserId, x.QuestionId }).IsUnique();
        b.HasIndex(x => new { x.UserId, x.UpdatedAt });
        b.HasOne(x => x.Question).WithMany().HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class QuestionReportConfiguration : IEntityTypeConfiguration<QuestionReport>
{
    public void Configure(EntityTypeBuilder<QuestionReport> b)
    {
        b.ToTable("QuestionReports");
        b.Property(x => x.Reason).HasMaxLength(100).IsRequired();
        b.Property(x => x.Details).HasMaxLength(2000);
        b.Property(x => x.ResolutionNote).HasMaxLength(1000);
        b.Property(x => x.Status).AsString();
        b.HasIndex(x => new { x.Status, x.CreatedAt });
        b.HasIndex(x => new { x.UserId, x.QuestionId });
        b.HasOne(x => x.Question).WithMany().HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class QuestionAttemptConfiguration : IEntityTypeConfiguration<QuestionAttempt>
{
    public void Configure(EntityTypeBuilder<QuestionAttempt> b)
    {
        b.ToTable("QuestionAttempts");
        b.Property(x => x.SelectedOptionIds).HasMaxLength(400);
        b.HasIndex(x => new { x.UserId, x.QuestionId, x.CreatedAt });
        b.HasIndex(x => new { x.UserId, x.CreatedAt });
        b.HasIndex(x => new { x.UserId, x.CategoryId });
        b.HasIndex(x => new { x.UserId, x.TechnologyId });
        b.HasIndex(x => x.QuestionId);
        b.HasOne(x => x.Question).WithMany().HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
    }
}

internal sealed class SavedCollectionConfiguration : IEntityTypeConfiguration<SavedCollection>
{
    public void Configure(EntityTypeBuilder<SavedCollection> b)
    {
        b.ToTable("SavedCollections");
        b.Property(x => x.Name).HasMaxLength(80).IsRequired();
        b.Property(x => x.Description).HasMaxLength(400);
        b.HasIndex(x => new { x.UserId, x.Name }).IsUnique();
        b.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class SavedCollectionItemConfiguration : IEntityTypeConfiguration<SavedCollectionItem>
{
    public void Configure(EntityTypeBuilder<SavedCollectionItem> b)
    {
        b.ToTable("SavedCollectionItems");
        b.Property(x => x.ItemType).AsString();
        b.HasIndex(x => new { x.CollectionId, x.ItemType, x.ItemId }).IsUnique();
        b.HasOne(x => x.Collection).WithMany(x => x.Items).HasForeignKey(x => x.CollectionId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class QuizConfiguration : IEntityTypeConfiguration<Quiz>
{
    public void Configure(EntityTypeBuilder<Quiz> b)
    {
        b.ToTable("Quizzes");
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(1000);
        b.Property(x => x.Mode).AsString();
        b.HasIndex(x => new { x.IsSystem, x.IsPublished, x.IsDeleted });
        b.HasIndex(x => x.OwnerUserId);
        b.HasIndex(x => x.DailyDate).IsUnique().HasFilter("[DailyDate] IS NOT NULL");
        b.HasOne(x => x.Technology).WithMany().HasForeignKey(x => x.TechnologyId).OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class QuizQuestionConfiguration : IEntityTypeConfiguration<QuizQuestion>
{
    public void Configure(EntityTypeBuilder<QuizQuestion> b)
    {
        b.ToTable("QuizQuestions");
        b.HasIndex(x => new { x.QuizId, x.QuestionId }).IsUnique();
        b.HasOne(x => x.Quiz).WithMany(x => x.Questions).HasForeignKey(x => x.QuizId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Question).WithMany().HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class QuizAttemptConfiguration : IEntityTypeConfiguration<QuizAttempt>
{
    public void Configure(EntityTypeBuilder<QuizAttempt> b)
    {
        b.ToTable("QuizAttempts");
        b.Property(x => x.Status).AsString();
        b.Property(x => x.QuestionOrderJson).Unbounded();
        b.HasIndex(x => new { x.UserId, x.QuizId, x.Status });
        b.HasIndex(x => new { x.Status, x.ExpiresAt });
        b.HasIndex(x => new { x.UserId, x.StartedAt });
        b.HasOne(x => x.Quiz).WithMany().HasForeignKey(x => x.QuizId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
    }
}

internal sealed class QuizAttemptAnswerConfiguration : IEntityTypeConfiguration<QuizAttemptAnswer>
{
    public void Configure(EntityTypeBuilder<QuizAttemptAnswer> b)
    {
        b.ToTable("QuizAttemptAnswers");
        b.Property(x => x.SelectedOptionIds).HasMaxLength(400);
        b.HasIndex(x => new { x.AttemptId, x.QuestionId }).IsUnique();
        b.HasOne(x => x.Attempt).WithMany(x => x.Answers).HasForeignKey(x => x.AttemptId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Question).WithMany().HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.NoAction);
    }
}
