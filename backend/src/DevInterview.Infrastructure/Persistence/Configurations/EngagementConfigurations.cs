using DevInterview.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevInterview.Infrastructure.Persistence.Configurations;

internal sealed class UserActivityLogConfiguration : IEntityTypeConfiguration<UserActivityLog>
{
    public void Configure(EntityTypeBuilder<UserActivityLog> b)
    {
        b.ToTable("UserActivityLogs");
        b.Property(x => x.ActivityType).AsString();
        b.HasIndex(x => new { x.UserId, x.LocalDate });
        b.HasIndex(x => new { x.UserId, x.OccurredAt });
        b.HasIndex(x => x.OccurredAt);
        b.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class UserPerformanceStatConfiguration : IEntityTypeConfiguration<UserPerformanceStat>
{
    public void Configure(EntityTypeBuilder<UserPerformanceStat> b)
    {
        b.ToTable("UserPerformanceStats");
        b.HasIndex(x => new { x.UserId, x.TechnologyId }).IsUnique();
        b.HasOne(x => x.Technology).WithMany().HasForeignKey(x => x.TechnologyId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
    }
}

internal sealed class AchievementConfiguration : IEntityTypeConfiguration<Achievement>
{
    public void Configure(EntityTypeBuilder<Achievement> b)
    {
        b.ToTable("Achievements");
        b.Property(x => x.Code).HasMaxLength(40).IsRequired();
        b.Property(x => x.Name).HasMaxLength(80).IsRequired();
        b.Property(x => x.Description).HasMaxLength(300);
        b.Property(x => x.IconKey).HasMaxLength(40);
        b.HasIndex(x => x.Code).IsUnique();
    }
}

internal sealed class UserAchievementConfiguration : IEntityTypeConfiguration<UserAchievement>
{
    public void Configure(EntityTypeBuilder<UserAchievement> b)
    {
        b.ToTable("UserAchievements");
        b.HasIndex(x => new { x.UserId, x.AchievementId }).IsUnique();
        b.HasOne(x => x.Achievement).WithMany().HasForeignKey(x => x.AchievementId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class UserStreakConfiguration : IEntityTypeConfiguration<UserStreak>
{
    public void Configure(EntityTypeBuilder<UserStreak> b)
    {
        b.ToTable("UserStreaks");
        b.HasIndex(x => x.UserId).IsUnique();
        b.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class UserXpTransactionConfiguration : IEntityTypeConfiguration<UserXpTransaction>
{
    public void Configure(EntityTypeBuilder<UserXpTransaction> b)
    {
        b.ToTable("UserXPTransactions");
        b.Property(x => x.SourceType).AsString();
        b.Property(x => x.SourceKey).HasMaxLength(120).IsRequired();
        b.Property(x => x.Reason).HasMaxLength(200);
        // Duplicate reward prevention: each (user, source) can be rewarded once.
        b.HasIndex(x => new { x.UserId, x.SourceType, x.SourceKey }).IsUnique();
        b.HasIndex(x => new { x.UserId, x.CreatedAt });
        b.HasIndex(x => x.CreatedAt);
        b.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class UsageCounterConfiguration : IEntityTypeConfiguration<UsageCounter>
{
    public void Configure(EntityTypeBuilder<UsageCounter> b)
    {
        b.ToTable("UsageCounters");
        b.Property(x => x.Feature).AsString();
        b.HasIndex(x => new { x.UserId, x.Feature, x.Date }).IsUnique();
        b.HasIndex(x => x.Date);
        b.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.ToTable("Notifications");
        b.Property(x => x.Category).AsString();
        b.Property(x => x.Title).HasMaxLength(120).IsRequired();
        b.Property(x => x.Body).HasMaxLength(600);
        b.Property(x => x.DeepLink).HasMaxLength(300);
        b.HasIndex(x => new { x.UserId, x.ReadAt });
        b.HasIndex(x => new { x.UserId, x.CreatedAt });
        b.HasIndex(x => new { x.UserId, x.Category, x.CreatedAt });
        b.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.ToTable("AuditLogs");
        b.Property(x => x.Action).HasMaxLength(60).IsRequired();
        b.Property(x => x.EntityType).HasMaxLength(60).IsRequired();
        b.Property(x => x.EntityId).HasMaxLength(100);
        b.Property(x => x.Details).HasMaxLength(4000);
        b.Property(x => x.IpAddress).HasMaxLength(64);
        b.HasIndex(x => x.CreatedAt);
        b.HasIndex(x => new { x.EntityType, x.EntityId });
        b.HasIndex(x => x.UserId);
    }
}

internal sealed class AppSettingConfiguration : IEntityTypeConfiguration<AppSetting>
{
    public void Configure(EntityTypeBuilder<AppSetting> b)
    {
        b.ToTable("AppSettings");
        b.Property(x => x.Key).HasMaxLength(100).IsRequired();
        b.Property(x => x.Value).HasMaxLength(4000);
        b.Property(x => x.Description).HasMaxLength(500);
        b.HasIndex(x => x.Key).IsUnique();
    }
}
