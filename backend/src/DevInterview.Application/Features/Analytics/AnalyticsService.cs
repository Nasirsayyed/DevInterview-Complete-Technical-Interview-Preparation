using DevInterview.Application.Abstractions;
using DevInterview.Application.Common;
using DevInterview.Application.Features.Coding;
using DevInterview.Application.Features.Gamification;
using DevInterview.Application.Features.Learning;
using DevInterview.Application.Features.Subscriptions;
using DevInterview.Contracts.Engagement;
using DevInterview.Contracts.Practice;
using DevInterview.Domain.Enums;
using DevInterview.Domain.Rules;
using Microsoft.EntityFrameworkCore;

namespace DevInterview.Application.Features.Analytics;

public interface IAnalyticsService
{
    Task<AnalyticsOverviewDto> GetOverviewAsync(Guid userId, CancellationToken ct);
    Task<IReadOnlyList<TechnologyAnalyticsDto>> GetTechnologyAsync(Guid userId, CancellationToken ct);
    Task<IReadOnlyList<PeriodActivityDto>> GetWeeklyAsync(Guid userId, int weeks, CancellationToken ct);
    Task<IReadOnlyList<PeriodActivityDto>> GetMonthlyAsync(Guid userId, int months, CancellationToken ct);
    Task<IReadOnlyList<ActivityDayDto>> GetDailyActivityAsync(Guid userId, int days, CancellationToken ct);
    Task<IReadOnlyList<SkillProgressDto>> GetSkillProgressAsync(Guid userId, CancellationToken ct);
    Task<int> GetPreparationScoreAsync(Guid userId, CancellationToken ct);
}

public sealed class AnalyticsService : IAnalyticsService
{
    public const int WeakThreshold = 60;
    public const int StrongThreshold = 80;
    public const int MinAttemptsForTopic = 5;

    private readonly IAppDbContext _db;
    private readonly IClock _clock;

    public AnalyticsService(IAppDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    private sealed record Totals(int Attempted, int Correct, int ChallengesAttempted, int ChallengesSolved, int Mocks, double MockAvg, int Lessons, int LessonsAvailable);

    private async Task<Totals> LoadTotalsAsync(Guid userId, CancellationToken ct)
    {
        var graded = _db.QuestionAttempts.Where(a => a.UserId == userId && a.IsCorrect != null);
        var attempted = await graded.CountAsync(ct);
        var correct = await graded.CountAsync(a => a.IsCorrect == true, ct);
        var challengesAttempted = await _db.CodeSubmissions.Where(s => s.UserId == userId && s.Kind == SubmissionKind.Submit).Select(s => s.ChallengeId).Distinct().CountAsync(ct);
        var solved = await _db.CodeSubmissions.Where(s => s.UserId == userId && s.Kind == SubmissionKind.Submit && s.Status == SubmissionStatus.Accepted)
            .Select(s => s.ChallengeId).Distinct().CountAsync(ct);
        var mockScores = await _db.MockInterviewEvaluations.Where(e => e.Interview.UserId == userId).Select(e => e.OverallScore).ToListAsync(ct);
        var lessons = await _db.UserLearningProgress.CountAsync(p => p.UserId == userId && p.Status == ProgressStatus.Completed, ct);
        // Learning completion is measured against the paths the user has started.
        var startedPaths = await _db.UserLearningProgress.Where(p => p.UserId == userId).Select(p => p.PathId).Distinct().ToListAsync(ct);
        var available = startedPaths.Count == 0 ? 0 : await _db.LearningLessons.CountAsync(l => startedPaths.Contains(l.Module.PathId), ct);
        return new Totals(attempted, correct, challengesAttempted, solved, mockScores.Count, mockScores.Count == 0 ? 0 : mockScores.Average(), lessons, available);
    }

    private static PreparationInputs Inputs(Totals t) =>
        new(t.Attempted, t.Correct, t.ChallengesAttempted, t.ChallengesSolved, t.Mocks, t.MockAvg, t.Lessons, t.LessonsAvailable);

    public async Task<int> GetPreparationScoreAsync(Guid userId, CancellationToken ct) =>
        PreparationScore.Calculate(Inputs(await LoadTotalsAsync(userId, ct)));

    public async Task<AnalyticsOverviewDto> GetOverviewAsync(Guid userId, CancellationToken ct)
    {
        var t = await LoadTotalsAsync(userId, ct);
        var inputs = Inputs(t);
        var components = new ScoreComponentsDto(
            PreparationScore.Calculate(inputs with { ChallengesAttempted = 0, MockInterviewsCompleted = 0, LessonsAvailable = 0 }) * 100 / 40,
            PreparationScore.Calculate(inputs with { QuestionsAttempted = 0, MockInterviewsCompleted = 0, LessonsAvailable = 0 }) * 100 / 20,
            PreparationScore.Calculate(inputs with { QuestionsAttempted = 0, ChallengesAttempted = 0, LessonsAvailable = 0 }) * 100 / 25,
            PreparationScore.Calculate(inputs with { QuestionsAttempted = 0, ChallengesAttempted = 0, MockInterviewsCompleted = 0 }) * 100 / 15);

        var byDifficulty = await _db.QuestionAttempts.Where(a => a.UserId == userId && a.IsCorrect != null)
            .GroupBy(a => a.Difficulty).Select(g => new { g.Key, Total = g.Count(), Correct = g.Count(a => a.IsCorrect == true) }).ToListAsync(ct);
        var difficulty = Enum.GetValues<Difficulty>().Select(d =>
        {
            var row = byDifficulty.FirstOrDefault(r => r.Key == d);
            return new DifficultyPerformanceDto(d.ToString(), row?.Total ?? 0, row?.Correct ?? 0, Pct(row?.Correct ?? 0, row?.Total ?? 0));
        }).ToList();

        var quizzes = await _db.QuizAttempts.Where(a => a.UserId == userId && a.Status != QuizAttemptStatus.InProgress && a.MaxScore > 0)
            .Select(a => new { a.Score, a.MaxScore, a.Passed }).ToListAsync(ct);
        var quizStats = new QuizStatsDto(quizzes.Count,
            quizzes.Count == 0 ? 0 : Math.Round(quizzes.Average(q => q.Score / q.MaxScore * 100m), 1), Pct(quizzes.Count(q => q.Passed), quizzes.Count));

        var submissions = await _db.CodeSubmissions.CountAsync(s => s.UserId == userId && s.Kind == SubmissionKind.Submit, ct);
        var coding = new CodingStatsDto(t.ChallengesAttempted, t.ChallengesSolved, Pct(t.ChallengesSolved, t.ChallengesAttempted), submissions);

        var evals = await _db.MockInterviewEvaluations.Where(e => e.Interview.UserId == userId).OrderBy(e => e.CreatedAt)
            .Select(e => new { e.CreatedAt, e.OverallScore, e.TechnicalScore, e.CommunicationScore, e.ProblemSolvingScore }).ToListAsync(ct);
        var mock = new MockStatsDto(evals.Count, Avg(evals.Select(e => e.OverallScore)), Avg(evals.Select(e => e.TechnicalScore)),
            Avg(evals.Select(e => e.CommunicationScore)), Avg(evals.Select(e => e.ProblemSolvingScore)),
            evals.TakeLast(12).Select(e => new ScorePointDto(e.CreatedAt, e.OverallScore)).ToList());

        var learningSeconds = await _db.UserActivityLogs.Where(a => a.UserId == userId).SumAsync(a => (int?)a.DurationSeconds, ct) ?? 0;

        var categories = await _db.QuestionAttempts.Where(a => a.UserId == userId && a.IsCorrect != null)
            .GroupBy(a => a.CategoryId).Select(g => new { g.Key, Total = g.Count(), Correct = g.Count(a => a.IsCorrect == true) })
            .Where(g => g.Total >= MinAttemptsForTopic).ToListAsync(ct);
        var catIds = categories.Select(c => c.Key).ToList();
        var catNames = await _db.Categories.Where(c => catIds.Contains(c.Id)).Select(c => new { c.Id, Name = c.Technology.Name + ": " + c.Name }).ToDictionaryAsync(c => c.Id, c => c.Name, ct);
        var topics = categories.Select(c => new TopicPerformanceDto(catNames.GetValueOrDefault(c.Key, "Unknown"), c.Key, c.Correct, c.Total, Pct(c.Correct, c.Total))).ToList();
        var weak = topics.Where(x => x.Accuracy < WeakThreshold).OrderBy(x => x.Accuracy).Take(6).ToList();
        var strong = topics.Where(x => x.Accuracy >= StrongThreshold).OrderByDescending(x => x.Accuracy).Take(6).ToList();

        var recommendations = await BuildRecommendationsAsync(userId, t, weak, difficulty, ct);
        return new AnalyticsOverviewDto(PreparationScore.Calculate(inputs), components, t.Attempted, Pct(t.Correct, t.Attempted), difficulty,
            quizStats, coding, mock, learningSeconds / 60, weak, strong, recommendations);
    }

    private async Task<IReadOnlyList<RecommendationDto>> BuildRecommendationsAsync(Guid userId, Totals t, IReadOnlyList<TopicPerformanceDto> weak,
        IReadOnlyList<DifficultyPerformanceDto> difficulty, CancellationToken ct)
    {
        var list = new List<RecommendationDto>();
        foreach (var w in weak.Take(3))
            list.Add(new RecommendationDto($"Revise {w.Name}", $"Your accuracy is {w.Accuracy:0}% over {w.Total} questions.", "category", w.Id));
        if (t.Attempted < 20)
            list.Add(new RecommendationDto("Build a baseline", "Answer at least 20 questions so your analytics become reliable.", "practice", null));
        var nextLevel = difficulty.FirstOrDefault(d => d.Attempted >= 10 && d.Accuracy >= StrongThreshold);
        if (nextLevel is not null && Enum.TryParse<Difficulty>(nextLevel.Difficulty, out var dl) && dl < Difficulty.Expert)
            list.Add(new RecommendationDto($"Try {dl + 1} questions", $"You score {nextLevel.Accuracy:0}% on {dl} questions.", "difficulty", null));
        if (t.ChallengesAttempted == 0)
            list.Add(new RecommendationDto("Solve your first coding challenge", "Coding rounds are part of most technical interviews.", "challenge", null));
        else if (t.ChallengesAttempted > 0 && t.ChallengesSolved * 100 / t.ChallengesAttempted < 50)
            list.Add(new RecommendationDto("Focus on Easy challenges", "Your coding success rate is below 50%; consolidate fundamentals first.", "challenge", null));
        if (t.Mocks == 0)
            list.Add(new RecommendationDto("Take a mock interview", "Practising out loud improves communication scores.", "mockInterview", null));
        var dueCards = await _db.FlashcardReviews.CountAsync(r => r.UserId == userId && r.DueAt <= _clock.UtcNow, ct);
        if (dueCards > 0)
            list.Add(new RecommendationDto($"Review {dueCards} due flashcards", "Spaced repetition works best when reviews happen on time.", "flashcards", null));
        return list.Take(6).ToList();
    }

    public async Task<IReadOnlyList<TechnologyAnalyticsDto>> GetTechnologyAsync(Guid userId, CancellationToken ct)
    {
        var rows = await _db.QuestionAttempts.Where(a => a.UserId == userId && a.IsCorrect != null)
            .GroupBy(a => new { a.TechnologyId, a.CategoryId }).Select(g => new { g.Key.TechnologyId, g.Key.CategoryId, Total = g.Count(), Correct = g.Count(a => a.IsCorrect == true) })
            .ToListAsync(ct);
        var techIds = rows.Select(r => r.TechnologyId).Distinct().ToList();
        var catIds = rows.Select(r => r.CategoryId).Distinct().ToList();
        var techs = await _db.Technologies.Where(t => techIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.Name, ct);
        var cats = await _db.Categories.Where(c => catIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name, ct);
        return rows.GroupBy(r => r.TechnologyId).Select(g => new TechnologyAnalyticsDto(g.Key, techs.GetValueOrDefault(g.Key, "Unknown"),
                g.Sum(r => r.Total), g.Sum(r => r.Correct), Pct(g.Sum(r => r.Correct), g.Sum(r => r.Total)),
                g.Select(r => new CategoryPerformanceDto(r.CategoryId, cats.GetValueOrDefault(r.CategoryId, "Unknown"), r.Total, r.Correct, Pct(r.Correct, r.Total)))
                    .OrderBy(c => c.Accuracy).ToList()))
            .OrderByDescending(t => t.Attempted).ToList();
    }

    private async Task<DateOnly> TodayAsync(Guid userId, CancellationToken ct)
    {
        var tz = await _db.UserPreferences.Where(p => p.UserId == userId).Select(p => p.TimeZoneId).FirstOrDefaultAsync(ct);
        return TimeZones.LocalDate(_clock.UtcNow, tz);
    }

    public async Task<IReadOnlyList<ActivityDayDto>> GetDailyActivityAsync(Guid userId, int days, CancellationToken ct)
    {
        days = Math.Clamp(days, 1, 366);
        var today = await TodayAsync(userId, ct);
        var from = today.AddDays(-(days - 1));
        var logs = await _db.UserActivityLogs.AsNoTracking().Where(a => a.UserId == userId && a.LocalDate >= from)
            .Select(a => new { a.LocalDate, a.ActivityType, a.DurationSeconds, a.EntityId }).ToListAsync(ct);
        var fromUtc = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddDays(-1);
        var tz = await _db.UserPreferences.Where(p => p.UserId == userId).Select(p => p.TimeZoneId).FirstOrDefaultAsync(ct);
        var attempts = (await _db.QuestionAttempts.AsNoTracking().Where(a => a.UserId == userId && a.CreatedAt >= fromUtc)
            .Select(a => new { a.CreatedAt, a.IsCorrect }).ToListAsync(ct)).Select(a => new { Date = TimeZones.LocalDate(a.CreatedAt, tz), a.IsCorrect }).ToList();
        var xp = (await _db.UserXpTransactions.AsNoTracking().Where(x => x.UserId == userId && x.CreatedAt >= fromUtc)
            .Select(x => new { x.CreatedAt, x.Amount }).ToListAsync(ct)).Select(x => new { Date = TimeZones.LocalDate(x.CreatedAt, tz), x.Amount }).ToList();

        return Enumerable.Range(0, days).Select(i => from.AddDays(i)).Select(d => new ActivityDayDto(d,
            attempts.Count(a => a.Date == d), attempts.Count(a => a.Date == d && a.IsCorrect == true),
            logs.Count(l => l.LocalDate == d && l.ActivityType == ActivityType.QuizCompleted),
            logs.Count(l => l.LocalDate == d && l.ActivityType is ActivityType.ChallengeSolved or ActivityType.ChallengeSubmitted),
            logs.Count(l => l.LocalDate == d && l.ActivityType == ActivityType.LessonCompleted && l.EntityId != null),
            logs.Where(l => l.LocalDate == d).Sum(l => l.DurationSeconds) / 60, xp.Where(x => x.Date == d).Sum(x => x.Amount))).ToList();
    }

    public async Task<IReadOnlyList<PeriodActivityDto>> GetWeeklyAsync(Guid userId, int weeks, CancellationToken ct)
    {
        weeks = Math.Clamp(weeks, 1, 52);
        var daily = await GetDailyActivityAsync(userId, weeks * 7, ct);
        return daily.GroupBy(d => TimeZones.WeekStart(d.Date)).OrderBy(g => g.Key)
            .Select(g => new PeriodActivityDto(g.Key, g.Sum(d => d.Questions), g.Sum(d => d.Correct), g.Sum(d => d.Minutes), g.Sum(d => d.Xp), g.Sum(d => d.Challenges), g.Sum(d => d.Quizzes)))
            .TakeLast(weeks).ToList();
    }

    public async Task<IReadOnlyList<PeriodActivityDto>> GetMonthlyAsync(Guid userId, int months, CancellationToken ct)
    {
        months = Math.Clamp(months, 1, 12);
        var daily = await GetDailyActivityAsync(userId, months * 31, ct);
        return daily.GroupBy(d => new DateOnly(d.Date.Year, d.Date.Month, 1)).OrderBy(g => g.Key)
            .Select(g => new PeriodActivityDto(g.Key, g.Sum(d => d.Questions), g.Sum(d => d.Correct), g.Sum(d => d.Minutes), g.Sum(d => d.Xp), g.Sum(d => d.Challenges), g.Sum(d => d.Quizzes)))
            .TakeLast(months).ToList();
    }

    public async Task<IReadOnlyList<SkillProgressDto>> GetSkillProgressAsync(Guid userId, CancellationToken ct)
    {
        var stats = await _db.UserPerformanceStats.AsNoTracking().Where(s => s.UserId == userId && s.Attempted > 0)
            .Select(s => new { s.TechnologyId, s.Technology.Name, s.Attempted, s.Correct }).ToListAsync(ct);
        return stats.Select(s =>
        {
            var accuracy = Pct(s.Correct, s.Attempted);
            var proficiency = s.Attempted < MinAttemptsForTopic ? "Getting started" : accuracy >= 85 ? "Strong" : accuracy >= 65 ? "Competent" : "Needs practice";
            return new SkillProgressDto(s.TechnologyId, s.Name, s.Attempted, s.Correct, accuracy, proficiency);
        }).OrderByDescending(s => s.Attempted).ToList();
    }

    internal static decimal Pct(int part, int total) => total == 0 ? 0 : Math.Round(part * 100m / total, 1);
    private static int Avg(IEnumerable<int> values) { var l = values.ToList(); return l.Count == 0 ? 0 : (int)Math.Round(l.Average()); }
}

public interface IDashboardService
{
    Task<DashboardSummaryDto> GetSummaryAsync(Guid userId, CancellationToken ct);
}

public sealed class DashboardService : IDashboardService
{
    private readonly IAppDbContext _db;
    private readonly IAnalyticsService _analytics;
    private readonly IGamificationService _gamification;
    private readonly ILearningService _learning;
    private readonly ICodingService _coding;
    private readonly IEntitlementService _entitlements;
    private readonly IClock _clock;

    public DashboardService(IAppDbContext db, IAnalyticsService analytics, IGamificationService gamification, ILearningService learning,
        ICodingService coding, IEntitlementService entitlements, IClock clock)
    {
        _db = db;
        _analytics = analytics;
        _gamification = gamification;
        _learning = learning;
        _coding = coding;
        _entitlements = entitlements;
        _clock = clock;
    }

    public async Task<DashboardSummaryDto> GetSummaryAsync(Guid userId, CancellationToken ct)
    {
        var user = await _db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => new { u.FirstName, u.ProfilePhotoUrl }).FirstOrDefaultAsync(ct)
                   ?? throw new NotFoundException("User");
        var pref = await _db.UserPreferences.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId, ct);
        var tier = await _entitlements.GetTierAsync(userId, ct);
        var xp = await _gamification.GetTotalXpAsync(userId, ct);
        var (streak, longest) = await _gamification.GetStreakAsync(userId, ct);

        var graded = _db.QuestionAttempts.Where(a => a.UserId == userId && a.IsCorrect != null);
        var attempted = await graded.CountAsync(ct);
        var correct = await graded.CountAsync(a => a.IsCorrect == true, ct);
        var solved = await _db.CodeSubmissions.Where(s => s.UserId == userId && s.Kind == SubmissionKind.Submit && s.Status == SubmissionStatus.Accepted)
            .Select(s => s.ChallengeId).Distinct().CountAsync(ct);
        var mockScores = await _db.MockInterviewEvaluations.Where(e => e.Interview.UserId == userId).Select(e => e.OverallScore).ToListAsync(ct);
        var totalSeconds = await _db.UserActivityLogs.Where(a => a.UserId == userId).SumAsync(a => (int?)a.DurationSeconds, ct) ?? 0;
        var today = TimeZones.LocalDate(_clock.UtcNow, pref?.TimeZoneId);
        var weekStart = TimeZones.WeekStart(today);
        var weekSeconds = await _db.UserActivityLogs.Where(a => a.UserId == userId && a.LocalDate >= weekStart).SumAsync(a => (int?)a.DurationSeconds, ct) ?? 0;
        var todayQuestions = await _db.UserActivityLogs.CountAsync(a => a.UserId == userId && a.LocalDate == today && a.ActivityType == ActivityType.QuestionAttempted, ct)
                             + await _db.QuestionAttempts.CountAsync(a => a.UserId == userId && a.QuizAttemptId != null && a.CreatedAt >= _clock.UtcNow.Date, ct);

        var paths = await _learning.GetPathsAsync(userId, null, null, ct);
        var inProgress = paths.Where(p => p.CompletedLessons > 0 && p.ProgressPercent < 100).ToList();
        var continueLearning = new List<ContinueLearningDto>();
        foreach (var p in inProgress.Take(3))
        {
            var detail = await _learning.GetPathAsync(userId, p.Id, ct);
            var next = detail.Modules.SelectMany(m => m.Lessons).FirstOrDefault(l => l.Id == detail.NextLessonId);
            continueLearning.Add(new ContinueLearningDto(p.Id, p.Title, next?.Id, next?.Title, p.ProgressPercent));
        }
        var recommended = await _learning.GetRecommendedAsync(userId, 4, ct);

        var recent = await _db.QuestionAttempts.AsNoTracking().Where(a => a.UserId == userId).OrderByDescending(a => a.CreatedAt).Take(6)
            .Select(a => new RecentQuestionDto(a.QuestionId, a.Question.Title, a.Question.Technology.Name, a.IsCorrect, a.CreatedAt)).ToListAsync(ct);
        var now = _clock.UtcNow;
        var reminders = await _db.InterviewReminders.AsNoTracking().Where(r => r.UserId == userId && r.ScheduledAt >= now).OrderBy(r => r.ScheduledAt).Take(3)
            .Select(r => new InterviewReminderDto(r.Id, r.Title, r.CompanyName, r.ScheduledAt, r.Notes)).ToListAsync(ct);
        var unread = await _db.Notifications.CountAsync(n => n.UserId == userId && n.ReadAt == null, ct);

        return new DashboardSummaryDto(user.FirstName, user.ProfilePhotoUrl, tier.ToString(), GamificationService.ToLevelDto(xp), streak, longest,
            await _analytics.GetPreparationScoreAsync(userId, ct), attempted, correct, attempted - correct, solved, mockScores.Count,
            mockScores.Count == 0 ? 0 : (int)Math.Round(mockScores.Average()), totalSeconds / 60, weekSeconds / 60, pref?.WeeklyGoalMinutes ?? 180,
            todayQuestions, pref?.DailyQuestionGoal ?? 10, continueLearning, recommended, recent,
            await _coding.GetDailyChallengeAsync(userId, ct), reminders, unread);
    }
}
