using DevInterview.Application.Abstractions;
using DevInterview.Application.Common;
using DevInterview.Contracts.Common;
using DevInterview.Contracts.Engagement;
using DevInterview.Domain.Entities;
using DevInterview.Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DevInterview.Application.Features.Notifications;

public interface INotificationService
{
    Task<PagedResult<NotificationDto>> ListAsync(Guid userId, PageQuery query, bool unreadOnly, CancellationToken ct);
    Task MarkReadAsync(Guid userId, Guid notificationId, CancellationToken ct);
    Task MarkAllReadAsync(Guid userId, CancellationToken ct);
    Task<IReadOnlyList<NotificationPreferenceDto>> GetPreferencesAsync(Guid userId, CancellationToken ct);
    Task<IReadOnlyList<NotificationPreferenceDto>> UpdatePreferencesAsync(Guid userId, UpdateNotificationPreferencesRequest request, CancellationToken ct);
    Task<IReadOnlyList<InterviewReminderDto>> GetRemindersAsync(Guid userId, CancellationToken ct);
    Task<InterviewReminderDto> CreateReminderAsync(Guid userId, CreateInterviewReminderRequest request, CancellationToken ct);
    Task DeleteReminderAsync(Guid userId, Guid reminderId, CancellationToken ct);
    /// <summary>Creates an in-app notification and pushes it to the user's devices if the category is enabled.</summary>
    Task NotifyAsync(Guid userId, NotificationCategory category, string title, string body, string? deepLink, CancellationToken ct, Guid? broadcastId = null);
}

public sealed class NotificationService : INotificationService
{
    private readonly IAppDbContext _db;
    private readonly IPushNotificationSender _push;
    private readonly IClock _clock;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(IAppDbContext db, IPushNotificationSender push, IClock clock, ILogger<NotificationService> logger)
    {
        _db = db;
        _push = push;
        _clock = clock;
        _logger = logger;
    }

    public async Task<PagedResult<NotificationDto>> ListAsync(Guid userId, PageQuery query, bool unreadOnly, CancellationToken ct)
    {
        var q = _db.Notifications.AsNoTracking().Where(n => n.UserId == userId);
        if (unreadOnly) q = q.Where(n => n.ReadAt == null);
        return await q.OrderByDescending(n => n.CreatedAt)
            .Select(n => new NotificationDto(n.Id, n.Category.ToString(), n.Title, n.Body, n.DeepLink, n.CreatedAt, n.ReadAt))
            .ToPagedAsync(query, ct);
    }

    public async Task MarkReadAsync(Guid userId, Guid notificationId, CancellationToken ct)
    {
        var updated = await _db.Notifications.Where(n => n.Id == notificationId && n.UserId == userId && n.ReadAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, _clock.UtcNow), ct);
        if (updated == 0 && !await _db.Notifications.AnyAsync(n => n.Id == notificationId && n.UserId == userId, ct))
            throw new NotFoundException("Notification", notificationId);
    }

    public Task MarkAllReadAsync(Guid userId, CancellationToken ct) =>
        _db.Notifications.Where(n => n.UserId == userId && n.ReadAt == null).ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, _clock.UtcNow), ct);

    public async Task<IReadOnlyList<NotificationPreferenceDto>> GetPreferencesAsync(Guid userId, CancellationToken ct)
    {
        var existing = await _db.NotificationPreferences.AsNoTracking().Where(p => p.UserId == userId).ToListAsync(ct);
        return Enum.GetValues<NotificationCategory>().Where(c => c != NotificationCategory.System).Select(c =>
        {
            var p = existing.FirstOrDefault(e => e.Category == c);
            return new NotificationPreferenceDto(c.ToString(), p?.Enabled ?? true, p?.PreferredHourLocal ?? 19);
        }).ToList();
    }

    public async Task<IReadOnlyList<NotificationPreferenceDto>> UpdatePreferencesAsync(Guid userId, UpdateNotificationPreferencesRequest request, CancellationToken ct)
    {
        var existing = await _db.NotificationPreferences.Where(p => p.UserId == userId).ToListAsync(ct);
        foreach (var dto in request.Preferences)
        {
            var category = EnumParser.Parse<NotificationCategory>(dto.Category, "category");
            var p = existing.FirstOrDefault(e => e.Category == category);
            if (p is null)
            {
                p = new NotificationPreference { UserId = userId, Category = category };
                _db.NotificationPreferences.Add(p);
            }
            p.Enabled = dto.Enabled;
            p.PreferredHourLocal = Math.Clamp(dto.PreferredHourLocal, 0, 23);
        }
        await _db.SaveChangesAsync(ct);
        return await GetPreferencesAsync(userId, ct);
    }

    public async Task<IReadOnlyList<InterviewReminderDto>> GetRemindersAsync(Guid userId, CancellationToken ct) =>
        await _db.InterviewReminders.AsNoTracking().Where(r => r.UserId == userId && r.ScheduledAt >= _clock.UtcNow.AddDays(-1))
            .OrderBy(r => r.ScheduledAt).Select(r => new InterviewReminderDto(r.Id, r.Title, r.CompanyName, r.ScheduledAt, r.Notes)).ToListAsync(ct);

    public async Task<InterviewReminderDto> CreateReminderAsync(Guid userId, CreateInterviewReminderRequest request, CancellationToken ct)
    {
        var at = request.ScheduledAt.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(request.ScheduledAt, DateTimeKind.Utc) : request.ScheduledAt.ToUniversalTime();
        if (at <= _clock.UtcNow) throw ValidationAppException.For("scheduledAt", "The interview date must be in the future.");
        var reminder = new InterviewReminder { UserId = userId, Title = request.Title.Trim(), CompanyName = request.CompanyName?.Trim(), ScheduledAt = at, Notes = request.Notes?.Trim() };
        _db.InterviewReminders.Add(reminder);
        await _db.SaveChangesAsync(ct);
        return new InterviewReminderDto(reminder.Id, reminder.Title, reminder.CompanyName, reminder.ScheduledAt, reminder.Notes);
    }

    public async Task DeleteReminderAsync(Guid userId, Guid reminderId, CancellationToken ct)
    {
        var deleted = await _db.InterviewReminders.Where(r => r.Id == reminderId && r.UserId == userId).ExecuteDeleteAsync(ct);
        if (deleted == 0) throw new NotFoundException("Reminder", reminderId);
    }

    public async Task NotifyAsync(Guid userId, NotificationCategory category, string title, string body, string? deepLink, CancellationToken ct, Guid? broadcastId = null)
    {
        var enabled = category == NotificationCategory.System ||
                      await _db.NotificationPreferences.Where(p => p.UserId == userId && p.Category == category).Select(p => (bool?)p.Enabled).FirstOrDefaultAsync(ct) != false;
        if (!enabled) return;
        var notification = new Notification { UserId = userId, Category = category, Title = title, Body = body, DeepLink = deepLink, BroadcastId = broadcastId };
        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync(ct);

        if (!_push.IsConfigured) return;
        var tokens = await _db.DeviceTokens.Where(d => d.UserId == userId).Select(d => d.Token).ToListAsync(ct);
        if (tokens.Count == 0) return;
        try
        {
            var data = new Dictionary<string, string> { ["category"] = category.ToString(), ["notificationId"] = notification.Id.ToString() };
            if (deepLink is not null) data["deepLink"] = deepLink;
            var invalid = await _push.SendAsync(tokens, new PushMessage(title, body, data), ct);
            if (invalid.Count > 0) await _db.DeviceTokens.Where(d => invalid.Contains(d.Token)).ExecuteDeleteAsync(ct);
            notification.PushedAt = _clock.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Push delivery failed for notification {NotificationId}", notification.Id);
        }
    }
}

public sealed class CreateInterviewReminderRequestValidator : AbstractValidator<CreateInterviewReminderRequest>
{
    public CreateInterviewReminderRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(120);
        RuleFor(x => x.CompanyName).MaximumLength(120);
        RuleFor(x => x.Notes).MaximumLength(1000);
    }
}

public sealed class UpdateNotificationPreferencesRequestValidator : AbstractValidator<UpdateNotificationPreferencesRequest>
{
    public UpdateNotificationPreferencesRequestValidator()
    {
        RuleFor(x => x.Preferences).NotNull().Must(p => p.Count <= 20);
        RuleForEach(x => x.Preferences).ChildRules(p =>
        {
            p.RuleFor(x => x.Category).NotEmpty();
            p.RuleFor(x => x.PreferredHourLocal).InclusiveBetween(0, 23);
        });
    }
}
