using DevInterview.Domain.Common;
using DevInterview.Domain.Enums;

namespace DevInterview.Domain.Entities;

public class UserActivityLog : BaseEntity
{
    public Guid UserId { get; set; }
    public ActivityType ActivityType { get; set; }
    public Guid? EntityId { get; set; }
    public Guid? TechnologyId { get; set; }
    public int DurationSeconds { get; set; }
    public DateTime OccurredAt { get; set; }
    /// <summary>Calendar day of the activity in the user's time zone (used for streaks/heatmaps).</summary>
    public DateOnly LocalDate { get; set; }
}

/// <summary>Aggregated per-user, per-technology accuracy. Maintained incrementally by the analytics service.</summary>
public class UserPerformanceStat : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid TechnologyId { get; set; }
    public Technology Technology { get; set; } = null!;
    public int Attempted { get; set; }
    public int Correct { get; set; }
}

public class Achievement : BaseEntity, IActivatable
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string IconKey { get; set; } = "trophy";
    public int XpReward { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UserAchievement : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid AchievementId { get; set; }
    public Achievement Achievement { get; set; } = null!;
    public DateTime AwardedAt { get; set; }
}

public class UserStreak : BaseEntity
{
    public Guid UserId { get; set; }
    public int CurrentStreak { get; set; }
    public int LongestStreak { get; set; }
    public DateOnly? LastActiveDate { get; set; }
}

/// <summary>XP ledger. The unique (UserId, SourceType, SourceKey) index prevents duplicate rewards.</summary>
public class UserXpTransaction : BaseEntity
{
    public Guid UserId { get; set; }
    public int Amount { get; set; }
    public XpSourceType SourceType { get; set; }
    public string SourceKey { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

/// <summary>Daily usage counter for free-plan limits and AI fair-use quotas.</summary>
public class UsageCounter : BaseEntity
{
    public Guid UserId { get; set; }
    public UsageFeature Feature { get; set; }
    public DateOnly Date { get; set; }
    public int Count { get; set; }
}

public class Notification : BaseEntity
{
    public Guid UserId { get; set; }
    public NotificationCategory Category { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    /// <summary>Optional in-app deep link, e.g. "devinterview://challenge/{id}".</summary>
    public string? DeepLink { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime? PushedAt { get; set; }
    public Guid? BroadcastId { get; set; }
}

public class AuditLog : BaseEntity
{
    public Guid? UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    /// <summary>JSON with sensitive fields masked.</summary>
    public string? Details { get; set; }
    public string? IpAddress { get; set; }
}

public class AppSetting : BaseEntity
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class SubscriptionPlan : BaseEntity, IActivatable
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public SubscriptionTier Tier { get; set; }
    public long PriceMicros { get; set; }
    public string Currency { get; set; } = "USD";
    public int BillingPeriodMonths { get; set; }
    public string? GooglePlayProductId { get; set; }
    public string? GooglePlayBasePlanId { get; set; }
    /// <summary>JSON array of feature bullet strings shown on the plans screen.</summary>
    public string FeaturesJson { get; set; } = "[]";
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
}

public class UserSubscription : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid PlanId { get; set; }
    public SubscriptionPlan Plan { get; set; } = null!;
    public SubscriptionStatus Status { get; set; }
    public PaymentProvider Provider { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool AutoRenewing { get; set; }
    /// <summary>SHA-256 of the purchase token (the raw token is used only for verification calls).</summary>
    public string? PurchaseTokenHash { get; set; }
    public string? PurchaseToken { get; set; }
    public string? ExternalOrderId { get; set; }
    public DateTime? LastVerifiedAt { get; set; }
    public string? Notes { get; set; }

    public bool GrantsAccess(DateTime nowUtc) =>
        Status is SubscriptionStatus.Active or SubscriptionStatus.GracePeriod or SubscriptionStatus.Cancelled
        && ExpiresAt > nowUtc;
}
