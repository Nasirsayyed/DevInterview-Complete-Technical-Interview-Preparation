using DevInterview.Application.Abstractions;
using DevInterview.Application.Common;
using DevInterview.Application.Features.Ai;
using DevInterview.Application.Features.Coding;
using DevInterview.Application.Features.Flashcards;
using DevInterview.Application.Features.Gamification;
using DevInterview.Application.Features.Library;
using DevInterview.Application.Features.MockInterviews;
using DevInterview.Application.Features.Quizzes;
using DevInterview.Application.Features.Subscriptions;
using DevInterview.Contracts.Ai;
using DevInterview.Contracts.Engagement;
using DevInterview.Contracts.Learning;
using DevInterview.Contracts.Practice;
using DevInterview.Domain.Entities;
using DevInterview.Domain.Enums;
using DevInterview.Infrastructure.CodeExecution;
using DevInterview.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevInterview.UnitTests.Application;

public class EntitlementServiceTests : IDisposable
{
    private readonly TestDb _db = new();
    public void Dispose() => _db.Dispose();
    private EntitlementService Create() => new(_db.Create(), _db.Clock, TestDb.Options(Limits.Default));

    [Fact]
    public async Task Free_users_hit_daily_limit_then_reset_next_day()
    {
        var (user, _, _) = await _db.SeedBasicsAsync(1);
        for (var i = 0; i < 3; i++) await Create().ConsumeAsync(user, UsageFeature.QuestionView, default);
        await Assert.ThrowsAsync<QuotaExceededException>(() => Create().ConsumeAsync(user, UsageFeature.QuestionView, default));
        _db.Clock.Advance(TimeSpan.FromDays(1));
        await Create().ConsumeAsync(user, UsageFeature.QuestionView, default);
    }

    [Fact]
    public async Task Ai_features_require_premium_and_respect_fair_use_quota()
    {
        var (user, _, _) = await _db.SeedBasicsAsync(1);
        await Assert.ThrowsAsync<PremiumRequiredException>(() => Create().ConsumeAsync(user, UsageFeature.AiAnswerEvaluation, default));
        await _db.GrantPremiumAsync(user, _db.Clock.UtcNow.AddDays(30));
        await Create().ConsumeAsync(user, UsageFeature.AiAnswerEvaluation, default);
        await Create().ConsumeAsync(user, UsageFeature.AiAnswerEvaluation, default);
        await Assert.ThrowsAsync<QuotaExceededException>(() => Create().ConsumeAsync(user, UsageFeature.AiAnswerEvaluation, default));
    }

    [Fact]
    public async Task Expired_subscription_no_longer_grants_premium()
    {
        var (user, _, _) = await _db.SeedBasicsAsync(1);
        await _db.GrantPremiumAsync(user, _db.Clock.UtcNow.AddHours(1));
        Assert.True(await Create().IsPremiumAsync(user, default));
        _db.Clock.Advance(TimeSpan.FromHours(2));
        Assert.False(await Create().IsPremiumAsync(user, default));
        await Assert.ThrowsAsync<PremiumRequiredException>(() => Create().EnsurePremiumAsync(user, "AI", default));
    }

    [Fact]
    public async Task Premium_users_have_no_question_view_limit()
    {
        var (user, _, _) = await _db.SeedBasicsAsync(1);
        await _db.GrantPremiumAsync(user, _db.Clock.UtcNow.AddDays(30));
        for (var i = 0; i < 10; i++) await Create().ConsumeAsync(user, UsageFeature.QuestionView, default);
    }
}

public class GamificationServiceTests : IDisposable
{
    private readonly TestDb _db = new();
    public void Dispose() => _db.Dispose();
    private GamificationService Create() => new(_db.Create(), _db.Clock);

    [Fact]
    public async Task Duplicate_rewards_for_the_same_source_are_ignored()
    {
        var (user, _, _) = await _db.SeedBasicsAsync(1);
        Assert.Equal(40, await Create().AwardXpAsync(user, 40, XpSourceType.ChallengeSolved, "challenge:1", "Solved", default));
        Assert.Equal(0, await Create().AwardXpAsync(user, 40, XpSourceType.ChallengeSolved, "challenge:1", "Solved again", default));
        Assert.Equal(40, await Create().GetTotalXpAsync(user, default));
    }

    [Fact]
    public async Task Activity_updates_streak_and_awards_first_quiz_achievement()
    {
        var (user, _, _) = await _db.SeedBasicsAsync(1);
        await using (var db = _db.Create())
        {
            db.Achievements.Add(new Achievement { Code = "FIRST_QUIZ", Name = "First Quiz", XpReward = 20 });
            var quiz = new Quiz { Title = "q", IsSystem = true };
            db.Quizzes.Add(quiz);
            db.QuizAttempts.Add(new QuizAttempt { Quiz = quiz, UserId = user, Status = QuizAttemptStatus.Submitted, StartedAt = _db.Clock.UtcNow, TotalQuestions = 1, MaxScore = 1 });
            await db.SaveChangesAsync();
        }
        await Create().RecordActivityAsync(user, ActivityType.QuizCompleted, null, null, 120, default);
        _db.Clock.Advance(TimeSpan.FromDays(1));
        await Create().RecordActivityAsync(user, ActivityType.QuestionAttempted, null, null, 30, default);

        var (current, longest) = await Create().GetStreakAsync(user, default);
        Assert.Equal(2, current);
        Assert.Equal(2, longest);
        var profile = await Create().GetProfileAsync(user, default);
        Assert.Contains(profile.Achievements, a => a.Code == "FIRST_QUIZ" && a.Earned);
        Assert.Equal(20, profile.Level.TotalXp);
        // Weekly goal of 60 minutes is not reached by 2.5 minutes of activity.
        Assert.False(profile.WeeklyGoal.Achieved);
    }
}

public class QuizServiceTests : IDisposable
{
    private readonly TestDb _db = new();
    public void Dispose() => _db.Dispose();

    private QuizService Create()
    {
        var ctx = _db.Create();
        return new QuizService(ctx, new EntitlementService(ctx, _db.Clock, TestDb.Options(Limits.Default)), new GamificationService(ctx, _db.Clock),
            _db.Clock, TestDb.Options(Limits.Default), NullLogger<QuizService>.Instance);
    }

    private async Task<(Guid User, QuizListItemDto Quiz)> CreateQuizAsync(int? timeLimit = 300)
    {
        var (user, tech, _) = await _db.SeedBasicsAsync(6);
        var quiz = await Create().CreateAsync(user, new CreateQuizRequest(null, "Technology", new[] { tech }, null, null, 5, timeLimit, 0.25m, 60), default);
        return (user, quiz);
    }

    private static IReadOnlyList<SaveQuizAnswerRequest> AnswerAll(QuizAttemptDto attempt, bool correct) =>
        attempt.Questions.Select(q => new SaveQuizAnswerRequest(q.QuestionId,
            new[] { q.Options.Single(o => o.Text == (correct ? "right" : "wrong")).Id }, false, 5)).ToList();

    [Fact]
    public async Task Submitting_grades_on_the_server_with_negative_marking()
    {
        var (user, quiz) = await CreateQuizAsync();
        var attempt = await Create().StartAsync(user, quiz.Id, default);
        var answers = AnswerAll(attempt, true).Take(3).Concat(AnswerAll(attempt, false).Skip(3).Take(1)).ToList(); // 3 right, 1 wrong, 1 skipped
        var result = await Create().SubmitAsync(user, quiz.Id, new SubmitQuizRequest(attempt.AttemptId, answers), default);

        Assert.Equal(3, result.Correct);
        Assert.Equal(1, result.Incorrect);
        Assert.Equal(1, result.Skipped);
        Assert.Equal(2.75m, result.Score);
        Assert.Equal(55m, result.Percentage);
        Assert.False(result.Passed);
        Assert.All(result.Review, r => Assert.Contains(r.Options, o => o.IsCorrect));
    }

    [Fact]
    public async Task Duplicate_and_concurrent_submissions_grade_once()
    {
        var (user, quiz) = await CreateQuizAsync();
        var attempt = await Create().StartAsync(user, quiz.Id, default);
        var answers = AnswerAll(attempt, true);
        var results = await Task.WhenAll(
            Create().SubmitAsync(user, quiz.Id, new SubmitQuizRequest(attempt.AttemptId, answers), default),
            Create().SubmitAsync(user, quiz.Id, new SubmitQuizRequest(attempt.AttemptId, answers), default));
        var again = await Create().SubmitAsync(user, quiz.Id, new SubmitQuizRequest(attempt.AttemptId, Array.Empty<SaveQuizAnswerRequest>()), default);

        Assert.All(results.Append(again), r => Assert.Equal(5, r.Correct));
        await using var db = _db.Create();
        Assert.Equal(5, await db.QuestionAttempts.CountAsync(a => a.QuizAttemptId == attempt.AttemptId));
        Assert.Equal(1, await db.UserXpTransactions.CountAsync(x => x.SourceType == XpSourceType.QuizCompleted));
    }

    [Fact]
    public async Task Answers_after_the_deadline_are_rejected_and_attempt_auto_submits()
    {
        var (user, quiz) = await CreateQuizAsync(timeLimit: 60);
        var attempt = await Create().StartAsync(user, quiz.Id, default);
        var answers = AnswerAll(attempt, true);
        await Create().SaveAnswerAsync(user, attempt.AttemptId, answers[0], default);

        _db.Clock.Advance(TimeSpan.FromSeconds(60) + QuizService.DeadlineGrace + TimeSpan.FromSeconds(1));
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => Create().SaveAnswerAsync(user, attempt.AttemptId, answers[1], default));
        Assert.Equal("quiz_expired", ex.Code);

        var result = await Create().GetResultAsync(user, attempt.AttemptId, default);
        Assert.Equal("Expired", result.Status);
        Assert.Equal(1, result.Correct); // only the answer saved before the deadline counts
        Assert.Equal(4, result.Skipped);
    }

    [Fact]
    public async Task Background_job_finalizes_expired_attempts()
    {
        var (user, quiz) = await CreateQuizAsync(timeLimit: 60);
        await Create().StartAsync(user, quiz.Id, default);
        _db.Clock.Advance(TimeSpan.FromMinutes(5));
        Assert.Equal(1, await Create().FinalizeExpiredAttemptsAsync(default));
        Assert.Equal(0, await Create().FinalizeExpiredAttemptsAsync(default));
    }

    [Fact]
    public async Task Starting_again_resumes_the_in_progress_attempt()
    {
        var (user, quiz) = await CreateQuizAsync();
        var first = await Create().StartAsync(user, quiz.Id, default);
        var second = await Create().StartAsync(user, quiz.Id, default);
        Assert.Equal(first.AttemptId, second.AttemptId);
        Assert.All(first.Questions, q => Assert.Equal(2, q.Options.Count));
    }

    [Fact]
    public async Task Free_users_cannot_create_oversized_quizzes()
    {
        var (user, tech, _) = await _db.SeedBasicsAsync(3);
        await Assert.ThrowsAsync<PremiumRequiredException>(() =>
            Create().CreateAsync(user, new CreateQuizRequest(null, "Custom", new[] { tech }, null, null, 50, null, 0, 60), default));
    }
}

public class FlashcardAndSyncTests : IDisposable
{
    private readonly TestDb _db = new();
    public void Dispose() => _db.Dispose();

    private FlashcardService Flashcards(AppDbContextHolder h) => new(h.Ctx, new GamificationService(h.Ctx, _db.Clock), _db.Clock);
    private sealed record AppDbContextHolder(DevInterview.Infrastructure.Persistence.AppDbContext Ctx);

    private async Task<(Guid User, Guid Card, Guid Question)> SeedAsync()
    {
        var (user, tech, questions) = await _db.SeedBasicsAsync(1);
        await using var db = _db.Create();
        var card = new Flashcard { TechnologyId = tech, Front = "Q", Back = "A", IsPublished = true };
        db.Flashcards.Add(card);
        await db.SaveChangesAsync();
        return (user, card.Id, questions[0].Id);
    }

    [Fact]
    public async Task Replayed_reviews_are_idempotent()
    {
        var (user, card, _) = await SeedAsync();
        var review = new ReviewFlashcardRequest(card, "Good", Guid.NewGuid(), _db.Clock.UtcNow);
        var first = await Flashcards(new(_db.Create())).ReviewAsync(user, review, default);
        var replay = await Flashcards(new(_db.Create())).ReviewAsync(user, review, default);
        Assert.False(first.Duplicate);
        Assert.True(replay.Duplicate);
        Assert.Equal(first.NextDueAt, replay.NextDueAt);
        await using var db = _db.Create();
        Assert.Equal(1, (await db.FlashcardReviews.SingleAsync()).TotalReviews);
    }

    [Fact]
    public async Task Older_offline_review_does_not_rewind_the_schedule()
    {
        var (user, card, _) = await SeedAsync();
        var now = _db.Clock.UtcNow;
        await Flashcards(new(_db.Create())).ReviewAsync(user, new ReviewFlashcardRequest(card, "Good", Guid.NewGuid(), now), default);
        var stale = await Flashcards(new(_db.Create())).ReviewAsync(user, new ReviewFlashcardRequest(card, "Again", Guid.NewGuid(), now.AddHours(-3)), default);
        Assert.Equal(1, stale.IntervalDays); // still the "Good" schedule, not the stale "Again"
        Assert.True(stale.NextDueAt > now.AddHours(12));
    }

    [Fact]
    public async Task Sync_applies_last_writer_wins_and_reports_conflicts()
    {
        var (user, card, question) = await SeedAsync();
        var ctx = _db.Create();
        var sync = new SyncService(ctx, new FlashcardService(ctx, new GamificationService(ctx, _db.Clock), _db.Clock), _db.Clock);
        var t0 = _db.Clock.UtcNow;

        var push = await sync.PushAsync(user, new SyncPushRequest(
            new[] { new BookmarkChangeDto(question, true, t0.AddMinutes(-10)) },
            new[] { new NoteChangeDto(question, "offline note", false, t0.AddMinutes(-10)) },
            new[] { new ReviewFlashcardRequest(card, "Easy", Guid.NewGuid(), t0.AddMinutes(-9)) }), default);
        Assert.Equal((1, 1, 1), (push.AppliedBookmarks, push.AppliedNotes, push.AppliedReviews));

        // A newer server-side edit wins over an older offline edit.
        await using (var db = _db.Create())
        {
            var note = await db.QuestionNotes.SingleAsync();
            note.Content = "edited on web";
            note.ClientUpdatedAt = t0;
            await db.SaveChangesAsync();
        }
        var ctx2 = _db.Create();
        var stale = await new SyncService(ctx2, new FlashcardService(ctx2, new GamificationService(ctx2, _db.Clock), _db.Clock), _db.Clock)
            .PushAsync(user, new SyncPushRequest(null, new[] { new NoteChangeDto(question, "stale phone edit", false, t0.AddMinutes(-5)) }, null), default);
        Assert.Contains(stale.Conflicts, c => c.EntityType == "note" && c.Resolution == "server-newer");
        await using var check = _db.Create();
        Assert.Equal("edited on web", (await check.QuestionNotes.SingleAsync()).Content);

        var pull = await new SyncService(check, null!, _db.Clock).PullAsync(user, null, default);
        Assert.Single(pull.Bookmarks);
        Assert.Equal("edited on web", Assert.Single(pull.Notes).Content);
    }
}

public class MockInterviewServiceTests : IDisposable
{
    private readonly TestDb _db = new();
    public void Dispose() => _db.Dispose();

    private MockInterviewService Create(ScriptedProvider provider)
    {
        var ctx = _db.Create();
        var options = TestDb.Options(new AiOptions { ProviderOrder = new() { provider.Name }, MaxRetries = 0 });
        var orchestrator = new AiOrchestrator(new[] { provider }, ctx, options, NullLogger<AiOrchestrator>.Instance);
        return new MockInterviewService(ctx, orchestrator, new PromptProvider(ctx), new EntitlementService(ctx, _db.Clock, TestDb.Options(Limits.Default)),
            new GamificationService(ctx, _db.Clock), _db.Clock, options, NullLogger<MockInterviewService>.Instance);
    }

    private static string Turn(int score, string action, string next) =>
        $$"""{"score":{{score}},"feedback":"ok","nextAction":"{{action}}","nextQuestion":"{{next}}"}""";

    [Fact]
    public async Task Interview_flow_limits_follow_ups_adapts_difficulty_and_produces_bounded_report()
    {
        var (user, _, _) = await _db.SeedBasicsAsync(1);
        await _db.GrantPremiumAsync(user, _db.Clock.UtcNow.AddDays(30));
        var longAnswer = string.Join(' ', Enumerable.Repeat("detail", 40));
        var provider = new ScriptedProvider("Anthropic")
            .Returns("""{"intro":"Hi, I'm Alex.","question":"What is dependency injection?"}""")
            .Returns(Turn(90, "follow_up", "How do lifetimes differ?"))
            .Returns(Turn(90, "follow_up", "What is a captive dependency?"))
            .Returns(Turn(90, "follow_up", "Third follow-up that must be refused"))
            .Returns(Turn(20, "new_question", "Explain async/await."))
            .Returns("""{"overallScore":99,"technicalScore":99,"communicationScore":80,"problemSolvingScore":99,"completenessScore":99,"practicalKnowledgeScore":99,"summary":"Good","strengths":["DI"],"weaknesses":["async"],"missingConcepts":[],"suggestedAnswers":[],"recommendations":["Practise async"],"learningPath":["Async"]}""");

        var interview = await Create(provider).StartAsync(user, new StartMockInterviewRequest("Technical", "C#", "Intermediate", "Intermediate", 30, 2, "Backend Developer"), default);
        Assert.Equal("What is dependency injection?", interview.CurrentQuestion!.Text);

        for (var i = 0; i < 3; i++)
            interview = await Create(provider).AnswerAsync(user, interview.Id, new SubmitInterviewAnswerRequest(interview.CurrentQuestion!.Id, longAnswer, 30, false), default);
        // After two follow-ups the model's third follow-up is replaced by a new main question.
        Assert.False(interview.CurrentQuestion!.IsFollowUp);
        Assert.Equal("Expert", interview.CurrentDifficulty); // Intermediate -> Advanced -> Expert -> stays Expert

        interview = await Create(provider).AnswerAsync(user, interview.Id, new SubmitInterviewAnswerRequest(interview.CurrentQuestion!.Id, "not sure", 10, true), default);
        Assert.Null(interview.CurrentQuestion); // budget of 2 main questions exhausted
        Assert.Equal("Advanced", interview.CurrentDifficulty);

        var report = await Create(provider).CompleteAsync(user, interview.Id, default);
        var answerAverage = (90 + 90 + 90 + 10) / 4; // "not sure" is capped to 10 for length
        Assert.True(report.OverallScore <= answerAverage + 10, $"overall {report.OverallScore} must stay near the answer average {answerAverage}");
        Assert.Contains(report.InteractionSignals, s => s.Contains("by voice"));
        var again = await Create(provider).CompleteAsync(user, interview.Id, default); // idempotent, no extra AI call
        Assert.Equal(report.OverallScore, again.OverallScore);
    }

    [Fact]
    public async Task Free_users_cannot_start_mock_interviews()
    {
        var (user, _, _) = await _db.SeedBasicsAsync(1);
        await Assert.ThrowsAsync<PremiumRequiredException>(() => Create(new ScriptedProvider("Anthropic"))
            .StartAsync(user, new StartMockInterviewRequest("Hr", "General", "Beginner", "Beginner", 20, 3, "Developer"), default));
    }
}

public class CodeExecutionTests
{
    [Fact]
    public void Sandbox_output_parser_maps_exit_codes_to_outcomes()
    {
        string B(string s) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(s));
        var stdout = string.Join('\n',
            "@@DI_CASE 1 0 12", B("0 1\n"), "",
            "@@DI_CASE 2 1 5", "", B("boom"),
            "@@DI_CASE 3 137 2000", "", "",
            "@@DI_CASE 4 137 30", "", "");
        var result = DockerSandboxExecutionProvider.Parse(stdout, 5, 2000, killed: false);
        Assert.True(result.CompiledOk);
        Assert.Equal(new[] { CaseOutcome.Ok, CaseOutcome.RuntimeError, CaseOutcome.TimeLimitExceeded, CaseOutcome.MemoryLimitExceeded, CaseOutcome.InternalError },
            result.Cases.Select(c => c.Outcome));
        Assert.Equal("0 1\n", result.Cases[0].Stdout);
        Assert.Equal("boom", result.Cases[1].Stderr);
    }

    [Fact]
    public void Compile_failure_is_detected()
    {
        var result = DockerSandboxExecutionProvider.Parse("@@DI_COMPILE_FAILED\n" + Convert.ToBase64String("error CS1002"u8.ToArray()), 2, 1000, false);
        Assert.False(result.CompiledOk);
        Assert.Equal("error CS1002", result.CompileOutput);
    }

    [Fact]
    public void First_failing_case_decides_the_verdict()
    {
        Assert.Equal(SubmissionStatus.Accepted, CodingService.Overall(new[] { SubmissionStatus.Accepted, SubmissionStatus.Accepted }));
        Assert.Equal(SubmissionStatus.WrongAnswer, CodingService.Overall(new[] { SubmissionStatus.Accepted, SubmissionStatus.WrongAnswer, SubmissionStatus.TimeLimitExceeded }));
        var (status, passed) = CodingService.Evaluate(new CaseResult(CaseOutcome.Ok, "1 2\r\n", "", 0, 3), "1 2");
        Assert.True(passed);
        Assert.Equal(SubmissionStatus.Accepted, status);
    }

    [Fact]
    public void Audit_details_mask_secrets()
    {
        var masked = DbAuditLogger.Mask("""{"email":"a@b.c","newPassword":"hunter2","refreshToken":"abc","apiKey":"k"}""");
        Assert.DoesNotContain("hunter2", masked);
        Assert.DoesNotContain("\"abc\"", masked);
        Assert.Contains("a@b.c", masked);
    }

    [Fact]
    public void Image_signature_detection_rejects_non_images()
    {
        Assert.Equal(".png", DevInterview.Application.Features.Users.ImageSignature.Detect(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0 }));
        Assert.Equal(".jpg", DevInterview.Application.Features.Users.ImageSignature.Detect(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }));
        Assert.Null(DevInterview.Application.Features.Users.ImageSignature.Detect("<?php echo 1; ?>"u8));
    }
}

public class CollectionServiceTests : IDisposable
{
    private readonly TestDb _db = new();
    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Adding_the_same_item_twice_keeps_one_entry()
    {
        var (user, _, questions) = await _db.SeedBasicsAsync(1);
        var service = new CollectionService(_db.Create());
        var collection = await service.CreateAsync(user, new DevInterview.Contracts.Content.CreateCollectionRequest("Before Monday", null), default);
        await service.AddItemAsync(user, collection.Id, new("Question", questions[0].Id), default);
        await new CollectionService(_db.Create()).AddItemAsync(user, collection.Id, new("Question", questions[0].Id), default);
        var detail = await new CollectionService(_db.Create()).GetAsync(user, collection.Id, default);
        Assert.Single(detail.Items);
        await Assert.ThrowsAsync<ConflictException>(() => new CollectionService(_db.Create()).CreateAsync(user, new("Before Monday", null), default));
    }
}
