using DevInterview.Application.Abstractions;
using DevInterview.Application.Common;
using DevInterview.Application.Features.Gamification;
using DevInterview.Application.Features.Subscriptions;
using DevInterview.Contracts.Common;
using DevInterview.Contracts.Content;
using DevInterview.Contracts.Practice;
using DevInterview.Domain.Entities;
using DevInterview.Domain.Enums;
using DevInterview.Domain.Rules;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DevInterview.Application.Features.Quizzes;

public interface IQuizService
{
    Task<PagedResult<QuizListItemDto>> ListAsync(Guid userId, QuizQuery query, CancellationToken ct);
    Task<QuizListItemDto> GetAsync(Guid userId, Guid quizId, CancellationToken ct);
    Task<QuizListItemDto> CreateAsync(Guid userId, CreateQuizRequest request, CancellationToken ct);
    Task<QuizListItemDto> GetDailyAsync(Guid userId, CancellationToken ct);
    Task<QuizAttemptDto> StartAsync(Guid userId, Guid quizId, CancellationToken ct);
    Task<QuizAttemptDto> GetAttemptStateAsync(Guid userId, Guid attemptId, CancellationToken ct);
    Task SaveAnswerAsync(Guid userId, Guid attemptId, SaveQuizAnswerRequest request, CancellationToken ct);
    Task<QuizResultDto> SubmitAsync(Guid userId, Guid quizId, SubmitQuizRequest request, CancellationToken ct);
    Task<QuizResultDto> GetResultAsync(Guid userId, Guid attemptId, CancellationToken ct);
    Task<PagedResult<QuizAttemptSummaryDto>> GetHistoryAsync(Guid userId, PageQuery query, CancellationToken ct);
    /// <summary>Background job: grades timed attempts whose deadline passed without a submission.</summary>
    Task<int> FinalizeExpiredAttemptsAsync(CancellationToken ct);
}

public sealed class QuizService : IQuizService
{
    /// <summary>Network grace period after the deadline in which answers are still accepted.</summary>
    public static readonly TimeSpan DeadlineGrace = TimeSpan.FromSeconds(10);
    public const int DailyQuizQuestionCount = 10;

    private readonly IAppDbContext _db;
    private readonly IEntitlementService _entitlements;
    private readonly IGamificationService _gamification;
    private readonly IClock _clock;
    private readonly PlanLimitsOptions _limits;
    private readonly ILogger<QuizService> _logger;

    public QuizService(IAppDbContext db, IEntitlementService entitlements, IGamificationService gamification, IClock clock,
        IOptions<PlanLimitsOptions> limits, ILogger<QuizService> logger)
    {
        _db = db;
        _entitlements = entitlements;
        _gamification = gamification;
        _clock = clock;
        _limits = limits.Value;
        _logger = logger;
    }

    private IQueryable<Quiz> VisibleTo(Guid userId) => _db.Quizzes.Where(q => q.IsActive && !q.IsDeleted &&
        ((q.IsSystem && q.IsPublished) || q.OwnerUserId == userId));

    private IQueryable<QuizListItemDto> Project(IQueryable<Quiz> q, Guid userId) => q.Select(x => new QuizListItemDto(
        x.Id, x.Title, x.Description, x.Mode.ToString(), x.Questions.Count, x.TimeLimitSeconds, x.PassingPercentage,
        x.NegativeMarkingFraction, x.Difficulty == null ? null : x.Difficulty.ToString(),
        x.Technology == null ? null : new RefDto(x.Technology.Id, x.Technology.Name, x.Technology.Slug), x.IsPremium,
        _db.QuizAttempts.Count(a => a.QuizId == x.Id && a.UserId == userId && a.Status != QuizAttemptStatus.InProgress),
        _db.QuizAttempts.Where(a => a.QuizId == x.Id && a.UserId == userId && a.Status != QuizAttemptStatus.InProgress && a.MaxScore > 0)
            .Select(a => (decimal?)(a.Score * 100 / a.MaxScore)).Max()));

    public async Task<PagedResult<QuizListItemDto>> ListAsync(Guid userId, QuizQuery query, CancellationToken ct)
    {
        var q = VisibleTo(userId).AsNoTracking().Where(x => x.Mode != QuizMode.Daily);
        if (EnumParser.ParseOptional<QuizMode>(query.Mode, "mode") is { } mode) q = q.Where(x => x.Mode == mode);
        if (query.TechnologyId is { } t) q = q.Where(x => x.TechnologyId == t);
        if (EnumParser.ParseOptional<Difficulty>(query.Difficulty, "difficulty") is { } d) q = q.Where(x => x.Difficulty == d);
        if (!string.IsNullOrWhiteSpace(query.Search)) q = q.Where(x => x.Title.Contains(query.Search));
        q = q.OrderByDescending(x => x.IsSystem).ThenByDescending(x => x.CreatedAt);
        return await Project(q, userId).ToPagedAsync(query, ct);
    }

    public async Task<QuizListItemDto> GetAsync(Guid userId, Guid quizId, CancellationToken ct) =>
        await Project(VisibleTo(userId).AsNoTracking().Where(x => x.Id == quizId), userId).FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("Quiz", quizId);

    public async Task<QuizListItemDto> CreateAsync(Guid userId, CreateQuizRequest request, CancellationToken ct)
    {
        var mode = EnumParser.Parse<QuizMode>(request.Mode, nameof(request.Mode));
        if (mode == QuizMode.Daily) throw ValidationAppException.For("mode", "Daily quizzes are generated automatically.");
        var difficulty = EnumParser.ParseOptional<Difficulty>(request.Difficulty, nameof(request.Difficulty));
        var premium = await _entitlements.IsPremiumAsync(userId, ct);
        var maxQuestions = premium ? 100 : _limits.FreeMaxQuizQuestions;
        if (request.QuestionCount > maxQuestions)
        {
            if (!premium) throw new PremiumRequiredException($"Quizzes longer than {maxQuestions} questions");
            throw ValidationAppException.For("questionCount", $"A quiz can contain at most {maxQuestions} questions.");
        }

        var techIds = request.TechnologyIds?.Distinct().ToList() ?? new List<Guid>();
        var catIds = request.CategoryIds?.Distinct().ToList() ?? new List<Guid>();
        var pool = _db.Questions.Where(q => q.Status == ContentStatus.Published && q.IsActive && !q.IsDeleted && q.Options.Any(o => o.IsCorrect));
        if (!premium) pool = pool.Where(q => !q.IsPremium);
        if (techIds.Count > 0) pool = pool.Where(q => techIds.Contains(q.TechnologyId));
        if (catIds.Count > 0) pool = pool.Where(q => catIds.Contains(q.CategoryId));
        if (difficulty is { } d) pool = pool.Where(q => q.Difficulty == d);

        var candidates = await pool.Select(q => q.Id).Take(5000).ToListAsync(ct);
        var selected = candidates.OrderBy(_ => Random.Shared.Next()).Take(request.QuestionCount).ToList();
        if (selected.Count < Math.Min(3, request.QuestionCount))
            throw new BusinessRuleException(ErrorCodes.Validation, "Not enough questions match these filters. Try broadening the technologies or difficulty.");

        var techNames = techIds.Count == 0 ? new List<string>() :
            await _db.Technologies.Where(t => techIds.Contains(t.Id)).Select(t => t.Name).ToListAsync(ct);
        var title = string.IsNullOrWhiteSpace(request.Title)
            ? $"{(techNames.Count == 0 ? "Mixed" : string.Join(", ", techNames.Take(3)))} {mode} quiz"
            : request.Title.Trim();

        var quiz = new Quiz
        {
            Title = Text.Truncate(title, 200), Mode = mode, OwnerUserId = userId, IsSystem = false, IsPublished = false,
            TimeLimitSeconds = request.TimeLimitSeconds, NegativeMarkingFraction = request.NegativeMarkingFraction,
            PassingPercentage = request.PassingPercentage, Difficulty = difficulty,
            TechnologyId = techIds.Count == 1 ? techIds[0] : null, CategoryId = catIds.Count == 1 ? catIds[0] : null
        };
        for (var i = 0; i < selected.Count; i++) quiz.Questions.Add(new QuizQuestion { QuestionId = selected[i], DisplayOrder = i });
        _db.Quizzes.Add(quiz);
        await _db.SaveChangesAsync(ct);
        return await GetAsync(userId, quiz.Id, ct);
    }

    public async Task<QuizListItemDto> GetDailyAsync(Guid userId, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(_clock.UtcNow);
        var existing = await _db.Quizzes.Where(q => q.Mode == QuizMode.Daily && q.DailyDate == today).Select(q => (Guid?)q.Id).FirstOrDefaultAsync(ct);
        if (existing is null)
        {
            var ids = await _db.Questions.Where(q => q.Status == ContentStatus.Published && q.IsActive && !q.IsDeleted && !q.IsPremium && q.Options.Any(o => o.IsCorrect))
                .Select(q => q.Id).ToListAsync(ct);
            if (ids.Count == 0) throw new NotFoundException("Daily quiz");
            var rng = new Random(today.DayNumber); // deterministic per day across instances
            var picked = ids.OrderBy(id => id).OrderBy(_ => rng.Next()).Take(DailyQuizQuestionCount).ToList();
            var quiz = new Quiz
            {
                Title = $"Daily Quiz · {today:MMM d}", Description = "Ten mixed questions, refreshed every day.",
                Mode = QuizMode.Daily, DailyDate = today, IsSystem = true, IsPublished = true, TimeLimitSeconds = 600,
                PassingPercentage = 60, ShuffleQuestions = false
            };
            for (var i = 0; i < picked.Count; i++) quiz.Questions.Add(new QuizQuestion { QuestionId = picked[i], DisplayOrder = i });
            _db.Quizzes.Add(quiz);
            try
            {
                await _db.SaveChangesAsync(ct);
                existing = quiz.Id;
            }
            catch (DbUpdateException)
            {
                _db.ResetTracking(); // another request created today's quiz concurrently
                existing = await _db.Quizzes.Where(q => q.Mode == QuizMode.Daily && q.DailyDate == today).Select(q => (Guid?)q.Id).FirstAsync(ct);
            }
        }
        return await GetAsync(userId, existing.Value, ct);
    }

    public async Task<QuizAttemptDto> StartAsync(Guid userId, Guid quizId, CancellationToken ct)
    {
        var quiz = await VisibleTo(userId).Include(q => q.Questions).FirstOrDefaultAsync(q => q.Id == quizId, ct)
                   ?? throw new NotFoundException("Quiz", quizId);
        if (quiz.IsPremium) await _entitlements.EnsurePremiumAsync(userId, "This quiz", ct);
        if (quiz.Questions.Count == 0) throw new BusinessRuleException(ErrorCodes.Validation, "This quiz has no questions yet.");

        var now = _clock.UtcNow;
        var active = await _db.QuizAttempts.Where(a => a.QuizId == quizId && a.UserId == userId && a.Status == QuizAttemptStatus.InProgress)
            .OrderByDescending(a => a.StartedAt).FirstOrDefaultAsync(ct);
        if (active is not null)
        {
            if (active.ExpiresAt is null || active.ExpiresAt.Value.Add(DeadlineGrace) > now)
                return await GetAttemptStateAsync(userId, active.Id, ct); // resume
            await GradeAndCloseAsync(active.Id, QuizAttemptStatus.Expired, ct);
        }

        var order = quiz.Questions.OrderBy(q => q.DisplayOrder).Select(q => q.QuestionId).ToList();
        if (quiz.ShuffleQuestions) order = order.OrderBy(_ => Random.Shared.Next()).ToList();
        var attempt = new QuizAttempt
        {
            QuizId = quizId, UserId = userId, Status = QuizAttemptStatus.InProgress, StartedAt = now,
            ExpiresAt = quiz.TimeLimitSeconds is { } secs ? now.AddSeconds(secs) : null,
            TotalQuestions = order.Count, MaxScore = quiz.Questions.Sum(q => q.Points), QuestionOrderJson = Json.Serialize(order)
        };
        foreach (var qid in order) attempt.Answers.Add(new QuizAttemptAnswer { QuestionId = qid });
        _db.QuizAttempts.Add(attempt);
        await _db.SaveChangesAsync(ct);
        return await GetAttemptStateAsync(userId, attempt.Id, ct);
    }

    public async Task<QuizAttemptDto> GetAttemptStateAsync(Guid userId, Guid attemptId, CancellationToken ct)
    {
        var attempt = await _db.QuizAttempts.AsNoTracking().Include(a => a.Quiz).ThenInclude(q => q.Questions)
            .Include(a => a.Answers)
            .FirstOrDefaultAsync(a => a.Id == attemptId && a.UserId == userId, ct) ?? throw new NotFoundException("Quiz attempt", attemptId);
        var order = Json.Deserialize<List<Guid>>(attempt.QuestionOrderJson) ?? new List<Guid>();
        var questions = await _db.Questions.AsNoTracking().Include(q => q.Options).Where(q => order.Contains(q.Id)).ToDictionaryAsync(q => q.Id, ct);
        var points = attempt.Quiz.Questions.ToDictionary(q => q.QuestionId, q => q.Points);
        var answers = attempt.Answers.ToDictionary(a => a.QuestionId);

        var dtos = order.Where(questions.ContainsKey).Select((id, i) =>
        {
            var q = questions[id];
            answers.TryGetValue(id, out var ans);
            return new QuizQuestionDto(id, i + 1, q.Title, q.Body, q.CodeSnippet, q.CodeLanguage, q.Type.ToString(),
                q.Type == QuestionType.MultipleSelect || q.Options.Count(o => o.IsCorrect) > 1,
                q.Options.OrderBy(o => o.DisplayOrder).Select(o => new QuestionOptionDto(o.Id, o.Text, o.DisplayOrder)).ToList(),
                points.GetValueOrDefault(id, 1m), QuizScoring.ParseIds(ans?.SelectedOptionIds), ans?.IsMarkedForReview ?? false);
        }).ToList();

        return new QuizAttemptDto(attempt.Id, attempt.QuizId, attempt.Quiz.Title, attempt.Status.ToString(), attempt.StartedAt,
            attempt.ExpiresAt, _clock.UtcNow, attempt.Quiz.TimeLimitSeconds, attempt.Quiz.NegativeMarkingFraction,
            attempt.Quiz.PassingPercentage, dtos);
    }

    public async Task SaveAnswerAsync(Guid userId, Guid attemptId, SaveQuizAnswerRequest request, CancellationToken ct)
    {
        var attempt = await _db.QuizAttempts.Include(a => a.Answers)
            .FirstOrDefaultAsync(a => a.Id == attemptId && a.UserId == userId, ct) ?? throw new NotFoundException("Quiz attempt", attemptId);
        if (attempt.Status != QuizAttemptStatus.InProgress)
            throw new ConflictException("This quiz attempt has already been submitted.");
        if (IsPastDeadline(attempt))
        {
            await GradeAndCloseAsync(attempt.Id, QuizAttemptStatus.Expired, ct);
            throw new BusinessRuleException(ErrorCodes.QuizExpired, "Time is up. The quiz was submitted automatically.");
        }
        await ApplyAnswerAsync(attempt, request, ct);
        await _db.SaveChangesAsync(ct);
    }

    private bool IsPastDeadline(QuizAttempt attempt) =>
        attempt.ExpiresAt is { } deadline && _clock.UtcNow > deadline.Add(DeadlineGrace);

    private async Task ApplyAnswerAsync(QuizAttempt attempt, SaveQuizAnswerRequest request, CancellationToken ct)
    {
        var answer = attempt.Answers.FirstOrDefault(a => a.QuestionId == request.QuestionId)
                     ?? throw ValidationAppException.For("questionId", "This question is not part of the quiz.");
        var selected = (request.SelectedOptionIds ?? Array.Empty<Guid>()).Distinct().ToList();
        if (selected.Count > 0)
        {
            var valid = await _db.QuestionOptions.Where(o => o.QuestionId == request.QuestionId).Select(o => o.Id).ToListAsync(ct);
            if (selected.Any(s => !valid.Contains(s))) throw ValidationAppException.For("selectedOptionIds", "Unknown option selected.");
        }
        answer.SelectedOptionIds = QuizScoring.ToCsv(selected);
        answer.IsMarkedForReview = request.IsMarkedForReview;
        answer.TimeSpentSeconds = Math.Clamp(request.TimeSpentSeconds, 0, 24 * 3600);
        answer.AnsweredAt = selected.Count > 0 ? _clock.UtcNow : null;
    }

    public async Task<QuizResultDto> SubmitAsync(Guid userId, Guid quizId, SubmitQuizRequest request, CancellationToken ct)
    {
        var attempt = await _db.QuizAttempts.Include(a => a.Answers)
            .FirstOrDefaultAsync(a => a.Id == request.AttemptId && a.UserId == userId && a.QuizId == quizId, ct)
            ?? throw new NotFoundException("Quiz attempt", request.AttemptId);

        if (attempt.Status != QuizAttemptStatus.InProgress)
            return await GetResultAsync(userId, attempt.Id, ct); // duplicate submit: idempotent

        var expired = IsPastDeadline(attempt);
        if (!expired && request.Answers is { Count: > 0 })
        {
            foreach (var a in request.Answers) await ApplyAnswerAsync(attempt, a, ct);
            await _db.SaveChangesAsync(ct);
        }
        await GradeAndCloseAsync(attempt.Id, expired ? QuizAttemptStatus.Expired : QuizAttemptStatus.Submitted, ct);
        return await GetResultAsync(userId, attempt.Id, ct);
    }

    /// <summary>
    /// Atomically claims the attempt (InProgress → final status) so concurrent submits grade only once, then grades it,
    /// records analytics and awards XP. Returns false if another request already closed it.
    /// </summary>
    private async Task<bool> GradeAndCloseAsync(Guid attemptId, QuizAttemptStatus finalStatus, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var claimed = await _db.QuizAttempts.Where(a => a.Id == attemptId && a.Status == QuizAttemptStatus.InProgress)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.Status, finalStatus).SetProperty(a => a.SubmittedAt, now), ct);
        if (claimed == 0) return false;
        _db.ResetTracking();

        var attempt = await _db.QuizAttempts.Include(a => a.Quiz).ThenInclude(q => q.Questions).Include(a => a.Answers)
            .FirstAsync(a => a.Id == attemptId, ct);
        var questionIds = attempt.Answers.Select(a => a.QuestionId).ToList();
        var questions = await _db.Questions.AsNoTracking().Include(q => q.Options).Where(q => questionIds.Contains(q.Id)).ToDictionaryAsync(q => q.Id, ct);
        var points = attempt.Quiz.Questions.ToDictionary(q => q.QuestionId, q => q.Points);
        var order = Json.Deserialize<List<Guid>>(attempt.QuestionOrderJson) ?? questionIds;

        var graded = new List<GradedAnswer>();
        var pointList = new List<decimal>();
        var stats = new List<(Guid, bool)>();
        foreach (var qid in order)
        {
            var answer = attempt.Answers.First(a => a.QuestionId == qid);
            if (!questions.TryGetValue(qid, out var question)) continue;
            var p = points.GetValueOrDefault(qid, 1m);
            var correct = question.Options.Where(o => o.IsCorrect).Select(o => o.Id).ToList();
            var result = QuizScoring.Grade(QuizScoring.ParseIds(answer.SelectedOptionIds), correct, p, attempt.Quiz.NegativeMarkingFraction);
            answer.IsCorrect = result.Attempted ? result.IsCorrect : null;
            answer.PointsAwarded = result.PointsAwarded;
            graded.Add(result);
            pointList.Add(p);
            if (result.Attempted)
            {
                stats.Add((question.TechnologyId, result.IsCorrect));
                _db.QuestionAttempts.Add(new QuestionAttempt
                {
                    UserId = attempt.UserId, QuestionId = qid, TechnologyId = question.TechnologyId, CategoryId = question.CategoryId,
                    Difficulty = question.Difficulty, IsCorrect = result.IsCorrect, SelectedOptionIds = answer.SelectedOptionIds,
                    TimeSpentSeconds = answer.TimeSpentSeconds, QuizAttemptId = attempt.Id
                });
            }
        }

        var summary = QuizScoring.Summarize(graded, pointList, attempt.Quiz.PassingPercentage);
        attempt.Score = summary.Score;
        attempt.MaxScore = summary.MaxScore;
        attempt.TotalQuestions = summary.Total;
        attempt.CorrectCount = summary.Correct;
        attempt.IncorrectCount = summary.Incorrect;
        attempt.SkippedCount = summary.Skipped;
        attempt.Passed = summary.Passed;
        await _db.SaveChangesAsync(ct);

        await _gamification.RecordAnswerStatsAsync(attempt.UserId, stats, ct);
        foreach (var (qid, answer) in attempt.Answers.Where(a => a.IsCorrect == true).Select(a => (a.QuestionId, a)))
            await _gamification.AwardXpAsync(attempt.UserId, XpRules.QuestionCorrect, XpSourceType.QuestionCorrect, $"q:{qid}", "Correct quiz answer", ct);
        if (summary.Attempted > 0)
            await _gamification.AwardXpAsync(attempt.UserId, XpRules.QuizCompleted, XpSourceType.QuizCompleted, $"quiz:{attempt.Id}", $"Completed {attempt.Quiz.Title}", ct);
        if (summary.Passed)
            await _gamification.AwardXpAsync(attempt.UserId, XpRules.QuizPassedBonus, XpSourceType.QuizPassed, $"quizpass:{attempt.QuizId}", $"Passed {attempt.Quiz.Title}", ct);
        var duration = (int)Math.Min((now - attempt.StartedAt).TotalSeconds, attempt.Quiz.TimeLimitSeconds ?? 4 * 3600);
        await _gamification.RecordActivityAsync(attempt.UserId, ActivityType.QuizCompleted, attempt.Id, attempt.Quiz.TechnologyId, duration, ct);
        _logger.LogInformation("Quiz attempt {AttemptId} closed as {Status} with {Percentage}%", attempt.Id, finalStatus, summary.Percentage);
        return true;
    }

    public async Task<QuizResultDto> GetResultAsync(Guid userId, Guid attemptId, CancellationToken ct)
    {
        var attempt = await _db.QuizAttempts.AsNoTracking().Include(a => a.Quiz).Include(a => a.Answers)
            .FirstOrDefaultAsync(a => a.Id == attemptId && a.UserId == userId, ct) ?? throw new NotFoundException("Quiz attempt", attemptId);
        if (attempt.Status == QuizAttemptStatus.InProgress)
        {
            if (!IsPastDeadline(attempt)) throw new ConflictException("This attempt is still in progress.");
            await GradeAndCloseAsync(attempt.Id, QuizAttemptStatus.Expired, ct);
            return await GetResultAsync(userId, attemptId, ct);
        }

        var order = Json.Deserialize<List<Guid>>(attempt.QuestionOrderJson) ?? new List<Guid>();
        var questions = await _db.Questions.AsNoTracking().Include(q => q.Options).Include(q => q.Answer).Include(q => q.Category)
            .Where(q => order.Contains(q.Id)).ToDictionaryAsync(q => q.Id, ct);
        var answers = attempt.Answers.ToDictionary(a => a.QuestionId);

        var review = new List<QuizReviewItemDto>();
        foreach (var (qid, index) in order.Select((id, i) => (id, i)))
        {
            if (!questions.TryGetValue(qid, out var q)) continue;
            answers.TryGetValue(qid, out var ans);
            var selected = QuizScoring.ParseIds(ans?.SelectedOptionIds).ToHashSet();
            review.Add(new QuizReviewItemDto(qid, index + 1, q.Title, q.Body, q.CodeSnippet,
                q.Options.OrderBy(o => o.DisplayOrder).Select(o => new ReviewOptionDto(o.Id, o.Text, o.IsCorrect, selected.Contains(o.Id), o.Explanation)).ToList(),
                ans?.IsCorrect, selected.Count == 0, ans?.PointsAwarded ?? 0,
                q.Answer?.DetailedExplanation ?? q.Answer?.ShortAnswer ?? string.Empty, q.Category.Name));
        }

        var breakdown = review.Where(r => questions.ContainsKey(r.QuestionId))
            .GroupBy(r => (questions[r.QuestionId].CategoryId, r.Category))
            .Select(g => new TopicPerformanceDto(g.Key.Category, g.Key.CategoryId, g.Count(r => r.IsCorrect == true), g.Count(),
                Math.Round(g.Count(r => r.IsCorrect == true) * 100m / g.Count(), 1)))
            .OrderBy(t => t.Accuracy).ToList();
        var recommended = breakdown.Where(b => b.Accuracy < 60).Select(b => b.Name).ToList();

        var xp = await _db.UserXpTransactions.Where(x => x.UserId == userId &&
                ((x.SourceType == XpSourceType.QuizCompleted && x.SourceKey == $"quiz:{attempt.Id}") ||
                 (x.SourceType == XpSourceType.QuizPassed && x.SourceKey == $"quizpass:{attempt.QuizId}" && x.CreatedAt >= attempt.StartedAt)))
            .SumAsync(x => (int?)x.Amount, ct) ?? 0;

        var attempted = attempt.CorrectCount + attempt.IncorrectCount;
        var end = attempt.SubmittedAt ?? _clock.UtcNow;
        var totalSeconds = (int)(end - attempt.StartedAt).TotalSeconds;
        if (attempt.Quiz.TimeLimitSeconds is { } limit) totalSeconds = Math.Min(totalSeconds, limit);
        return new QuizResultDto(attempt.Id, attempt.QuizId, attempt.Quiz.Title, attempt.Status.ToString(), attempt.TotalQuestions,
            attempted, attempt.CorrectCount, attempt.IncorrectCount, attempt.SkippedCount,
            attempted == 0 ? 0 : Math.Round(attempt.CorrectCount * 100m / attempted, 1), attempt.Score, attempt.MaxScore,
            attempt.MaxScore == 0 ? 0 : Math.Round(attempt.Score * 100m / attempt.MaxScore, 1), attempt.Passed,
            attempt.Quiz.PassingPercentage, Math.Max(0, totalSeconds), attempt.StartedAt, attempt.SubmittedAt, review, breakdown,
            recommended, xp);
    }

    public async Task<PagedResult<QuizAttemptSummaryDto>> GetHistoryAsync(Guid userId, PageQuery query, CancellationToken ct) =>
        await _db.QuizAttempts.AsNoTracking().Where(a => a.UserId == userId && a.Status != QuizAttemptStatus.InProgress)
            .OrderByDescending(a => a.StartedAt)
            .Select(a => new QuizAttemptSummaryDto(a.Id, a.QuizId, a.Quiz.Title, a.Status.ToString(),
                a.MaxScore == 0 ? 0 : Math.Round(a.Score * 100 / a.MaxScore, 1), a.Passed, a.CorrectCount, a.TotalQuestions, a.StartedAt, a.SubmittedAt))
            .ToPagedAsync(query, ct);

    public async Task<int> FinalizeExpiredAttemptsAsync(CancellationToken ct)
    {
        var cutoff = _clock.UtcNow - DeadlineGrace;
        var ids = await _db.QuizAttempts.Where(a => a.Status == QuizAttemptStatus.InProgress && a.ExpiresAt != null && a.ExpiresAt < cutoff)
            .Select(a => a.Id).Take(200).ToListAsync(ct);
        var closed = 0;
        foreach (var id in ids)
            if (await GradeAndCloseAsync(id, QuizAttemptStatus.Expired, ct)) closed++;
        return closed;
    }
}

public sealed class CreateQuizRequestValidator : AbstractValidator<CreateQuizRequest>
{
    public CreateQuizRequestValidator()
    {
        RuleFor(x => x.Title).MaximumLength(200);
        RuleFor(x => x.Mode).NotEmpty();
        RuleFor(x => x.QuestionCount).InclusiveBetween(3, 100);
        RuleFor(x => x.TimeLimitSeconds).InclusiveBetween(60, 4 * 3600).When(x => x.TimeLimitSeconds is not null);
        RuleFor(x => x.NegativeMarkingFraction).InclusiveBetween(0m, 1m);
        RuleFor(x => x.PassingPercentage).InclusiveBetween(1, 100);
        RuleFor(x => x.TechnologyIds).Must(l => l is null || l.Count <= 20);
        RuleFor(x => x.CategoryIds).Must(l => l is null || l.Count <= 50);
    }
}

public sealed class SaveQuizAnswerRequestValidator : AbstractValidator<SaveQuizAnswerRequest>
{
    public SaveQuizAnswerRequestValidator()
    {
        RuleFor(x => x.QuestionId).NotEmpty();
        RuleFor(x => x.SelectedOptionIds).Must(l => l is null || l.Count <= 10);
        RuleFor(x => x.TimeSpentSeconds).GreaterThanOrEqualTo(0);
    }
}

public sealed class SubmitQuizRequestValidator : AbstractValidator<SubmitQuizRequest>
{
    public SubmitQuizRequestValidator()
    {
        RuleFor(x => x.AttemptId).NotEmpty();
        RuleFor(x => x.Answers).Must(l => l is null || l.Count <= 200);
        RuleForEach(x => x.Answers).SetValidator(new SaveQuizAnswerRequestValidator());
    }
}
