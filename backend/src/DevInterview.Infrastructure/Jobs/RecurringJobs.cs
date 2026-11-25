using DevInterview.Application.Features.Notifications;
using DevInterview.Application.Features.Quizzes;
using DevInterview.Application.Features.Subscriptions;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace DevInterview.Infrastructure.Jobs;

/// <summary>Hangfire job entry points. Each runs in its own DI scope.</summary>
public sealed class RecurringJobs
{
    private readonly IQuizService _quizzes;
    private readonly ISubscriptionService _subscriptions;
    private readonly IReminderJobService _reminders;
    private readonly ILogger<RecurringJobs> _logger;

    public RecurringJobs(IQuizService quizzes, ISubscriptionService subscriptions, IReminderJobService reminders, ILogger<RecurringJobs> logger)
    {
        _quizzes = quizzes;
        _subscriptions = subscriptions;
        _reminders = reminders;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 1)]
    public async Task FinalizeExpiredQuizzes()
    {
        var count = await _quizzes.FinalizeExpiredAttemptsAsync(CancellationToken.None);
        if (count > 0) _logger.LogInformation("Auto-submitted {Count} expired quiz attempts", count);
    }

    [AutomaticRetry(Attempts = 2)]
    public Task ProcessSubscriptionExpirations() => _subscriptions.ProcessExpirationsAsync(CancellationToken.None);

    [AutomaticRetry(Attempts = 0)]
    public Task SendHourlyReminders() => _reminders.SendHourlyRemindersAsync(CancellationToken.None);

    [AutomaticRetry(Attempts = 1)]
    public Task SendInterviewReminders() => _reminders.SendInterviewRemindersAsync(CancellationToken.None);

    [AutomaticRetry(Attempts = 1)]
    public Task Cleanup() => _reminders.CleanupAsync(CancellationToken.None);

    public static void Register(IRecurringJobManager manager)
    {
        manager.AddOrUpdate<RecurringJobs>("quiz-expiry", j => j.FinalizeExpiredQuizzes(), "*/2 * * * *");
        manager.AddOrUpdate<RecurringJobs>("subscription-expiry", j => j.ProcessSubscriptionExpirations(), "17 * * * *");
        manager.AddOrUpdate<RecurringJobs>("hourly-reminders", j => j.SendHourlyReminders(), "2 * * * *");
        manager.AddOrUpdate<RecurringJobs>("interview-reminders", j => j.SendInterviewReminders(), "*/30 * * * *");
        manager.AddOrUpdate<RecurringJobs>("cleanup", j => j.Cleanup(), "43 3 * * *");
    }
}
