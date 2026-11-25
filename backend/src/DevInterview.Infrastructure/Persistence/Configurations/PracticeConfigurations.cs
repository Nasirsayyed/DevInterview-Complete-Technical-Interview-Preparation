using DevInterview.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevInterview.Infrastructure.Persistence.Configurations;

internal sealed class SupportedLanguageConfiguration : IEntityTypeConfiguration<SupportedLanguage>
{
    public void Configure(EntityTypeBuilder<SupportedLanguage> b)
    {
        b.ToTable("SupportedLanguages");
        b.Property(x => x.Code).HasMaxLength(20).IsRequired();
        b.Property(x => x.DisplayName).HasMaxLength(40);
        b.Property(x => x.Version).HasMaxLength(40);
        b.Property(x => x.FileExtension).HasMaxLength(10);
        b.HasIndex(x => x.Code).IsUnique();
    }
}

internal sealed class CodingChallengeConfiguration : IEntityTypeConfiguration<CodingChallenge>
{
    public void Configure(EntityTypeBuilder<CodingChallenge> b)
    {
        b.ToTable("CodingChallenges");
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.Slug).HasMaxLength(200).IsRequired();
        b.Property(x => x.Category).AsString();
        b.Property(x => x.Status).AsString();
        b.Property(x => x.ProblemStatement).Unbounded();
        b.Property(x => x.InputDescription).Unbounded();
        b.Property(x => x.OutputDescription).Unbounded();
        b.Property(x => x.Constraints).Unbounded();
        b.Property(x => x.SolutionExplanation).Unbounded();
        b.Property(x => x.HintsJson).Unbounded();
        b.Property(x => x.ExpectedTimeComplexity).HasMaxLength(60);
        b.Property(x => x.ExpectedSpaceComplexity).HasMaxLength(60);
        b.Property(x => x.AllowedLanguages).HasMaxLength(200);
        b.Property(x => x.ExternalKey).HasMaxLength(120);
        b.HasIndex(x => x.Slug).IsUnique();
        b.HasIndex(x => x.ExternalKey).IsUnique().HasFilter("[ExternalKey] IS NOT NULL");
        b.HasIndex(x => new { x.Status, x.IsActive, x.IsDeleted, x.Difficulty, x.Category });
    }
}

internal sealed class ChallengeStarterCodeConfiguration : IEntityTypeConfiguration<ChallengeStarterCode>
{
    public void Configure(EntityTypeBuilder<ChallengeStarterCode> b)
    {
        b.ToTable("ChallengeStarterCodes");
        b.Property(x => x.LanguageCode).HasMaxLength(20);
        b.Property(x => x.Code).Unbounded();
        b.HasIndex(x => new { x.ChallengeId, x.LanguageCode }).IsUnique();
        b.HasOne(x => x.Challenge).WithMany(x => x.StarterCodes).HasForeignKey(x => x.ChallengeId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ChallengeTestCaseConfiguration : IEntityTypeConfiguration<ChallengeTestCase>
{
    public void Configure(EntityTypeBuilder<ChallengeTestCase> b)
    {
        b.ToTable("ChallengeTestCases");
        b.Property(x => x.Input).Unbounded();
        b.Property(x => x.ExpectedOutput).Unbounded();
        b.Property(x => x.Explanation).HasMaxLength(2000);
        b.HasIndex(x => new { x.ChallengeId, x.IsSample, x.DisplayOrder });
        b.HasOne(x => x.Challenge).WithMany(x => x.TestCases).HasForeignKey(x => x.ChallengeId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class CodeSubmissionConfiguration : IEntityTypeConfiguration<CodeSubmission>
{
    public void Configure(EntityTypeBuilder<CodeSubmission> b)
    {
        b.ToTable("CodeSubmissions");
        b.Property(x => x.LanguageCode).HasMaxLength(20);
        b.Property(x => x.SourceCode).Unbounded();
        b.Property(x => x.CompileOutput).Unbounded();
        b.Property(x => x.Kind).AsString();
        b.Property(x => x.Status).AsString();
        b.Property(x => x.IdempotencyKey).HasMaxLength(64);
        b.HasIndex(x => new { x.UserId, x.ChallengeId, x.Kind, x.Status });
        b.HasIndex(x => new { x.UserId, x.CreatedAt });
        b.HasIndex(x => new { x.ChallengeId, x.Kind, x.Status });
        b.HasIndex(x => new { x.UserId, x.IdempotencyKey }).IsUnique().HasFilter("[IdempotencyKey] IS NOT NULL");
        b.HasOne(x => x.Challenge).WithMany().HasForeignKey(x => x.ChallengeId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
    }
}

internal sealed class CodeSubmissionResultConfiguration : IEntityTypeConfiguration<CodeSubmissionResult>
{
    public void Configure(EntityTypeBuilder<CodeSubmissionResult> b)
    {
        b.ToTable("CodeSubmissionResults");
        b.Property(x => x.Status).AsString();
        b.Property(x => x.ActualOutput).Unbounded();
        b.Property(x => x.ErrorOutput).Unbounded();
        b.HasIndex(x => x.SubmissionId);
        b.HasIndex(x => x.TestCaseId);
        b.HasOne(x => x.Submission).WithMany(x => x.Results).HasForeignKey(x => x.SubmissionId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<ChallengeTestCase>().WithMany().HasForeignKey(x => x.TestCaseId).OnDelete(DeleteBehavior.NoAction);
    }
}

internal sealed class MockInterviewConfiguration : IEntityTypeConfiguration<MockInterview>
{
    public void Configure(EntityTypeBuilder<MockInterview> b)
    {
        b.ToTable("MockInterviews");
        b.Property(x => x.Type).AsString();
        b.Property(x => x.Status).AsString();
        b.Property(x => x.Technology).HasMaxLength(80);
        b.Property(x => x.TargetRole).HasMaxLength(80);
        b.Property(x => x.IntroMessage).HasMaxLength(2000);
        b.Ignore(x => x.MainQuestionsAsked);
        b.HasIndex(x => new { x.UserId, x.Status });
        b.HasIndex(x => new { x.UserId, x.StartedAt });
        b.HasOne(x => x.Evaluation).WithOne(x => x.Interview).HasForeignKey<MockInterviewEvaluation>(x => x.InterviewId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class MockInterviewQuestionConfiguration : IEntityTypeConfiguration<MockInterviewQuestion>
{
    public void Configure(EntityTypeBuilder<MockInterviewQuestion> b)
    {
        b.ToTable("MockInterviewQuestions");
        b.Property(x => x.Text).HasMaxLength(2000).IsRequired();
        b.HasIndex(x => new { x.InterviewId, x.Sequence }).IsUnique();
        b.HasOne(x => x.Interview).WithMany(x => x.Questions).HasForeignKey(x => x.InterviewId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Answer).WithOne(x => x.Question).HasForeignKey<MockInterviewAnswer>(x => x.QuestionId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class MockInterviewAnswerConfiguration : IEntityTypeConfiguration<MockInterviewAnswer>
{
    public void Configure(EntityTypeBuilder<MockInterviewAnswer> b)
    {
        b.ToTable("MockInterviewAnswers");
        b.Property(x => x.AnswerText).Unbounded();
        b.Property(x => x.Feedback).HasMaxLength(1000);
        b.HasIndex(x => x.QuestionId).IsUnique();
    }
}

internal sealed class MockInterviewEvaluationConfiguration : IEntityTypeConfiguration<MockInterviewEvaluation>
{
    public void Configure(EntityTypeBuilder<MockInterviewEvaluation> b)
    {
        b.ToTable("MockInterviewEvaluations");
        b.Property(x => x.Summary).HasMaxLength(4000);
        b.Property(x => x.ReportJson).Unbounded();
        b.HasIndex(x => x.InterviewId).IsUnique();
    }
}

internal sealed class AnswerEvaluationConfiguration : IEntityTypeConfiguration<AnswerEvaluation>
{
    public void Configure(EntityTypeBuilder<AnswerEvaluation> b)
    {
        b.ToTable("AnswerEvaluations");
        b.Property(x => x.QuestionText).HasMaxLength(4000);
        b.Property(x => x.UserAnswer).Unbounded();
        b.Property(x => x.ResultJson).Unbounded();
        b.HasIndex(x => new { x.UserId, x.CreatedAt });
        b.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class CodeReviewConfiguration : IEntityTypeConfiguration<CodeReview>
{
    public void Configure(EntityTypeBuilder<CodeReview> b)
    {
        b.ToTable("CodeReviews");
        b.Property(x => x.LanguageCode).HasMaxLength(20);
        b.Property(x => x.SourceCode).Unbounded();
        b.Property(x => x.ResultJson).Unbounded();
        b.HasIndex(x => new { x.UserId, x.CreatedAt });
        b.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class AiUsageLogConfiguration : IEntityTypeConfiguration<AiUsageLog>
{
    public void Configure(EntityTypeBuilder<AiUsageLog> b)
    {
        b.ToTable("AIUsageLogs");
        b.Property(x => x.Feature).AsString();
        b.Property(x => x.Provider).HasMaxLength(40);
        b.Property(x => x.Model).HasMaxLength(80);
        b.Property(x => x.ErrorCode).HasMaxLength(40);
        b.Property(x => x.EstimatedCostUsd).HasPrecision(18, 6);
        b.HasIndex(x => x.CreatedAt);
        b.HasIndex(x => new { x.UserId, x.CreatedAt });
    }
}

internal sealed class AiPromptTemplateConfiguration : IEntityTypeConfiguration<AiPromptTemplate>
{
    public void Configure(EntityTypeBuilder<AiPromptTemplate> b)
    {
        b.ToTable("AIPromptTemplates");
        b.Property(x => x.Key).HasMaxLength(60).IsRequired();
        b.Property(x => x.SystemPrompt).Unbounded();
        b.Property(x => x.Notes).HasMaxLength(1000);
        b.HasIndex(x => new { x.Key, x.Version }).IsUnique();
    }
}

internal sealed class AiProviderSettingConfiguration : IEntityTypeConfiguration<AiProviderSetting>
{
    public void Configure(EntityTypeBuilder<AiProviderSetting> b)
    {
        b.ToTable("AIProviderSettings");
        b.Property(x => x.ProviderName).HasMaxLength(40).IsRequired();
        b.Property(x => x.Model).HasMaxLength(80);
        b.HasIndex(x => x.ProviderName).IsUnique();
    }
}
