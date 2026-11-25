using System.Text;
using System.Text.Json.Nodes;
using DevInterview.Application.Abstractions;
using DevInterview.Application.Common;
using DevInterview.Application.Features.Ai;
using DevInterview.Application.Features.Gamification;
using DevInterview.Application.Features.Subscriptions;
using DevInterview.Contracts.Ai;
using DevInterview.Contracts.Common;
using DevInterview.Domain.Entities;
using DevInterview.Domain.Enums;
using DevInterview.Domain.Rules;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DevInterview.Application.Features.MockInterviews;

public interface IMockInterviewService
{
    Task<MockInterviewDto> StartAsync(Guid userId, StartMockInterviewRequest request, CancellationToken ct);
    Task<MockInterviewDto> GetAsync(Guid userId, Guid interviewId, CancellationToken ct);
    Task<PagedResult<MockInterviewListItemDto>> ListAsync(Guid userId, PageQuery query, CancellationToken ct);
    Task<MockInterviewDto> AnswerAsync(Guid userId, Guid interviewId, SubmitInterviewAnswerRequest request, CancellationToken ct);
    Task<MockInterviewDto> NextQuestionAsync(Guid userId, Guid interviewId, CancellationToken ct);
    Task<MockInterviewReportDto> CompleteAsync(Guid userId, Guid interviewId, CancellationToken ct);
    Task<MockInterviewReportDto> GetReportAsync(Guid userId, Guid interviewId, CancellationToken ct);
}

public sealed class MockInterviewService : IMockInterviewService
{
    public const int MaxFollowUpsPerTopic = 2;
    public const string SkippedMarker = "[The candidate skipped this question]";

    private readonly IAppDbContext _db;
    private readonly IAiOrchestrator _ai;
    private readonly IPromptProvider _prompts;
    private readonly IEntitlementService _entitlements;
    private readonly IGamificationService _gamification;
    private readonly IClock _clock;
    private readonly AiOptions _options;
    private readonly ILogger<MockInterviewService> _logger;

    public MockInterviewService(IAppDbContext db, IAiOrchestrator ai, IPromptProvider prompts, IEntitlementService entitlements,
        IGamificationService gamification, IClock clock, IOptions<AiOptions> options, ILogger<MockInterviewService> logger)
    {
        _db = db;
        _ai = ai;
        _prompts = prompts;
        _entitlements = entitlements;
        _gamification = gamification;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<MockInterviewDto> StartAsync(Guid userId, StartMockInterviewRequest request, CancellationToken ct)
    {
        if (!_ai.IsAvailable) throw new ServiceUnavailableException(ErrorCodes.AiUnavailable, "AI mock interviews are not configured on this server.");
        await _entitlements.EnsurePremiumAsync(userId, "AI mock interviews", ct);
        var type = EnumParser.Parse<MockInterviewType>(request.Type, nameof(request.Type));
        var level = EnumParser.Parse<ExperienceLevel>(request.ExperienceLevel, nameof(request.ExperienceLevel));
        var difficulty = EnumParser.Parse<Difficulty>(request.Difficulty, nameof(request.Difficulty));
        var inProgress = await _db.MockInterviews.CountAsync(m => m.UserId == userId && m.Status == MockInterviewStatus.InProgress, ct);
        if (inProgress >= 3) throw new ConflictException("You already have 3 interviews in progress. Finish or end one first.");
        await _entitlements.ConsumeAsync(userId, UsageFeature.AiMockInterview, ct);

        var interview = new MockInterview
        {
            UserId = userId, Type = type, Technology = request.Technology.Trim(), ExperienceLevel = level, InitialDifficulty = difficulty,
            CurrentDifficulty = difficulty, TargetRole = request.TargetRole.Trim(), PlannedQuestionCount = request.QuestionCount,
            DurationMinutes = request.DurationMinutes, Status = MockInterviewStatus.InProgress, StartedAt = _clock.UtcNow
        };

        var json = await _ai.CompleteJsonAsync(AiFeature.MockInterview, userId, new AiCompletionRequest(
            await _prompts.GetAsync(DefaultPrompts.InterviewStart, ct),
            new[] { new AiMessage(AiRole.User, Settings(interview) + $"\nAsk the first question at {difficulty} difficulty.") }, 1500), ct);
        var question = AiJson.Str(json, "question", 2000);
        if (question.Length < 5) throw new ServiceUnavailableException(ErrorCodes.AiInvalidResponse, "The interviewer could not start. Please try again.");
        interview.IntroMessage = AiJson.Str(json, "intro", 1500);
        interview.Questions.Add(new MockInterviewQuestion { Sequence = 1, Text = question, Difficulty = difficulty, AskedAt = _clock.UtcNow });
        _db.MockInterviews.Add(interview);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Mock interview {InterviewId} started ({Type})", interview.Id, type);
        return await GetAsync(userId, interview.Id, ct);
    }

    private static string Settings(MockInterview m) =>
        $"Interview type: {m.Type}\nTechnology focus: {Text.Truncate(m.Technology, 80)}\nTarget role: {Text.Truncate(m.TargetRole, 80)}\n" +
        $"Candidate experience level: {m.ExperienceLevel}\nPlanned main questions: {m.PlannedQuestionCount}\nDuration: {m.DurationMinutes} minutes";

    private async Task<MockInterview> LoadAsync(Guid userId, Guid id, bool tracking, CancellationToken ct)
    {
        var q = _db.MockInterviews.Include(m => m.Questions).ThenInclude(q => q.Answer).Include(m => m.Evaluation).AsSplitQuery();
        if (!tracking) q = q.AsNoTracking();
        return await q.FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId, ct) ?? throw new NotFoundException("Mock interview", id);
    }

    public async Task<MockInterviewDto> GetAsync(Guid userId, Guid interviewId, CancellationToken ct) =>
        ToDto(await LoadAsync(userId, interviewId, false, ct));

    private MockInterviewDto ToDto(MockInterview m)
    {
        var messages = new List<InterviewMessageDto>();
        if (!string.IsNullOrEmpty(m.IntroMessage)) messages.Add(new InterviewMessageDto("interviewer", m.IntroMessage, null, false, m.StartedAt, null, null));
        foreach (var q in m.Questions.OrderBy(q => q.Sequence))
        {
            messages.Add(new InterviewMessageDto("interviewer", q.Text, q.Id, q.IsFollowUp, q.AskedAt, null, null));
            if (q.Answer is { } a)
                messages.Add(new InterviewMessageDto("candidate", a.AnswerText, q.Id, q.IsFollowUp, a.AnsweredAt, a.Score, a.Feedback));
        }
        var current = m.Status == MockInterviewStatus.InProgress
            ? m.Questions.Where(q => q.Answer is null).OrderBy(q => q.Sequence).LastOrDefault()
            : null;
        return new MockInterviewDto(m.Id, m.Type.ToString(), m.Technology, m.ExperienceLevel.ToString(), m.CurrentDifficulty.ToString(), m.TargetRole,
            m.PlannedQuestionCount, m.DurationMinutes, m.Status.ToString(), m.StartedAt, m.CompletedAt, m.StartedAt.AddMinutes(m.DurationMinutes), messages,
            current is null ? null : new InterviewQuestionDto(current.Id, current.Sequence, current.Text, current.IsFollowUp, current.Difficulty.ToString()),
            m.MainQuestionsAsked, m.Evaluation is not null);
    }

    public async Task<PagedResult<MockInterviewListItemDto>> ListAsync(Guid userId, PageQuery query, CancellationToken ct) =>
        await _db.MockInterviews.AsNoTracking().Where(m => m.UserId == userId).OrderByDescending(m => m.StartedAt)
            .Select(m => new MockInterviewListItemDto(m.Id, m.Type.ToString(), m.Technology, m.Status.ToString(), m.StartedAt, m.CompletedAt,
                m.Evaluation == null ? null : m.Evaluation.OverallScore))
            .ToPagedAsync(query, ct);

    public async Task<MockInterviewDto> AnswerAsync(Guid userId, Guid interviewId, SubmitInterviewAnswerRequest request, CancellationToken ct)
    {
        var interview = await LoadAsync(userId, interviewId, true, ct);
        EnsureInProgress(interview);
        var question = interview.Questions.FirstOrDefault(q => q.Id == request.QuestionId)
                       ?? throw ValidationAppException.For("questionId", "This question does not belong to the interview.");
        if (question.Answer is not null) throw new ConflictException("This question has already been answered.");
        var latest = interview.Questions.OrderBy(q => q.Sequence).Last();
        if (latest.Id != question.Id) throw new ConflictException("Please answer the current question.");

        await ProcessTurnAsync(interview, question, request.Answer.Trim(), request.ResponseSeconds, request.UsedVoiceInput, skipped: false, ct);
        return ToDto(interview);
    }

    public async Task<MockInterviewDto> NextQuestionAsync(Guid userId, Guid interviewId, CancellationToken ct)
    {
        var interview = await LoadAsync(userId, interviewId, true, ct);
        EnsureInProgress(interview);
        var current = interview.Questions.OrderBy(q => q.Sequence).Last();
        if (current.Answer is not null) return ToDto(interview); // nothing pending; the budget is exhausted
        await ProcessTurnAsync(interview, current, SkippedMarker, 0, false, skipped: true, ct);
        return ToDto(interview);
    }

    private static void EnsureInProgress(MockInterview interview)
    {
        if (interview.Status != MockInterviewStatus.InProgress) throw new ConflictException("This interview has already ended.");
    }

    private async Task ProcessTurnAsync(MockInterview interview, MockInterviewQuestion question, string answerText, int responseSeconds,
        bool voice, bool skipped, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var mainAsked = interview.MainQuestionsAsked;
        var followUpsInTopic = CountTrailingFollowUps(interview);
        var budgetExhausted = mainAsked >= interview.PlannedQuestionCount;
        var timeUp = now > interview.StartedAt.AddMinutes(interview.DurationMinutes);
        var mayFollowUp = !skipped && !timeUp && followUpsInTopic < MaxFollowUpsPerTopic;

        var instruction = new StringBuilder();
        instruction.AppendLine(Settings(interview));
        instruction.AppendLine($"Current difficulty: {interview.CurrentDifficulty}. Main questions asked: {mainAsked} of {interview.PlannedQuestionCount}.");
        if (timeUp) instruction.AppendLine("The interview time is over: evaluate the answer and set nextAction to \"complete\".");
        else if (budgetExhausted && mayFollowUp) instruction.AppendLine("The main-question budget is exhausted: either ask one final follow-up on this topic or set nextAction to \"complete\".");
        else if (budgetExhausted) instruction.AppendLine("The question budget is exhausted: set nextAction to \"complete\".");
        else if (!mayFollowUp) instruction.AppendLine("Do not ask a follow-up; set nextAction to \"new_question\" and move to a new topic.");
        if (skipped) instruction.AppendLine("The candidate chose to skip the question; score it 0 and move on.");
        instruction.AppendLine();
        instruction.AppendLine(UntrustedContent.Wrap("transcript", BuildTranscript(interview, question.Id), _options.MaxInputCharacters));
        instruction.AppendLine(UntrustedContent.Wrap("question", question.Text, 2000));
        instruction.AppendLine(UntrustedContent.Wrap("candidate_answer", answerText, 8000));

        var json = await _ai.CompleteJsonAsync(AiFeature.MockInterview, interview.UserId, new AiCompletionRequest(
            $"{await _prompts.GetAsync(DefaultPrompts.InterviewTurn, ct)}\n\n{UntrustedContent.Guard}",
            new[] { new AiMessage(AiRole.User, instruction.ToString()) }, 1500), ct);

        var score = skipped ? 0 : AnswerHeuristics.ApplyCap(AiJson.Score(json, "score"), answerText);
        question.Answer = new MockInterviewAnswer
        {
            AnswerText = Text.Truncate(answerText, 8000), AnsweredAt = now, ResponseSeconds = Math.Clamp(responseSeconds, 0, 3600),
            UsedVoiceInput = voice, Score = score, Feedback = skipped ? "Skipped." : AiJson.Str(json, "feedback", 600)
        };
        interview.CurrentDifficulty = AdaptiveDifficulty.Next(interview.CurrentDifficulty, score);

        // The server, not the model, decides whether another question is allowed.
        var action = AiJson.Str(json, "nextAction", 20).ToLowerInvariant();
        var next = AiJson.Str(json, "nextQuestion", 2000);
        var askFollowUp = action == "follow_up" && mayFollowUp && next.Length >= 5;
        var askNew = !askFollowUp && !budgetExhausted && !timeUp && next.Length >= 5;
        if (askFollowUp || askNew)
        {
            interview.Questions.Add(new MockInterviewQuestion
            {
                Sequence = interview.Questions.Count + 1, Text = next, IsFollowUp = askFollowUp,
                ParentQuestionId = askFollowUp ? (question.IsFollowUp ? question.ParentQuestionId : question.Id) : null,
                Difficulty = interview.CurrentDifficulty, AskedAt = now
            });
        }
        await _db.SaveChangesAsync(ct);
    }

    private static int CountTrailingFollowUps(MockInterview interview)
    {
        var count = 0;
        foreach (var q in interview.Questions.OrderByDescending(q => q.Sequence))
        {
            if (!q.IsFollowUp) break;
            count++;
        }
        return count;
    }

    private static string BuildTranscript(MockInterview interview, Guid? excludeQuestionId, int maxTurns = 24)
    {
        var turns = interview.Questions.OrderBy(q => q.Sequence).Where(q => q.Id != excludeQuestionId).TakeLast(maxTurns).ToList();
        if (turns.Count == 0) return "(no previous questions)";
        var sb = new StringBuilder();
        foreach (var q in turns)
        {
            sb.Append("Interviewer").Append(q.IsFollowUp ? " (follow-up)" : "").Append(": ").AppendLine(q.Text);
            if (q.Answer is { } a)
                sb.Append("Candidate: ").AppendLine(Text.Truncate(a.AnswerText, 1500)).Append("[answer score ").Append(a.Score).AppendLine("]");
        }
        return sb.ToString();
    }

    public async Task<MockInterviewReportDto> CompleteAsync(Guid userId, Guid interviewId, CancellationToken ct)
    {
        var interview = await LoadAsync(userId, interviewId, true, ct);
        if (interview.Evaluation is not null) return await GetReportAsync(userId, interviewId, ct); // idempotent
        EnsureInProgress(interview);
        var answered = interview.Questions.Where(q => q.Answer is not null && q.Answer.AnswerText != SkippedMarker).ToList();
        if (answered.Count == 0)
        {
            interview.Status = MockInterviewStatus.Abandoned;
            interview.CompletedAt = _clock.UtcNow;
            await _db.SaveChangesAsync(ct);
            throw new BusinessRuleException(ErrorCodes.Validation, "The interview ended without any answers, so no report was generated.");
        }

        var json = await _ai.CompleteJsonAsync(AiFeature.MockInterview, userId, new AiCompletionRequest(
            $"{await _prompts.GetAsync(DefaultPrompts.InterviewReport, ct)}\n\n{UntrustedContent.Guard}",
            new[] { new AiMessage(AiRole.User, Settings(interview) + "\n\n" + UntrustedContent.Wrap("transcript", BuildTranscript(interview, null, 60), _options.MaxInputCharacters * 2)) },
            6000), ct);

        var scores = interview.Questions.Where(q => q.Answer?.Score is not null).Select(q => q.Answer!.Score!.Value).ToList();
        var average = scores.Count == 0 ? 0 : (int)Math.Round(scores.Average());
        // Keep the report consistent with the per-answer scores: the overall cannot exceed the average by more than 10 points.
        int Bound(int value) => Math.Min(value, Math.Min(100, average + 10));
        var evaluation = new MockInterviewEvaluation
        {
            InterviewId = interview.Id,
            OverallScore = Bound(AiJson.Score(json, "overallScore", average)),
            TechnicalScore = Bound(AiJson.Score(json, "technicalScore", average)),
            CommunicationScore = Math.Min(100, AiJson.Score(json, "communicationScore", average)),
            ProblemSolvingScore = Bound(AiJson.Score(json, "problemSolvingScore", average)),
            CompletenessScore = Bound(AiJson.Score(json, "completenessScore", average)),
            PracticalKnowledgeScore = Bound(AiJson.Score(json, "practicalKnowledgeScore", average)),
            Summary = AiJson.Str(json, "summary", 2000),
            ReportJson = Json.Serialize(new ReportPayload(
                AiJson.StrList(json, "strengths"), AiJson.StrList(json, "weaknesses"), AiJson.StrList(json, "missingConcepts"),
                AiJson.Arr(json, "suggestedAnswers").OfType<JsonObject>().Take(10).Select(s => new SuggestedAnswerDto(
                    AiJson.Str(s, "question", 600), AiJson.Str(s, "candidateAnswerSummary", 600), AiJson.Str(s, "suggestedAnswer", 2500))).ToList(),
                AiJson.StrList(json, "recommendations"), AiJson.StrList(json, "learningPath", 10, 200)))
        };
        interview.Evaluation = evaluation;
        interview.Status = MockInterviewStatus.Completed;
        interview.CompletedAt = _clock.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _gamification.AwardXpAsync(userId, XpRules.MockInterviewCompleted, XpSourceType.MockInterviewCompleted, $"mock:{interview.Id}",
            $"Completed a {interview.Type} mock interview", ct);
        var minutes = (int)Math.Min((interview.CompletedAt.Value - interview.StartedAt).TotalSeconds, interview.DurationMinutes * 60 + 600);
        await _gamification.RecordActivityAsync(userId, ActivityType.MockInterviewCompleted, interview.Id, null, minutes, ct);
        return await GetReportAsync(userId, interviewId, ct);
    }

    internal sealed record ReportPayload(List<string> Strengths, List<string> Weaknesses, List<string> MissingConcepts,
        List<SuggestedAnswerDto> SuggestedAnswers, List<string> Recommendations, List<string> LearningPath);

    public async Task<MockInterviewReportDto> GetReportAsync(Guid userId, Guid interviewId, CancellationToken ct)
    {
        var m = await LoadAsync(userId, interviewId, false, ct);
        var e = m.Evaluation ?? throw new NotFoundException("Interview report", interviewId);
        var payload = Json.Deserialize<ReportPayload>(e.ReportJson) ?? new ReportPayload(new(), new(), new(), new(), new(), new());

        var tech = m.Technology.ToLowerInvariant();
        var paths = await _db.LearningPaths.AsNoTracking().Where(p => p.IsPublished && p.IsActive && !p.IsDeleted)
            .Select(p => new { p.Id, p.Title, p.Slug, Tech = p.Technology == null ? null : p.Technology.Name }).ToListAsync(ct);
        var recommended = paths.Where(p => (p.Tech is not null && tech.Contains(p.Tech.ToLowerInvariant())) ||
                                           tech.Split(' ', ',', '/').Any(w => w.Length > 1 && p.Title.Contains(w, StringComparison.OrdinalIgnoreCase)) ||
                                           (m.Type == MockInterviewType.SystemDesign && p.Slug.Contains("system-design")) ||
                                           (m.Type == MockInterviewType.Sql && p.Slug.Contains("sql")))
            .Take(3).Select(p => new RefDto(p.Id, p.Title, p.Slug)).ToList();

        var answers = m.Questions.Where(q => q.Answer is not null).Select(q => q.Answer!).ToList();
        var signals = new List<string>();
        var real = answers.Where(a => a.AnswerText != SkippedMarker).ToList();
        if (real.Count > 0)
        {
            signals.Add($"Average answer length: {(int)real.Average(a => AnswerHeuristics.WordCount(a.AnswerText))} words.");
            var timed = real.Where(a => a.ResponseSeconds > 0).ToList();
            if (timed.Count > 0) signals.Add($"Average response time: {(int)timed.Average(a => a.ResponseSeconds)} seconds.");
            if (real.Any(a => a.UsedVoiceInput)) signals.Add($"{real.Count(a => a.UsedVoiceInput)} of {real.Count} answers were given by voice.");
        }
        var skippedCount = answers.Count(a => a.AnswerText == SkippedMarker);
        if (skippedCount > 0) signals.Add($"{skippedCount} question(s) were skipped.");

        return new MockInterviewReportDto(m.Id, m.Type.ToString(), m.Technology, m.TargetRole, e.OverallScore, e.TechnicalScore, e.CommunicationScore,
            e.ProblemSolvingScore, e.CompletenessScore, e.PracticalKnowledgeScore, e.Summary, payload.Strengths, payload.Weaknesses,
            payload.MissingConcepts, payload.SuggestedAnswers, payload.Recommendations, payload.LearningPath, recommended, signals,
            m.Questions.OrderBy(q => q.Sequence).Select(q => new QuestionBreakdownDto(q.Sequence, q.Text, q.Answer?.AnswerText ?? "(not answered)",
                q.Answer?.Score, q.Answer?.Feedback, q.IsFollowUp)).ToList(),
            m.CompletedAt ?? e.CreatedAt);
    }
}

public sealed class StartMockInterviewRequestValidator : AbstractValidator<StartMockInterviewRequest>
{
    public StartMockInterviewRequestValidator()
    {
        RuleFor(x => x.Type).NotEmpty();
        RuleFor(x => x.Technology).NotEmpty().MaximumLength(80);
        RuleFor(x => x.ExperienceLevel).NotEmpty();
        RuleFor(x => x.Difficulty).NotEmpty();
        RuleFor(x => x.DurationMinutes).InclusiveBetween(10, 90);
        RuleFor(x => x.QuestionCount).InclusiveBetween(3, 15);
        RuleFor(x => x.TargetRole).NotEmpty().MaximumLength(80);
    }
}

public sealed class SubmitInterviewAnswerRequestValidator : AbstractValidator<SubmitInterviewAnswerRequest>
{
    public SubmitInterviewAnswerRequestValidator()
    {
        RuleFor(x => x.QuestionId).NotEmpty();
        RuleFor(x => x.Answer).NotEmpty().MaximumLength(8000);
        RuleFor(x => x.ResponseSeconds).InclusiveBetween(0, 24 * 3600);
    }
}
