using DevInterview.Domain.Common;
using DevInterview.Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace DevInterview.Domain.Entities;

/// <summary>Application user. Password hashing, lockout and tokens are handled by ASP.NET Core Identity.</summary>
public class AppUser : IdentityUser<Guid>, ISoftDeletable
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? ProfilePhotoUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public string? GoogleSubjectId { get; set; }

    public UserProfile? Profile { get; set; }
    public UserPreference? Preference { get; set; }
    public ICollection<AppUserRole> UserRoles { get; set; } = new List<AppUserRole>();

    public string FullName => $"{FirstName} {LastName}".Trim();
}

public class AppRole : IdentityRole<Guid>
{
    public AppRole() { }
    public AppRole(string name) : base(name) { }
    public string? Description { get; set; }
    public ICollection<AppUserRole> UserRoles { get; set; } = new List<AppUserRole>();
}

public class AppUserRole : IdentityUserRole<Guid>
{
    public AppUser User { get; set; } = null!;
    public AppRole Role { get; set; } = null!;
}

public static class RoleNames
{
    public const string SuperAdmin = "SuperAdmin";
    public const string Admin = "Admin";
    public const string ContentManager = "ContentManager";
    public const string User = "User";

    public static readonly string[] All = { SuperAdmin, Admin, ContentManager, User };
    public static readonly string[] AdminPanel = { SuperAdmin, Admin, ContentManager };
}

public class UserProfile : BaseEntity
{
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public ExperienceLevel ExperienceLevel { get; set; }
    public string? CurrentJobRole { get; set; }
    public string? TargetJobRole { get; set; }
    public string? Bio { get; set; }
    public ICollection<UserPreferredTechnology> PreferredTechnologies { get; set; } = new List<UserPreferredTechnology>();
}

public class UserPreferredTechnology
{
    public Guid UserProfileId { get; set; }
    public UserProfile UserProfile { get; set; } = null!;
    public Guid TechnologyId { get; set; }
    public Technology Technology { get; set; } = null!;
}

public class UserPreference : BaseEntity
{
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;
    /// <summary>IANA time zone id used to compute day boundaries for streaks and reminders.</summary>
    public string TimeZoneId { get; set; } = "UTC";
    public int DailyQuestionGoal { get; set; } = 10;
    public int WeeklyGoalMinutes { get; set; } = 180;
    public bool ShowOnLeaderboard { get; set; } = true;
}

/// <summary>Refresh token. Only a SHA-256 hash of the token is stored. Tokens rotate on every use;
/// reuse of a rotated token revokes the whole family.</summary>
public class RefreshToken : BaseEntity
{
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public string TokenHash { get; set; } = string.Empty;
    public Guid FamilyId { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? RevokedReason { get; set; }
    public string? ReplacedByTokenHash { get; set; }
    public string? CreatedByIp { get; set; }
    public string? DeviceName { get; set; }

    public bool IsActive(DateTime nowUtc) => RevokedAt is null && ExpiresAt > nowUtc;
}

public class DeviceToken : BaseEntity
{
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public string Token { get; set; } = string.Empty;
    public string Platform { get; set; } = "android";
    public DateTime LastSeenAt { get; set; }
}

public class NotificationPreference : BaseEntity
{
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public NotificationCategory Category { get; set; }
    public bool Enabled { get; set; } = true;
    /// <summary>Preferred local delivery time (hour 0-23) in the user's time zone.</summary>
    public int PreferredHourLocal { get; set; } = 19;
}

public class InterviewReminder : BaseEntity
{
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public DateTime ScheduledAt { get; set; }
    public string? Notes { get; set; }
    public bool ReminderSent { get; set; }
}
