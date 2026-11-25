using DevInterview.Application.Abstractions;
using DevInterview.Application.Common;
using DevInterview.Domain.Entities;
using DevInterview.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DevInterview.Application.Features.Subscriptions;

public interface IEntitlementService
{
    Task<SubscriptionTier> GetTierAsync(Guid userId, CancellationToken ct);
    Task<bool> IsPremiumAsync(Guid userId, CancellationToken ct);
    /// <summary>Throws <see cref="PremiumRequiredException"/> unless the user has an active premium entitlement.</summary>
    Task EnsurePremiumAsync(Guid userId, string featureName, CancellationToken ct);
    /// <summary>Increments today's usage counter, throwing <see cref="QuotaExceededException"/> when the tier limit is reached.</summary>
    Task ConsumeAsync(Guid userId, UsageFeature feature, CancellationToken ct);
    Task<int?> GetLimitAsync(Guid userId, UsageFeature feature, CancellationToken ct);
    Task<int> GetUsageTodayAsync(Guid userId, UsageFeature feature, CancellationToken ct);
}

/// <summary>Entitlements are always derived from server-side subscription records, never from client claims.</summary>
public sealed class EntitlementService : IEntitlementService
{
    private readonly IAppDbContext _db;
    private readonly IClock _clock;
    private readonly PlanLimitsOptions _limits;

    public EntitlementService(IAppDbContext db, IClock clock, IOptions<PlanLimitsOptions> limits)
    {
        _db = db;
        _clock = clock;
        _limits = limits.Value;
    }

    public async Task<SubscriptionTier> GetTierAsync(Guid userId, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var hasPremium = await _db.UserSubscriptions.AnyAsync(s =>
            s.UserId == userId && s.ExpiresAt > now && s.Plan.Tier == SubscriptionTier.Premium &&
            (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.GracePeriod || s.Status == SubscriptionStatus.Cancelled), ct);
        if (hasPremium) return SubscriptionTier.Premium;

        // Staff accounts get premium access so they can preview gated content.
        var isStaff = await _db.UserRoles.AnyAsync(ur => ur.UserId == userId &&
            RoleNames.AdminPanel.Contains(ur.Role.Name!), ct);
        return isStaff ? SubscriptionTier.Premium : SubscriptionTier.Free;
    }

    public async Task<bool> IsPremiumAsync(Guid userId, CancellationToken ct) =>
        await GetTierAsync(userId, ct) == SubscriptionTier.Premium;

    public async Task EnsurePremiumAsync(Guid userId, string featureName, CancellationToken ct)
    {
        if (!await IsPremiumAsync(userId, ct)) throw new PremiumRequiredException(featureName);
    }

    public async Task<int?> GetLimitAsync(Guid userId, UsageFeature feature, CancellationToken ct)
    {
        var premium = await IsPremiumAsync(userId, ct);
        return LimitFor(feature, premium);
    }

    internal int? LimitFor(UsageFeature feature, bool premium) => feature switch
    {
        UsageFeature.QuestionView => premium ? null : _limits.FreeDailyQuestionViews,
        UsageFeature.CodeRun => premium ? _limits.PremiumDailyCodeRuns : _limits.FreeDailyCodeRuns,
        UsageFeature.CodeSubmit => premium ? _limits.PremiumDailyCodeSubmissions : _limits.FreeDailyCodeSubmissions,
        UsageFeature.AiAnswerEvaluation => premium ? _limits.PremiumDailyAnswerEvaluations : 0,
        UsageFeature.AiCodeReview => premium ? _limits.PremiumDailyCodeReviews : 0,
        UsageFeature.AiCodeExplanation => premium ? _limits.PremiumDailyCodeExplanations : 0,
        UsageFeature.AiMockInterview => premium ? _limits.PremiumDailyMockInterviews : 0,
        UsageFeature.AiQuestionGeneration => _limits.AdminDailyQuestionGenerations,
        _ => null
    };

    public async Task<int> GetUsageTodayAsync(Guid userId, UsageFeature feature, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(_clock.UtcNow);
        return await _db.UsageCounters.Where(u => u.UserId == userId && u.Feature == feature && u.Date == today)
            .Select(u => u.Count).FirstOrDefaultAsync(ct);
    }

    public async Task ConsumeAsync(Guid userId, UsageFeature feature, CancellationToken ct)
    {
        var limit = await GetLimitAsync(userId, feature, ct);
        if (limit is 0) throw new PremiumRequiredException(FeatureName(feature));
        var today = DateOnly.FromDateTime(_clock.UtcNow);
        var max = limit ?? int.MaxValue;

        for (var attempt = 0; attempt < 3; attempt++)
        {
            // Atomic conditional increment: concurrent requests cannot exceed the limit.
            var updated = await _db.UsageCounters
                .Where(u => u.UserId == userId && u.Feature == feature && u.Date == today && u.Count < max)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.Count, u => u.Count + 1).SetProperty(u => u.UpdatedAt, _clock.UtcNow), ct);
            if (updated == 1) return;

            var exists = await _db.UsageCounters.AnyAsync(u => u.UserId == userId && u.Feature == feature && u.Date == today, ct);
            if (exists)
                throw new QuotaExceededException($"Daily limit of {max} reached for {FeatureName(feature)}. It resets at 00:00 UTC.");

            var counter = new UsageCounter { UserId = userId, Feature = feature, Date = today, Count = 1 };
            _db.UsageCounters.Add(counter);
            try
            {
                await _db.SaveChangesAsync(ct);
                return;
            }
            catch (DbUpdateException)
            {
                // Another request inserted today's counter first; retry the conditional increment.
                _db.UsageCounters.Remove(counter); // detaches the failed insert
            }
        }
        throw new QuotaExceededException($"Could not record usage for {FeatureName(feature)}. Please retry.");
    }

    public static string FeatureName(UsageFeature feature) => feature switch
    {
        UsageFeature.QuestionView => "question practice",
        UsageFeature.CodeRun => "code runs",
        UsageFeature.CodeSubmit => "code submissions",
        UsageFeature.AiAnswerEvaluation => "AI answer evaluation",
        UsageFeature.AiCodeReview => "AI code review",
        UsageFeature.AiCodeExplanation => "AI code explanation",
        UsageFeature.AiMockInterview => "AI mock interviews",
        UsageFeature.AiQuestionGeneration => "AI question generation",
        _ => feature.ToString()
    };
}
