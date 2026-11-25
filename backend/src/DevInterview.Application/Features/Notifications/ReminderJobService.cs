using DevInterview.Application.Abstractions;
using DevInterview.Application.Common;
using DevInterview.Application.Features.Coding;
using DevInterview.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DevInterview.Application.Features.Notifications;

/// <summary>Scheduled reminder jobs (run hourly by Hangfire). Each notification is sent at most once per user per period.</summary>
public interface IReminderJobService
{
    Task<int> SendHourlyRemindersAsync(CancellationToken ct);
    Task<int> SendInterviewRemindersAsync(CancellationToken ct);
    Task<int> CleanupAsync(CancellationToken ct);
}

public sealed class ReminderJobService : IReminderJobService
{
    private readonly IAppDbContext _db;
    private readonly INotificationService _notifications;
    private readonly ICodingService _coding;
    private readonly IClock _clock;
    private readonly ILogger<ReminderJobService> _logger;

    public ReminderJobService(IAppDbContext db, INotificationService notifications, ICodingService coding, IClock clock, ILogger<ReminderJobService> logger)
    {
        _db = db;
        _notifications = notifications;
        _coding = coding;
        _clock = clock;
        _logger = logger;
    }

    public async Task<int> SendHourlyRemindersAsync(CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var categories = new[] { NotificationCategory.DailyPractice, NotificationCategory.StreakReminder, NotificationCategory.DailyChallenge,
            NotificationCategory.WeeklySummary, NotificationCategory.LearningGoal, NotificationCategory.MockInterviewReminder };
        var candidates = await _db.NotificationPreferences.AsNoTracking()
            .Where(p => p.Enabled && categories.Contains(p.Category) && _db.Users.Any(u => u.Id == p.UserId && u.IsActive && !u.IsDeleted))
            .Select(p => new { p.UserId, p.Category, p.PreferredHourLocal, Tz = _db.UserPreferences.Where(x => x.UserId == p.UserId).Select(x => x.TimeZoneId).FirstOrDefault() })
            .ToListAsync(ct);

        var sent = 0;
        foreach (var c in candidates)
        {
            var tz = TimeZones.Find(c.Tz);
            var local = TimeZoneInfo.ConvertTimeFromUtc(now, tz);
            if (local.Hour != c.PreferredHourLocal) continue;
            var today = DateOnly.FromDateTime(local);
            var dayStartUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local.Date, DateTimeKind.Unspecified), tz);
            if (await _db.Notifications.AnyAsync(n => n.UserId == c.UserId && n.Category == c.Category && n.CreatedAt >= dayStartUtc, ct)) continue;

            var activeToday = await _db.UserActivityLogs.AnyAsync(a => a.UserId == c.UserId && a.LocalDate == today, ct);
            (string Title, string Body, string Link)? message = c.Category switch
            {
                NotificationCategory.DailyPractice when !activeToday =>
                    ("Time for today's practice", "A few questions a day keeps interview nerves away. Start your daily quiz.", "devinterview://quiz/daily"),
                NotificationCategory.StreakReminder when !activeToday && await HasStreakAsync(c.UserId, today, ct) =>
                    ("Keep your streak alive", "You haven't practised today yet. One question is enough to keep your streak.", "devinterview://practice"),
                NotificationCategory.DailyChallenge => await DailyChallengeAsync(c.UserId, ct),
                NotificationCategory.WeeklySummary when local.DayOfWeek == DayOfWeek.Sunday => await WeeklySummaryAsync(c.UserId, today, ct),
                NotificationCategory.LearningGoal when local.DayOfWeek == DayOfWeek.Thursday => await LearningGoalAsync(c.UserId, today, ct),
                NotificationCategory.MockInterviewReminder when local.DayOfWeek == DayOfWeek.Saturday &&
                    !await _db.MockInterviews.AnyAsync(m => m.UserId == c.UserId && m.StartedAt >= now.AddDays(-7), ct) =>
                    ("Practise out loud", "A mock interview this weekend helps you prepare for the real thing.", "devinterview://mock-interview"),
                _ => null
            };
            if (message is not { } m) continue;
            await _notifications.NotifyAsync(c.UserId, c.Category, m.Title, m.Body, m.Link, ct);
            sent++;
        }
        if (sent > 0) _logger.LogInformation("Sent {Count} scheduled reminders", sent);
        return sent;
    }

    private async Task<bool> HasStreakAsync(Guid userId, DateOnly today, CancellationToken ct) =>
        await _db.UserStreaks.AnyAsync(s => s.UserId == userId && s.CurrentStreak > 0 && s.LastActiveDate == today.AddDays(-1), ct);

    private async Task<(string, string, string)?> DailyChallengeAsync(Guid userId, CancellationToken ct)
    {
        var challenge = await _coding.GetDailyChallengeAsync(userId, ct);
        if (challenge is null || challenge.IsSolved) return null;
        return ("Daily coding challenge", $"Today's challenge: {challenge.Title} ({challenge.Difficulty}).", $"devinterview://challenge/{challenge.Id}");
    }

    private async Task<(string, string, string)?> WeeklySummaryAsync(Guid userId, DateOnly today, CancellationToken ct)
    {
        var weekStart = TimeZones.WeekStart(today);
        var minutes = (await _db.UserActivityLogs.Where(a => a.UserId == userId && a.LocalDate >= weekStart).SumAsync(a => (int?)a.DurationSeconds, ct) ?? 0) / 60;
        var since = weekStart.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var answered = await _db.QuestionAttempts.CountAsync(a => a.UserId == userId && a.CreatedAt >= since && a.IsCorrect != null, ct);
        var correct = await _db.QuestionAttempts.CountAsync(a => a.UserId == userId && a.CreatedAt >= since && a.IsCorrect == true, ct);
        if (answered == 0 && minutes == 0) return ("Your weekly summary", "No practice this week. Pick a learning path and start fresh tomorrow.", "devinterview://learn");
        return ("Your weekly summary", $"This week: {answered} questions ({(answered == 0 ? 0 : correct * 100 / answered)}% correct), {minutes} minutes of practice.", "devinterview://analytics");
    }

    private async Task<(string, string, string)?> LearningGoalAsync(Guid userId, DateOnly today, CancellationToken ct)
    {
        var target = await _db.UserPreferences.Where(p => p.UserId == userId).Select(p => p.WeeklyGoalMinutes).FirstOrDefaultAsync(ct);
        var weekStart = TimeZones.WeekStart(today);
        var minutes = (await _db.UserActivityLogs.Where(a => a.UserId == userId && a.LocalDate >= weekStart).SumAsync(a => (int?)a.DurationSeconds, ct) ?? 0) / 60;
        if (target <= 0 || minutes >= target) return null;
        return ("Weekly goal check-in", $"You're at {minutes} of {target} minutes this week. You can still make it!", "devinterview://learn");
    }

    public async Task<int> SendInterviewRemindersAsync(CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var due = await _db.InterviewReminders.Where(r => !r.ReminderSent && r.ScheduledAt > now && r.ScheduledAt <= now.AddHours(24)).Take(500).ToListAsync(ct);
        foreach (var r in due)
        {
            await _notifications.NotifyAsync(r.UserId, NotificationCategory.MockInterviewReminder, "Interview tomorrow",
                $"{r.Title}{(r.CompanyName is null ? "" : $" at {r.CompanyName}")} is coming up. Do a quick revision session.", "devinterview://flashcards", ct);
            r.ReminderSent = true;
        }
        await _db.SaveChangesAsync(ct);
        return due.Count;
    }

    public async Task<int> CleanupAsync(CancellationToken ct)
    {
        var cutoff = _clock.UtcNow.AddDays(-30);
        var tokens = await _db.RefreshTokens.Where(t => t.ExpiresAt < cutoff || (t.RevokedAt != null && t.RevokedAt < cutoff)).ExecuteDeleteAsync(ct);
        var counters = await _db.UsageCounters.Where(c => c.Date < DateOnly.FromDateTime(cutoff)).ExecuteDeleteAsync(ct);
        return tokens + counters;
    }
}
