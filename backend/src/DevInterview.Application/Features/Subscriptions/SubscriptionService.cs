using DevInterview.Application.Abstractions;
using DevInterview.Application.Common;
using DevInterview.Contracts.Common;
using DevInterview.Contracts.Engagement;
using DevInterview.Domain.Entities;
using DevInterview.Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DevInterview.Application.Features.Subscriptions;

public sealed record VerifiedPurchase(
    string ProductId, string? BasePlanId, SubscriptionStatus Status, DateTime StartsAt, DateTime ExpiresAt, bool AutoRenewing,
    string? OrderId, bool NeedsAcknowledgement, string? ObfuscatedAccountId = null);

/// <summary>Server-side purchase verification (e.g. Google Play Developer API). Never trusts client claims.</summary>
public interface IPurchaseVerifier
{
    bool IsConfigured { get; }
    Task<VerifiedPurchase?> VerifySubscriptionAsync(string productId, string purchaseToken, CancellationToken ct);
    Task AcknowledgeAsync(string productId, string purchaseToken, CancellationToken ct);
}

public interface ISubscriptionService
{
    Task<IReadOnlyList<SubscriptionPlanDto>> GetPlansAsync(CancellationToken ct);
    Task<SubscriptionStatusDto> GetStatusAsync(Guid userId, CancellationToken ct);
    Task<IReadOnlyList<PurchaseHistoryItemDto>> GetHistoryAsync(Guid userId, CancellationToken ct);
    Task<SubscriptionStatusDto> VerifyGooglePlayAsync(Guid userId, VerifyGooglePlayPurchaseRequest request, CancellationToken ct);
    /// <summary>Handles a Google Play real-time developer notification by re-verifying the token with Google.</summary>
    Task HandleGooglePlayNotificationAsync(string productId, string purchaseToken, CancellationToken ct);
    /// <summary>Background job: re-verifies or expires subscriptions past their expiry time.</summary>
    Task<int> ProcessExpirationsAsync(CancellationToken ct);
}

public sealed class SubscriptionService : ISubscriptionService
{
    private readonly IAppDbContext _db;
    private readonly IPurchaseVerifier _verifier;
    private readonly IEntitlementService _entitlements;
    private readonly ITokenService _hasher;
    private readonly IClock _clock;
    private readonly ILogger<SubscriptionService> _logger;

    public SubscriptionService(IAppDbContext db, IPurchaseVerifier verifier, IEntitlementService entitlements, ITokenService hasher, IClock clock,
        ILogger<SubscriptionService> logger)
    {
        _db = db;
        _verifier = verifier;
        _entitlements = entitlements;
        _hasher = hasher;
        _clock = clock;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SubscriptionPlanDto>> GetPlansAsync(CancellationToken ct) =>
        (await _db.SubscriptionPlans.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.DisplayOrder).ToListAsync(ct))
        .Select(ToDto).ToList();

    public static SubscriptionPlanDto ToDto(SubscriptionPlan p) => new(p.Id, p.Code, p.Name, p.Description, p.Tier.ToString(), p.PriceMicros, p.Currency,
        p.BillingPeriodMonths, p.GooglePlayProductId, p.GooglePlayBasePlanId, Json.StringList(p.FeaturesJson));

    public async Task<SubscriptionStatusDto> GetStatusAsync(Guid userId, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var current = await _db.UserSubscriptions.AsNoTracking().Include(s => s.Plan)
            .Where(s => s.UserId == userId && s.ExpiresAt > now && s.Status != SubscriptionStatus.Revoked && s.Status != SubscriptionStatus.Pending)
            .OrderByDescending(s => s.ExpiresAt).FirstOrDefaultAsync(ct);
        var tier = await _entitlements.GetTierAsync(userId, ct);
        var usage = new List<UsageLimitDto>();
        foreach (var feature in new[] { UsageFeature.QuestionView, UsageFeature.CodeRun, UsageFeature.CodeSubmit, UsageFeature.AiAnswerEvaluation,
                     UsageFeature.AiCodeReview, UsageFeature.AiMockInterview })
            usage.Add(new UsageLimitDto(feature.ToString(), await _entitlements.GetUsageTodayAsync(userId, feature, ct), await _entitlements.GetLimitAsync(userId, feature, ct)));
        return new SubscriptionStatusDto(tier.ToString(), current?.Status.ToString(), current?.Plan.Name, current?.ExpiresAt, current?.AutoRenewing ?? false,
            current?.Provider.ToString(), usage);
    }

    public async Task<IReadOnlyList<PurchaseHistoryItemDto>> GetHistoryAsync(Guid userId, CancellationToken ct) =>
        await _db.UserSubscriptions.AsNoTracking().Where(s => s.UserId == userId).OrderByDescending(s => s.StartsAt)
            .Select(s => new PurchaseHistoryItemDto(s.Id, s.Plan.Name, s.Status.ToString(), s.Provider.ToString(), s.StartsAt, s.ExpiresAt, s.ExternalOrderId))
            .ToListAsync(ct);

    public async Task<SubscriptionStatusDto> VerifyGooglePlayAsync(Guid userId, VerifyGooglePlayPurchaseRequest request, CancellationToken ct)
    {
        if (!_verifier.IsConfigured)
            throw new ServiceUnavailableException(ErrorCodes.BillingUnavailable, "In-app purchases are not configured on this server.");
        var verified = await _verifier.VerifySubscriptionAsync(request.ProductId, request.PurchaseToken, ct)
                       ?? throw new ValidationAppException("The purchase could not be verified with Google Play.");
        var plan = await _db.SubscriptionPlans.FirstOrDefaultAsync(p => p.GooglePlayProductId == verified.ProductId &&
                       (verified.BasePlanId == null || p.GooglePlayBasePlanId == null || p.GooglePlayBasePlanId == verified.BasePlanId), ct)
                   ?? throw new ValidationAppException("This product is not a DevInterview plan.");

        // The app launches checkout with the user's id as the obfuscated account id; a token bought by another account is rejected.
        if (verified.ObfuscatedAccountId is { Length: > 0 } owner && !string.Equals(owner, userId.ToString(), StringComparison.OrdinalIgnoreCase))
            throw new ConflictException("This purchase belongs to a different DevInterview account.");

        var hash = _hasher.HashToken(request.PurchaseToken);
        var existing = await _db.UserSubscriptions.FirstOrDefaultAsync(s => s.PurchaseTokenHash == hash, ct);
        if (existing is not null && existing.UserId != userId)
            throw new ConflictException("This purchase is already linked to another DevInterview account.");
        if (existing is null)
        {
            existing = new UserSubscription { UserId = userId, PlanId = plan.Id, Provider = PaymentProvider.GooglePlay, PurchaseTokenHash = hash, PurchaseToken = request.PurchaseToken };
            _db.UserSubscriptions.Add(existing);
        }
        Apply(existing, verified, plan.Id);
        await _db.SaveChangesAsync(ct);
        if (verified.NeedsAcknowledgement && verified.Status == SubscriptionStatus.Active)
        {
            try { await _verifier.AcknowledgeAsync(request.ProductId, request.PurchaseToken, ct); }
            catch (Exception ex) when (ex is not OperationCanceledException) { _logger.LogError(ex, "Failed to acknowledge purchase for subscription {SubscriptionId}", existing.Id); }
        }
        _logger.LogInformation("Verified Google Play subscription {SubscriptionId} for user {UserId}: {Status}", existing.Id, userId, verified.Status);
        return await GetStatusAsync(userId, ct);
    }

    private void Apply(UserSubscription s, VerifiedPurchase v, Guid planId)
    {
        s.PlanId = planId;
        s.Status = v.Status;
        s.StartsAt = v.StartsAt;
        s.ExpiresAt = v.ExpiresAt;
        s.AutoRenewing = v.AutoRenewing;
        s.ExternalOrderId = v.OrderId;
        s.LastVerifiedAt = _clock.UtcNow;
    }

    public async Task HandleGooglePlayNotificationAsync(string productId, string purchaseToken, CancellationToken ct)
    {
        if (!_verifier.IsConfigured) return;
        var hash = _hasher.HashToken(purchaseToken);
        var subscription = await _db.UserSubscriptions.FirstOrDefaultAsync(s => s.PurchaseTokenHash == hash, ct);
        if (subscription is null) return; // purchase not linked yet; the app will call verify
        var verified = await _verifier.VerifySubscriptionAsync(productId, purchaseToken, ct);
        if (verified is null) return;
        Apply(subscription, verified, subscription.PlanId);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> ProcessExpirationsAsync(CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var due = await _db.UserSubscriptions.Where(s => s.ExpiresAt <= now &&
                (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.GracePeriod || s.Status == SubscriptionStatus.Cancelled))
            .Take(500).ToListAsync(ct);
        foreach (var s in due)
        {
            if (s.Provider == PaymentProvider.GooglePlay && s.PurchaseToken is not null && _verifier.IsConfigured)
            {
                var productId = await _db.SubscriptionPlans.Where(p => p.Id == s.PlanId).Select(p => p.GooglePlayProductId).FirstOrDefaultAsync(ct);
                try
                {
                    var verified = productId is null ? null : await _verifier.VerifySubscriptionAsync(productId, s.PurchaseToken, ct);
                    if (verified is not null && verified.ExpiresAt > now) { Apply(s, verified, s.PlanId); continue; } // renewed
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Re-verification failed for subscription {SubscriptionId}", s.Id);
                }
            }
            s.Status = SubscriptionStatus.Expired;
        }
        await _db.SaveChangesAsync(ct);
        return due.Count(s => s.Status == SubscriptionStatus.Expired);
    }
}

public sealed class VerifyGooglePlayPurchaseRequestValidator : AbstractValidator<VerifyGooglePlayPurchaseRequest>
{
    public VerifyGooglePlayPurchaseRequestValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty().MaximumLength(150);
        RuleFor(x => x.PurchaseToken).NotEmpty().MaximumLength(4096);
    }
}
