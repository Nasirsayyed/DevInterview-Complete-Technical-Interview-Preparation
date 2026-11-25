using DevInterview.Application.Abstractions;
using DevInterview.Application.Common;
using DevInterview.Contracts.Engagement;
using DevInterview.Domain.Entities;
using DevInterview.Domain.Enums;
using DevInterview.Domain.Rules;
using Microsoft.EntityFrameworkCore;

namespace DevInterview.Application.Features.Gamification;

public interface IGamificationService
{
    /// <summary>Adds XP once per (source type, source key). Returns the XP actually granted (0 for duplicates).</summary>
    Task<int> AwardXpAsync(Guid userId, int amount, XpSourceType source, string sourceKey, string reason, CancellationToken ct);
    /// <summary>Logs an activity, updates the streak and weekly goal, and evaluates achievements.</summary>
    Task<int> RecordActivityAsync(Guid userId, ActivityType type, Guid? entityId, Guid? technologyId, int durationSeconds, CancellationToken ct);
    Task RecordAnswerStatsAsync(Guid userId, IReadOnlyList<(Guid TechnologyId, bool Correct)> answers, CancellationToken ct);
    Task<IReadOnlyList<string>> EvaluateAchievementsAsync(Guid userId, CancellationToken ct);
    Task<GamificationProfileDto> GetProfileAsync(Guid userId, CancellationToken ct);
    Task<LeaderboardDto> GetLeaderboardAsync(Guid userId, string period, int top, CancellationToken ct);
    Task<int> GetTotalXpAsync(Guid userId, CancellationToken ct);
    Task<(int Current, int Longest)> GetStreakAsync(Guid userId, CancellationToken ct);
}

public sealed class GamificationService : IGamificationService
{
    private static readonly string[] SqlTechnologySlugs = { "sql-server", "mysql", "postgresql", "sql" };
    private readonly IAppDbContext _db;
    private readonly IClock _clock;

    public GamificationService(IAppDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<int> AwardXpAsync(Guid userId, int amount, XpSourceType source, string sourceKey, string reason, CancellationToken ct)
    {
        if (amount <= 0) return 0;
        var exists = await _db.UserXpTransactions.AnyAsync(x => x.UserId == userId && x.SourceType == source && x.SourceKey == sourceKey, ct);
        if (exists) return 0;

        var tx = new UserXpTransaction { UserId = userId, Amount = amount, SourceType = source, SourceKey = sourceKey, Reason = reason };
        _db.UserXpTransactions.Add(tx);
        try
        {
            await _db.SaveChangesAsync(ct);
            return amount;
        }
        catch (DbUpdateException)
        {
            // Unique index (UserId, SourceType, SourceKey) rejected a concurrent duplicate.
            _db.UserXpTransactions.Remove(tx);
            return 0;
        }
    }

    public async Task<int> RecordActivityAsync(Guid userId, ActivityType type, Guid? entityId, Guid? technologyId, int durationSeconds, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var tz = await _db.UserPreferences.Where(p => p.UserId == userId).Select(p => p.TimeZoneId).FirstOrDefaultAsync(ct);
        var today = TimeZones.LocalDate(now, tz);

        _db.UserActivityLogs.Add(new UserActivityLog
        {
            UserId = userId, ActivityType = type, EntityId = entityId, TechnologyId = technologyId,
            DurationSeconds = Math.Clamp(durationSeconds, 0, 4 * 3600), OccurredAt = now, LocalDate = today
        });

        var streak = await _db.UserStreaks.FirstOrDefaultAsync(s => s.UserId == userId, ct);
        if (streak is null)
        {
            streak = new UserStreak { UserId = userId };
            _db.UserStreaks.Add(streak);
        }
        var update = StreakCalculator.RegisterActivity(streak.CurrentStreak, streak.LongestStreak, streak.LastActiveDate, today);
        streak.CurrentStreak = update.CurrentStreak;
        streak.LongestStreak = update.LongestStreak;
        streak.LastActiveDate = update.LastActiveDate;
        await _db.SaveChangesAsync(ct);

        var bonus = 0;
        if (update.Incremented && update.CurrentStreak > 0 && update.CurrentStreak % 7 == 0)
        {
            bonus += await AwardXpAsync(userId, XpRules.SevenDayStreakBonus, XpSourceType.DailyStreak,
                $"streak:{today:yyyy-MM-dd}", $"{update.CurrentStreak}-day streak", ct);
        }

        bonus += await CheckWeeklyGoalAsync(userId, today, ct);
        await EvaluateAchievementsAsync(userId, ct);
        return bonus;
    }

    private async Task<int> CheckWeeklyGoalAsync(Guid userId, DateOnly today, CancellationToken ct)
    {
        var weekStart = TimeZones.WeekStart(today);
        var target = await _db.UserPreferences.Where(p => p.UserId == userId).Select(p => p.WeeklyGoalMinutes).FirstOrDefaultAsync(ct);
        if (target <= 0) return 0;
        var seconds = await _db.UserActivityLogs.Where(a => a.UserId == userId && a.LocalDate >= weekStart)
            .SumAsync(a => (int?)a.DurationSeconds, ct) ?? 0;
        if (seconds / 60 < target) return 0;
        return await AwardXpAsync(userId, XpRules.WeeklyGoalBonus, XpSourceType.WeeklyGoal, $"week:{weekStart:yyyy-MM-dd}", "Weekly goal reached", ct);
    }

    public async Task RecordAnswerStatsAsync(Guid userId, IReadOnlyList<(Guid TechnologyId, bool Correct)> answers, CancellationToken ct)
    {
        if (answers.Count == 0) return;
        var techIds = answers.Select(a => a.TechnologyId).Distinct().ToList();
        var stats = await _db.UserPerformanceStats.Where(s => s.UserId == userId && techIds.Contains(s.TechnologyId)).ToListAsync(ct);
        foreach (var group in answers.GroupBy(a => a.TechnologyId))
        {
            var stat = stats.FirstOrDefault(s => s.TechnologyId == group.Key);
            if (stat is null)
            {
                stat = new UserPerformanceStat { UserId = userId, TechnologyId = group.Key };
                _db.UserPerformanceStats.Add(stat);
            }
            stat.Attempted += group.Count();
            stat.Correct += group.Count(a => a.Correct);
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<string>> EvaluateAchievementsAsync(Guid userId, CancellationToken ct)
    {
        var definitions = await _db.Achievements.Where(a => a.IsActive).ToListAsync(ct);
        if (definitions.Count == 0) return Array.Empty<string>();
        var earned = await _db.UserAchievements.Where(u => u.UserId == userId).Select(u => u.AchievementId).ToListAsync(ct);
        var pending = definitions.Where(d => !earned.Contains(d.Id)).ToList();
        if (pending.Count == 0) return Array.Empty<string>();

        var stats = await _db.UserPerformanceStats.Where(s => s.UserId == userId)
            .Select(s => new { s.Technology.Slug, s.Attempted, s.Correct }).ToListAsync(ct);
        var solved = await _db.CodeSubmissions.Where(s => s.UserId == userId && s.Kind == SubmissionKind.Submit && s.Status == SubmissionStatus.Accepted)
            .Select(s => s.ChallengeId).Distinct().CountAsync(ct);
        var streak = await _db.UserStreaks.Where(s => s.UserId == userId).Select(s => s.LongestStreak).FirstOrDefaultAsync(ct);
        var quizzes = await _db.QuizAttempts.Where(a => a.UserId == userId && a.Status != QuizAttemptStatus.InProgress)
            .Select(a => new { a.Score, a.MaxScore, a.TotalQuestions }).ToListAsync(ct);
        var mocks = await _db.MockInterviewEvaluations.Where(e => e.Interview.UserId == userId).Select(e => e.OverallScore).ToListAsync(ct);
        var lessons = await _db.UserLearningProgress.CountAsync(p => p.UserId == userId && p.Status == ProgressStatus.Completed, ct);

        bool Meets(string code) => code switch
        {
            AchievementCodes.FirstQuiz => quizzes.Count > 0,
            AchievementCodes.QuizAce => quizzes.Any(q => q.TotalQuestions >= 5 && q.MaxScore > 0 && q.Score >= q.MaxScore),
            AchievementCodes.SqlMaster => stats.Where(s => SqlTechnologySlugs.Contains(s.Slug)).Sum(s => s.Correct) >= AchievementCodes.SqlMasterCorrect,
            AchievementCodes.CSharpBeginner => stats.Where(s => s.Slug == "csharp").Sum(s => s.Correct) >= AchievementCodes.CSharpBeginnerCorrect,
            AchievementCodes.ProblemSolver => solved >= AchievementCodes.ProblemSolverSolved,
            AchievementCodes.CodingWarrior => solved >= AchievementCodes.CodingWarriorSolved,
            AchievementCodes.SevenDayStreak => streak >= 7,
            AchievementCodes.ThirtyDayStreak => streak >= 30,
            AchievementCodes.FirstMockInterview => mocks.Count > 0,
            AchievementCodes.InterviewChampion => mocks.Any(m => m >= AchievementCodes.InterviewChampionScore),
            AchievementCodes.Scholar => lessons >= AchievementCodes.ScholarLessons,
            AchievementCodes.Centurion => stats.Sum(s => s.Attempted) >= AchievementCodes.CenturionAttempts,
            _ => false
        };

        var awarded = new List<string>();
        foreach (var achievement in pending.Where(p => Meets(p.Code)))
        {
            _db.UserAchievements.Add(new UserAchievement { UserId = userId, AchievementId = achievement.Id, AwardedAt = _clock.UtcNow });
            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                _db.ResetTracking();
                continue;
            }
            await AwardXpAsync(userId, achievement.XpReward, XpSourceType.Achievement, achievement.Code, $"Achievement: {achievement.Name}", ct);
            _db.Notifications.Add(new Notification
            {
                UserId = userId, Category = NotificationCategory.System, Title = "Achievement unlocked",
                Body = $"You earned \"{achievement.Name}\" (+{achievement.XpReward} XP).", DeepLink = "devinterview://achievements"
            });
            await _db.SaveChangesAsync(ct);
            awarded.Add(achievement.Code);
        }
        return awarded;
    }

    public async Task<int> GetTotalXpAsync(Guid userId, CancellationToken ct) =>
        await _db.UserXpTransactions.Where(x => x.UserId == userId).SumAsync(x => (int?)x.Amount, ct) ?? 0;

    public async Task<(int Current, int Longest)> GetStreakAsync(Guid userId, CancellationToken ct)
    {
        var streak = await _db.UserStreaks.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == userId, ct);
        if (streak is null) return (0, 0);
        var tz = await _db.UserPreferences.Where(p => p.UserId == userId).Select(p => p.TimeZoneId).FirstOrDefaultAsync(ct);
        var today = TimeZones.LocalDate(_clock.UtcNow, tz);
        return (StreakCalculator.EffectiveStreak(streak.CurrentStreak, streak.LastActiveDate, today), streak.LongestStreak);
    }

    public static LevelDto ToLevelDto(int xp)
    {
        var info = LevelCalculator.FromXp(xp);
        return new LevelDto(info.Level, LevelCalculator.Title(info.Level), info.TotalXp, info.CurrentLevelXp, info.NextLevelXp,
            Math.Round(info.ProgressToNext, 3));
    }

    public async Task<GamificationProfileDto> GetProfileAsync(Guid userId, CancellationToken ct)
    {
        var xp = await GetTotalXpAsync(userId, ct);
        var (current, longest) = await GetStreakAsync(userId, ct);
        var pref = await _db.UserPreferences.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId, ct);
        var today = TimeZones.LocalDate(_clock.UtcNow, pref?.TimeZoneId);
        var weekStart = TimeZones.WeekStart(today);
        var weekSeconds = await _db.UserActivityLogs.Where(a => a.UserId == userId && a.LocalDate >= weekStart)
            .SumAsync(a => (int?)a.DurationSeconds, ct) ?? 0;
        var target = pref?.WeeklyGoalMinutes ?? 180;

        var definitions = await _db.Achievements.AsNoTracking().Where(a => a.IsActive).OrderBy(a => a.XpReward).ToListAsync(ct);
        var earned = await _db.UserAchievements.AsNoTracking().Where(u => u.UserId == userId)
            .ToDictionaryAsync(u => u.AchievementId, u => u.AwardedAt, ct);
        var achievements = definitions.Select(d => new AchievementDto(d.Code, d.Name, d.Description, d.IconKey, d.XpReward,
            earned.ContainsKey(d.Id), earned.TryGetValue(d.Id, out var at) ? at : null)).ToList();

        var recent = await _db.UserXpTransactions.AsNoTracking().Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt).Take(20)
            .Select(x => new XpTransactionDto(x.Amount, x.SourceType.ToString(), x.Reason, x.CreatedAt)).ToListAsync(ct);

        return new GamificationProfileDto(ToLevelDto(xp), current, longest,
            new WeeklyGoalDto(target, weekSeconds / 60, weekSeconds / 60 >= target, weekStart), achievements, recent);
    }

    public async Task<LeaderboardDto> GetLeaderboardAsync(Guid userId, string period, int top, CancellationToken ct)
    {
        top = Math.Clamp(top, 5, 100);
        var now = _clock.UtcNow;
        DateTime? since = period.ToLowerInvariant() switch
        {
            "weekly" => now.Date.AddDays(-(((int)now.DayOfWeek + 6) % 7)),
            "monthly" => new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc),
            _ => null
        };
        var normalizedPeriod = since is null ? "all" : period.ToLowerInvariant();

        var visibleUsers = _db.Users.Where(u => u.IsActive && !u.IsDeleted &&
            !_db.UserPreferences.Any(p => p.UserId == u.Id && !p.ShowOnLeaderboard));
        var xpQuery = _db.UserXpTransactions.Where(x => since == null || x.CreatedAt >= since);
        var totals = await xpQuery
            .Where(x => visibleUsers.Any(u => u.Id == x.UserId))
            .GroupBy(x => x.UserId)
            .Select(g => new { UserId = g.Key, Xp = g.Sum(x => x.Amount) })
            .OrderByDescending(x => x.Xp).Take(top).ToListAsync(ct);

        var ids = totals.Select(t => t.UserId).Append(userId).Distinct().ToList();
        var users = await _db.Users.AsNoTracking().Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName, u.ProfilePhotoUrl }).ToDictionaryAsync(u => u.Id, ct);
        var lifetime = await _db.UserXpTransactions.Where(x => ids.Contains(x.UserId)).GroupBy(x => x.UserId)
            .Select(g => new { g.Key, Xp = g.Sum(x => x.Amount) }).ToDictionaryAsync(x => x.Key, x => x.Xp, ct);

        string Display(Guid id) => users.TryGetValue(id, out var u)
            ? $"{u.FirstName} {(string.IsNullOrEmpty(u.LastName) ? "" : u.LastName[0] + ".")}".Trim()
            : "Developer";

        var entries = totals.Select((t, i) => new LeaderboardEntryDto(i + 1, t.UserId, Display(t.UserId),
            users.TryGetValue(t.UserId, out var u) ? u.ProfilePhotoUrl : null, t.Xp,
            LevelCalculator.FromXp(lifetime.GetValueOrDefault(t.UserId)).Level, t.UserId == userId)).ToList();

        var mine = entries.FirstOrDefault(e => e.IsCurrentUser);
        if (mine is null)
        {
            var myXp = await xpQuery.Where(x => x.UserId == userId).SumAsync(x => (int?)x.Amount, ct) ?? 0;
            var ahead = await xpQuery.Where(x => visibleUsers.Any(u => u.Id == x.UserId)).GroupBy(x => x.UserId)
                .Select(g => g.Sum(x => x.Amount)).CountAsync(sum => sum > myXp, ct);
            mine = new LeaderboardEntryDto(ahead + 1, userId, Display(userId),
                users.TryGetValue(userId, out var me) ? me.ProfilePhotoUrl : null, myXp,
                LevelCalculator.FromXp(lifetime.GetValueOrDefault(userId)).Level, true);
        }
        return new LeaderboardDto(normalizedPeriod, entries, mine);
    }
}
