using Asp.Versioning;
using DevInterview.API.Infrastructure;
using DevInterview.Application.Abstractions;
using DevInterview.Application.Features.Analytics;
using DevInterview.Application.Features.Flashcards;
using DevInterview.Application.Features.Gamification;
using DevInterview.Application.Features.Learning;
using DevInterview.Application.Features.Library;
using DevInterview.Application.Features.Notifications;
using DevInterview.Application.Features.Subscriptions;
using DevInterview.Contracts.Common;
using DevInterview.Contracts.Engagement;
using DevInterview.Contracts.Learning;
using DevInterview.Infrastructure.Billing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace DevInterview.API.Controllers;

[ApiVersion("1.0")]
[Authorize]
[Route("api/v{version:apiVersion}/dashboard")]
public sealed class DashboardController : ApiControllerBase
{
    private readonly IDashboardService _dashboard;
    private readonly IAnalyticsService _analytics;
    private readonly ICurrentUser _user;

    public DashboardController(IDashboardService dashboard, IAnalyticsService analytics, ICurrentUser user)
    {
        _dashboard = dashboard;
        _analytics = analytics;
        _user = user;
    }

    [HttpGet("summary")]
    public async Task<ActionResult<ApiResponse<DashboardSummaryDto>>> Summary(CancellationToken ct) => Success(await _dashboard.GetSummaryAsync(_user.RequireUserId(), ct));

    [HttpGet("activity")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ActivityDayDto>>>> Activity([FromQuery] int days = 7, CancellationToken ct = default) =>
        Success(await _analytics.GetDailyActivityAsync(_user.RequireUserId(), days, ct));

    [HttpGet("skill-progress")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<SkillProgressDto>>>> SkillProgress(CancellationToken ct) =>
        Success(await _analytics.GetSkillProgressAsync(_user.RequireUserId(), ct));
}

[ApiVersion("1.0")]
[Authorize]
[Route("api/v{version:apiVersion}/analytics")]
public sealed class AnalyticsController : ApiControllerBase
{
    private readonly IAnalyticsService _analytics;
    private readonly IEntitlementService _entitlements;
    private readonly ICurrentUser _user;

    public AnalyticsController(IAnalyticsService analytics, IEntitlementService entitlements, ICurrentUser user)
    {
        _analytics = analytics;
        _entitlements = entitlements;
        _user = user;
    }

    [HttpGet("overview")]
    public async Task<ActionResult<ApiResponse<AnalyticsOverviewDto>>> Overview(CancellationToken ct) => Success(await _analytics.GetOverviewAsync(_user.RequireUserId(), ct));

    /// <summary>Per-technology and per-category accuracy (Premium: advanced analytics).</summary>
    [HttpGet("technology")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<TechnologyAnalyticsDto>>>> Technology(CancellationToken ct)
    {
        await _entitlements.EnsurePremiumAsync(_user.RequireUserId(), "Advanced analytics", ct);
        return Success(await _analytics.GetTechnologyAsync(_user.RequireUserId(), ct));
    }

    [HttpGet("weekly")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PeriodActivityDto>>>> Weekly([FromQuery] int weeks = 8, CancellationToken ct = default) =>
        Success(await _analytics.GetWeeklyAsync(_user.RequireUserId(), weeks, ct));

    [HttpGet("monthly")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PeriodActivityDto>>>> Monthly([FromQuery] int months = 6, CancellationToken ct = default)
    {
        await _entitlements.EnsurePremiumAsync(_user.RequireUserId(), "Advanced analytics", ct);
        return Success(await _analytics.GetMonthlyAsync(_user.RequireUserId(), months, ct));
    }
}

[ApiVersion("1.0")]
[Authorize]
[Route("api/v{version:apiVersion}/gamification")]
public sealed class GamificationController : ApiControllerBase
{
    private readonly IGamificationService _gamification;
    private readonly ICurrentUser _user;

    public GamificationController(IGamificationService gamification, ICurrentUser user)
    {
        _gamification = gamification;
        _user = user;
    }

    [HttpGet("profile")]
    public async Task<ActionResult<ApiResponse<GamificationProfileDto>>> Profile(CancellationToken ct) => Success(await _gamification.GetProfileAsync(_user.RequireUserId(), ct));

    /// <summary>Leaderboard by XP. Period: weekly, monthly or all.</summary>
    [HttpGet("leaderboard")]
    public async Task<ActionResult<ApiResponse<LeaderboardDto>>> Leaderboard([FromQuery] string period = "weekly", [FromQuery] int top = 50, CancellationToken ct = default) =>
        Success(await _gamification.GetLeaderboardAsync(_user.RequireUserId(), period, top, ct));
}

[ApiVersion("1.0")]
[Authorize]
[Route("api/v{version:apiVersion}/learning")]
public sealed class LearningController : ApiControllerBase
{
    private readonly ILearningService _learning;
    private readonly ICurrentUser _user;

    public LearningController(ILearningService learning, ICurrentUser user)
    {
        _learning = learning;
        _user = user;
    }

    [HttpGet("paths")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<LearningPathListItemDto>>>> Paths([FromQuery] string? level, [FromQuery] Guid? technologyId, CancellationToken ct) =>
        Success(await _learning.GetPathsAsync(_user.RequireUserId(), level, technologyId, ct));

    [HttpGet("recommended")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<LearningPathListItemDto>>>> Recommended(CancellationToken ct) =>
        Success(await _learning.GetRecommendedAsync(_user.RequireUserId(), 5, ct));

    [HttpGet("paths/{id:guid}")]
    public async Task<ActionResult<ApiResponse<LearningPathDetailDto>>> Path(Guid id, CancellationToken ct) => Success(await _learning.GetPathAsync(_user.RequireUserId(), id, ct));

    [HttpGet("lessons/{id:guid}")]
    public async Task<ActionResult<ApiResponse<LessonDetailDto>>> Lesson(Guid id, CancellationToken ct) => Success(await _learning.GetLessonAsync(_user.RequireUserId(), id, ct));

    [HttpPost("progress")]
    public async Task<ActionResult<ApiResponse<LessonProgressDto>>> Progress(UpdateLessonProgressRequest request, CancellationToken ct) =>
        Success(await _learning.UpdateProgressAsync(_user.RequireUserId(), request, ct));
}

[ApiVersion("1.0")]
[Authorize]
[Route("api/v{version:apiVersion}/flashcards")]
public sealed class FlashcardsController : ApiControllerBase
{
    private readonly IFlashcardService _flashcards;
    private readonly ICurrentUser _user;

    public FlashcardsController(IFlashcardService flashcards, ICurrentUser user)
    {
        _flashcards = flashcards;
        _user = user;
    }

    [HttpGet("decks")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<FlashcardDeckDto>>>> Decks(CancellationToken ct) => Success(await _flashcards.GetDecksAsync(_user.RequireUserId(), ct));

    /// <summary>Cards for a study session: due cards first (most overdue), then new cards.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<FlashcardDto>>>> Cards([FromQuery] FlashcardQuery query, CancellationToken ct) =>
        Success(await _flashcards.GetCardsAsync(_user.RequireUserId(), query, ct));

    [HttpPost("review")]
    public async Task<ActionResult<ApiResponse<FlashcardReviewResultDto>>> Review(ReviewFlashcardRequest request, CancellationToken ct) =>
        Success(await _flashcards.ReviewAsync(_user.RequireUserId(), request, ct));

    [HttpPut("{id:guid}/bookmark")]
    public async Task<ActionResult<ApiResponse<object>>> Bookmark(Guid id, BookmarkFlashcardRequest request, CancellationToken ct)
    {
        await _flashcards.SetBookmarkAsync(_user.RequireUserId(), id, request.IsBookmarked, ct);
        return Done(request.IsBookmarked ? "Bookmarked." : "Bookmark removed.");
    }
}

[ApiVersion("1.0")]
[Authorize]
[Route("api/v{version:apiVersion}/notifications")]
public sealed class NotificationsController : ApiControllerBase
{
    private readonly INotificationService _notifications;
    private readonly ICurrentUser _user;

    public NotificationsController(INotificationService notifications, ICurrentUser user)
    {
        _notifications = notifications;
        _user = user;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<NotificationDto>>>> List([FromQuery] PageQuery query, [FromQuery] bool unreadOnly = false, CancellationToken ct = default) =>
        Success(await _notifications.ListAsync(_user.RequireUserId(), query, unreadOnly, ct));

    [HttpPost("{id:guid}/read")]
    public async Task<ActionResult<ApiResponse<object>>> Read(Guid id, CancellationToken ct)
    {
        await _notifications.MarkReadAsync(_user.RequireUserId(), id, ct);
        return Done();
    }

    [HttpPost("read-all")]
    public async Task<ActionResult<ApiResponse<object>>> ReadAll(CancellationToken ct)
    {
        await _notifications.MarkAllReadAsync(_user.RequireUserId(), ct);
        return Done();
    }

    [HttpGet("preferences")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<NotificationPreferenceDto>>>> Preferences(CancellationToken ct) =>
        Success(await _notifications.GetPreferencesAsync(_user.RequireUserId(), ct));

    [HttpPut("preferences")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<NotificationPreferenceDto>>>> UpdatePreferences(UpdateNotificationPreferencesRequest request, CancellationToken ct) =>
        Success(await _notifications.UpdatePreferencesAsync(_user.RequireUserId(), request, ct));

    [HttpGet("reminders")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<InterviewReminderDto>>>> Reminders(CancellationToken ct) =>
        Success(await _notifications.GetRemindersAsync(_user.RequireUserId(), ct));

    [HttpPost("reminders")]
    public async Task<ActionResult<ApiResponse<InterviewReminderDto>>> CreateReminder(CreateInterviewReminderRequest request, CancellationToken ct) =>
        Created(await _notifications.CreateReminderAsync(_user.RequireUserId(), request, ct));

    [HttpDelete("reminders/{id:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteReminder(Guid id, CancellationToken ct)
    {
        await _notifications.DeleteReminderAsync(_user.RequireUserId(), id, ct);
        return Done("Reminder deleted.");
    }
}

[ApiVersion("1.0")]
[Authorize]
[Route("api/v{version:apiVersion}/subscriptions")]
public sealed class SubscriptionsController : ApiControllerBase
{
    private readonly ISubscriptionService _subscriptions;
    private readonly ICurrentUser _user;
    private readonly GooglePlayOptions _play;
    private readonly ILogger<SubscriptionsController> _logger;

    public SubscriptionsController(ISubscriptionService subscriptions, ICurrentUser user, IOptions<GooglePlayOptions> play, ILogger<SubscriptionsController> logger)
    {
        _subscriptions = subscriptions;
        _user = user;
        _play = play.Value;
        _logger = logger;
    }

    [HttpGet("plans")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<SubscriptionPlanDto>>>> Plans(CancellationToken ct) => Success(await _subscriptions.GetPlansAsync(ct));

    /// <summary>Current tier, expiry and today's usage against plan limits (always computed server-side).</summary>
    [HttpGet("status")]
    public async Task<ActionResult<ApiResponse<SubscriptionStatusDto>>> Status(CancellationToken ct) => Success(await _subscriptions.GetStatusAsync(_user.RequireUserId(), ct));

    [HttpGet("history")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PurchaseHistoryItemDto>>>> History(CancellationToken ct) =>
        Success(await _subscriptions.GetHistoryAsync(_user.RequireUserId(), ct));

    /// <summary>Verifies a Google Play purchase token with Google and activates the entitlement.</summary>
    [HttpPost("google-play/verify")]
    public async Task<ActionResult<ApiResponse<SubscriptionStatusDto>>> VerifyGooglePlay(VerifyGooglePlayPurchaseRequest request, CancellationToken ct) =>
        Success(await _subscriptions.VerifyGooglePlayAsync(_user.RequireUserId(), request, ct));

    /// <summary>Google Play Real-Time Developer Notifications (Pub/Sub push). Protected by a shared secret query parameter.</summary>
    [HttpPost("google-play/rtdn")]
    [AllowAnonymous]
    public async Task<IActionResult> GooglePlayNotification([FromQuery] string? token, [FromBody] PubSubPush push, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_play.NotificationSecret) || !string.Equals(token, _play.NotificationSecret, StringComparison.Ordinal))
            return Unauthorized();
        try
        {
            var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(push.Message.Data));
            var node = System.Text.Json.Nodes.JsonNode.Parse(json);
            var sub = node?["subscriptionNotification"];
            var purchaseToken = sub?["purchaseToken"]?.GetValue<string>();
            var productId = sub?["subscriptionId"]?.GetValue<string>();
            if (purchaseToken is not null && productId is not null)
                await _subscriptions.HandleGooglePlayNotificationAsync(productId, purchaseToken, ct);
        }
        catch (Exception ex) when (ex is FormatException or System.Text.Json.JsonException)
        {
            _logger.LogWarning("Ignored malformed RTDN payload");
        }
        return NoContent(); // acknowledge so Pub/Sub does not retry
    }

    public sealed record PubSubMessage(string Data, string? MessageId);
    public sealed record PubSubPush(PubSubMessage Message, string? Subscription);
}

/// <summary>Offline synchronisation for bookmarks, notes and flashcard reviews.</summary>
[ApiVersion("1.0")]
[Authorize]
[Route("api/v{version:apiVersion}/sync")]
public sealed class SyncController : ApiControllerBase
{
    private readonly ISyncService _sync;
    private readonly ICurrentUser _user;

    public SyncController(ISyncService sync, ICurrentUser user)
    {
        _sync = sync;
        _user = user;
    }

    [HttpPost("push")]
    public async Task<ActionResult<ApiResponse<SyncPushResponse>>> Push(SyncPushRequest request, CancellationToken ct) =>
        Success(await _sync.PushAsync(_user.RequireUserId(), request, ct));

    [HttpGet("pull")]
    public async Task<ActionResult<ApiResponse<SyncPullResponse>>> Pull([FromQuery] DateTime? since, CancellationToken ct) =>
        Success(await _sync.PullAsync(_user.RequireUserId(), since, ct));
}
