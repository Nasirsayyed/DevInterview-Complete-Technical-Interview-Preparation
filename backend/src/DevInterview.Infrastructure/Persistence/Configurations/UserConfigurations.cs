using DevInterview.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevInterview.Infrastructure.Persistence.Configurations;

internal static class ConfigurationExtensions
{
    /// <summary>Removes the 4000-character convention limit (nvarchar(max) / TEXT).</summary>
    public static PropertyBuilder<T> Unbounded<T>(this PropertyBuilder<T> builder)
    {
        builder.Metadata.SetMaxLength(null);
        return builder;
    }

    public static PropertyBuilder<TEnum> AsString<TEnum>(this PropertyBuilder<TEnum> builder) where TEnum : struct, Enum =>
        builder.HasConversion<string>().HasMaxLength(40);
}

internal sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> b)
    {
        b.ToTable("Users");
        b.Property(x => x.FirstName).HasMaxLength(60).IsRequired();
        b.Property(x => x.LastName).HasMaxLength(60).IsRequired();
        b.Property(x => x.ProfilePhotoUrl).HasMaxLength(500);
        b.Property(x => x.GoogleSubjectId).HasMaxLength(100);
        b.HasIndex(x => x.GoogleSubjectId).IsUnique().HasFilter("[GoogleSubjectId] IS NOT NULL");
        b.HasIndex(x => x.CreatedAt);
        b.HasIndex(x => new { x.IsActive, x.IsDeleted });
        b.HasMany(x => x.UserRoles).WithOne(x => x.User).HasForeignKey(x => x.UserId).IsRequired();
        b.HasOne(x => x.Profile).WithOne(x => x.User).HasForeignKey<UserProfile>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Preference).WithOne(x => x.User).HasForeignKey<UserPreference>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.Ignore(x => x.FullName);
    }
}

internal sealed class AppRoleConfiguration : IEntityTypeConfiguration<AppRole>
{
    public void Configure(EntityTypeBuilder<AppRole> b)
    {
        b.ToTable("Roles");
        b.Property(x => x.Description).HasMaxLength(200);
        b.HasMany(x => x.UserRoles).WithOne(x => x.Role).HasForeignKey(x => x.RoleId).IsRequired();
    }
}

internal sealed class AppUserRoleConfiguration : IEntityTypeConfiguration<AppUserRole>
{
    public void Configure(EntityTypeBuilder<AppUserRole> b) => b.ToTable("UserRoles");
}

internal sealed class IdentityTablesConfiguration :
    IEntityTypeConfiguration<IdentityUserClaim<Guid>>, IEntityTypeConfiguration<IdentityUserLogin<Guid>>,
    IEntityTypeConfiguration<IdentityRoleClaim<Guid>>, IEntityTypeConfiguration<IdentityUserToken<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserClaim<Guid>> b) => b.ToTable("UserClaims");
    public void Configure(EntityTypeBuilder<IdentityUserLogin<Guid>> b) => b.ToTable("UserLogins");
    public void Configure(EntityTypeBuilder<IdentityRoleClaim<Guid>> b) => b.ToTable("RoleClaims");
    public void Configure(EntityTypeBuilder<IdentityUserToken<Guid>> b) => b.ToTable("UserTokens");
}

internal sealed class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> b)
    {
        b.ToTable("UserProfiles");
        b.HasIndex(x => x.UserId).IsUnique();
        b.Property(x => x.CurrentJobRole).HasMaxLength(100);
        b.Property(x => x.TargetJobRole).HasMaxLength(100);
        b.Property(x => x.Bio).HasMaxLength(1000);
    }
}

internal sealed class UserPreferredTechnologyConfiguration : IEntityTypeConfiguration<UserPreferredTechnology>
{
    public void Configure(EntityTypeBuilder<UserPreferredTechnology> b)
    {
        b.ToTable("UserPreferredTechnologies");
        b.HasKey(x => new { x.UserProfileId, x.TechnologyId });
        b.HasOne(x => x.UserProfile).WithMany(x => x.PreferredTechnologies).HasForeignKey(x => x.UserProfileId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Technology).WithMany().HasForeignKey(x => x.TechnologyId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class UserPreferenceConfiguration : IEntityTypeConfiguration<UserPreference>
{
    public void Configure(EntityTypeBuilder<UserPreference> b)
    {
        b.ToTable("UserPreferences");
        b.HasIndex(x => x.UserId).IsUnique();
        b.Property(x => x.TimeZoneId).HasMaxLength(64);
    }
}

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.ToTable("RefreshTokens");
        b.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        b.Property(x => x.ReplacedByTokenHash).HasMaxLength(64);
        b.Property(x => x.RevokedReason).HasMaxLength(40);
        b.Property(x => x.CreatedByIp).HasMaxLength(64);
        b.Property(x => x.DeviceName).HasMaxLength(100);
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasIndex(x => new { x.UserId, x.RevokedAt });
        b.HasIndex(x => x.FamilyId);
        b.HasIndex(x => x.ExpiresAt);
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class DeviceTokenConfiguration : IEntityTypeConfiguration<DeviceToken>
{
    public void Configure(EntityTypeBuilder<DeviceToken> b)
    {
        b.ToTable("DeviceTokens");
        b.Property(x => x.Token).HasMaxLength(450).IsRequired();
        b.Property(x => x.Platform).HasMaxLength(20);
        b.HasIndex(x => x.Token).IsUnique();
        b.HasIndex(x => x.UserId);
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class NotificationPreferenceConfiguration : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> b)
    {
        b.ToTable("NotificationPreferences");
        b.Property(x => x.Category).AsString();
        b.HasIndex(x => new { x.UserId, x.Category }).IsUnique();
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class InterviewReminderConfiguration : IEntityTypeConfiguration<InterviewReminder>
{
    public void Configure(EntityTypeBuilder<InterviewReminder> b)
    {
        b.ToTable("InterviewReminders");
        b.Property(x => x.Title).HasMaxLength(120).IsRequired();
        b.Property(x => x.CompanyName).HasMaxLength(120);
        b.Property(x => x.Notes).HasMaxLength(1000);
        b.HasIndex(x => new { x.UserId, x.ScheduledAt });
        b.HasIndex(x => new { x.ReminderSent, x.ScheduledAt });
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class SubscriptionPlanConfiguration : IEntityTypeConfiguration<SubscriptionPlan>
{
    public void Configure(EntityTypeBuilder<SubscriptionPlan> b)
    {
        b.ToTable("SubscriptionPlans");
        b.Property(x => x.Code).HasMaxLength(50).IsRequired();
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.Description).HasMaxLength(1000);
        b.Property(x => x.Tier).AsString();
        b.Property(x => x.Currency).HasMaxLength(3);
        b.Property(x => x.GooglePlayProductId).HasMaxLength(150);
        b.Property(x => x.GooglePlayBasePlanId).HasMaxLength(150);
        b.HasIndex(x => x.Code).IsUnique();
        b.HasIndex(x => x.GooglePlayProductId);
    }
}

internal sealed class UserSubscriptionConfiguration : IEntityTypeConfiguration<UserSubscription>
{
    public void Configure(EntityTypeBuilder<UserSubscription> b)
    {
        b.ToTable("UserSubscriptions");
        b.Property(x => x.Status).AsString();
        b.Property(x => x.Provider).AsString();
        b.Property(x => x.PurchaseTokenHash).HasMaxLength(64);
        b.Property(x => x.PurchaseToken).HasMaxLength(2000);
        b.Property(x => x.ExternalOrderId).HasMaxLength(150);
        b.Property(x => x.Notes).HasMaxLength(500);
        b.HasIndex(x => new { x.UserId, x.ExpiresAt });
        b.HasIndex(x => x.PurchaseTokenHash).IsUnique().HasFilter("[PurchaseTokenHash] IS NOT NULL");
        b.HasIndex(x => new { x.Status, x.ExpiresAt });
        b.HasOne(x => x.Plan).WithMany().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
