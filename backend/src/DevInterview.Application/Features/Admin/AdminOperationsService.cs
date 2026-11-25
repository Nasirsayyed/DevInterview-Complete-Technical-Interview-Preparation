using DevInterview.Application.Abstractions;
using DevInterview.Application.Common;
using DevInterview.Application.Features.Ai;
using DevInterview.Application.Features.Notifications;
using DevInterview.Application.Features.Subscriptions;
using DevInterview.Contracts.Admin;
using DevInterview.Contracts.Common;
using DevInterview.Contracts.Engagement;
using DevInterview.Domain.Entities;
using DevInterview.Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace DevInterview.Application.Features.Admin;

public interface IAdminOperationsService
{
    Task<AdminDashboardDto> GetDashboardAsync(CancellationToken ct);
    Task<PagedResult<AdminUserDto>> GetUsersAsync(AdminUserQuery query, CancellationToken ct);
    Task<AdminUserDto> GetUserAsync(Guid id, CancellationToken ct);
    Task<AdminUserDto> UpdateRolesAsync(Guid id, UpdateUserRolesRequest request, Guid actorId, CancellationToken ct);
    Task<AdminUserDto> SetActiveAsync(Guid id, SetUserActiveRequest request, Guid actorId, CancellationToken ct);
    Task<AdminSubscriptionDto> GrantSubscriptionAsync(Guid userId, GrantSubscriptionRequest request, CancellationToken ct);
    Task RevokeSubscriptionAsync(Guid subscriptionId, CancellationToken ct);
    Task<PagedResult<AdminSubscriptionDto>> GetSubscriptionsAsync(PageQuery query, CancellationToken ct);
    Task<IReadOnlyList<SubscriptionPlanDto>> GetPlansAsync(CancellationToken ct);
    Task<SubscriptionPlanDto> UpsertPlanAsync(Guid? id, SubscriptionPlanUpsertRequest request, CancellationToken ct);

    Task<IReadOnlyList<AiPromptDto>> GetPromptsAsync(CancellationToken ct);
    Task<AiPromptDto> SavePromptAsync(AiPromptUpsertRequest request, CancellationToken ct);
    Task<IReadOnlyList<AiProviderSettingDto>> GetProvidersAsync(CancellationToken ct);
    Task<AiUsageSummaryDto> GetAiUsageAsync(int days, CancellationToken ct);

    Task<BroadcastResultDto> BroadcastAsync(BroadcastNotificationRequest request, CancellationToken ct);
    Task<PagedResult<AuditLogDto>> GetAuditLogsAsync(AuditLogQuery query, CancellationToken ct);
    Task<IReadOnlyList<AppSettingDto>> GetSettingsAsync(CancellationToken ct);
    Task<AppSettingDto> SaveSettingAsync(AppSettingDto request, CancellationToken ct);
    Task<ContentReportDto> GetContentReportAsync(CancellationToken ct);
}

public sealed class AdminOperationsService : IAdminOperationsService
{
    private readonly IAppDbContext _db;
    private readonly IAuditLogger _audit;
    private readonly INotificationService _notifications;
    private readonly IEnumerable<IAiProvider> _providers;
    private readonly IClock _clock;

    public AdminOperationsService(IAppDbContext db, IAuditLogger audit, INotificationService notifications, IEnumerable<IAiProvider> providers, IClock clock)
    {
        _db = db;
        _audit = audit;
        _notifications = notifications;
        _providers = providers;
        _clock = clock;
    }

    public async Task<AdminDashboardDto> GetDashboardAsync(CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var weekAgo = now.AddDays(-7);
        var users = _db.Users.Where(u => !u.IsDeleted);
        var premium = await _db.UserSubscriptions.Where(s => s.ExpiresAt > now && s.Plan.Tier == SubscriptionTier.Premium &&
            (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.GracePeriod || s.Status == SubscriptionStatus.Cancelled))
            .Select(s => s.UserId).Distinct().CountAsync(ct);
        var from = DateOnly.FromDateTime(now.AddDays(-29));
        var fromUtc = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var signups = (await users.Where(u => u.CreatedAt >= fromUtc).Select(u => u.CreatedAt).ToListAsync(ct)).GroupBy(d => DateOnly.FromDateTime(d)).ToDictionary(g => g.Key, g => g.Count());
        var activity = await _db.UserActivityLogs.Where(a => a.OccurredAt >= fromUtc).GroupBy(a => a.LocalDate).Select(g => new { g.Key, Count = g.Select(a => a.UserId).Distinct().Count() }).ToListAsync(ct);
        var days = Enumerable.Range(0, 30).Select(i => from.AddDays(i)).ToList();
        var aiCost = await _db.AiUsageLogs.Where(l => l.CreatedAt >= weekAgo).Select(l => l.EstimatedCostUsd).ToListAsync(ct);
        return new AdminDashboardDto(
            await users.CountAsync(ct),
            await _db.UserActivityLogs.Where(a => a.OccurredAt >= weekAgo).Select(a => a.UserId).Distinct().CountAsync(ct),
            await users.CountAsync(u => u.CreatedAt >= weekAgo, ct), premium,
            await _db.Questions.CountAsync(q => !q.IsDeleted, ct), await _db.Questions.CountAsync(q => !q.IsDeleted && q.Status == ContentStatus.Published, ct),
            await _db.Questions.CountAsync(q => !q.IsDeleted && q.Status == ContentStatus.Draft, ct), await _db.CodingChallenges.CountAsync(c => !c.IsDeleted, ct),
            await _db.QuizAttempts.CountAsync(a => a.StartedAt >= weekAgo, ct), await _db.CodeSubmissions.CountAsync(s => s.CreatedAt >= weekAgo, ct),
            await _db.MockInterviews.CountAsync(m => m.StartedAt >= weekAgo, ct), await _db.AiUsageLogs.CountAsync(l => l.CreatedAt >= weekAgo, ct), aiCost.Sum(),
            await _db.QuestionReports.CountAsync(r => r.Status == QuestionReportStatus.Open, ct),
            days.Select(d => new TrendPointDto(d, signups.GetValueOrDefault(d))).ToList(),
            days.Select(d => new TrendPointDto(d, activity.FirstOrDefault(a => a.Key == d)?.Count ?? 0)).ToList());
    }

    private IQueryable<AdminUserDto> ProjectUsers(IQueryable<AppUser> q)
    {
        var now = _clock.UtcNow;
        return q.Select(u => new
        {
            u,
            Roles = u.UserRoles.Select(r => r.Role.Name!).ToList(),
            Premium = _db.UserSubscriptions.Where(s => s.UserId == u.Id && s.ExpiresAt > now && s.Plan.Tier == SubscriptionTier.Premium &&
                    (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.GracePeriod || s.Status == SubscriptionStatus.Cancelled))
                .Max(s => (DateTime?)s.ExpiresAt)
        }).Select(x => new AdminUserDto(x.u.Id, x.u.Email!, x.u.FirstName + " " + x.u.LastName, x.Roles, x.u.IsActive, x.u.EmailConfirmed,
            x.u.CreatedAt, x.u.LastLoginAt, x.Premium != null ? "Premium" : "Free", x.Premium));
    }

    public async Task<PagedResult<AdminUserDto>> GetUsersAsync(AdminUserQuery query, CancellationToken ct)
    {
        var q = _db.Users.AsNoTracking().Where(u => !u.IsDeleted);
        if (!string.IsNullOrWhiteSpace(query.Search))
            q = q.Where(u => u.Email!.Contains(query.Search) || u.FirstName.Contains(query.Search) || u.LastName.Contains(query.Search));
        if (!string.IsNullOrWhiteSpace(query.Role)) q = q.Where(u => u.UserRoles.Any(r => r.Role.Name == query.Role));
        if (query.IsActive is { } active) q = q.Where(u => u.IsActive == active);
        return await ProjectUsers(q.OrderByDescending(u => u.CreatedAt)).ToPagedAsync(query, ct);
    }

    public async Task<AdminUserDto> GetUserAsync(Guid id, CancellationToken ct) =>
        await ProjectUsers(_db.Users.AsNoTracking().Where(u => u.Id == id && !u.IsDeleted)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException("User", id);

    public async Task<AdminUserDto> UpdateRolesAsync(Guid id, UpdateUserRolesRequest request, Guid actorId, CancellationToken ct)
    {
        var roles = request.Roles.Distinct().ToList();
        if (roles.Any(r => !RoleNames.All.Contains(r))) throw ValidationAppException.For("roles", $"Roles must be among {string.Join(", ", RoleNames.All)}.");
        if (!roles.Contains(RoleNames.User)) roles.Add(RoleNames.User);
        var actorIsSuper = await _db.UserRoles.AnyAsync(r => r.UserId == actorId && r.Role.Name == RoleNames.SuperAdmin, ct);
        var targetIsSuper = await _db.UserRoles.AnyAsync(r => r.UserId == id && r.Role.Name == RoleNames.SuperAdmin, ct);
        if (!actorIsSuper && (roles.Contains(RoleNames.SuperAdmin) || roles.Contains(RoleNames.Admin) || targetIsSuper))
            throw new ForbiddenException("Only a Super Admin can grant or revoke Admin and Super Admin roles.");
        if (id == actorId && targetIsSuper && !roles.Contains(RoleNames.SuperAdmin))
            throw new BusinessRuleException(ErrorCodes.Forbidden, "You cannot remove your own Super Admin role.");
        if (!await _db.Users.AnyAsync(u => u.Id == id && !u.IsDeleted, ct)) throw new NotFoundException("User", id);

        var roleIds = await _db.Roles.Where(r => roles.Contains(r.Name!)).ToDictionaryAsync(r => r.Name!, r => r.Id, ct);
        var current = await _db.UserRoles.Where(r => r.UserId == id).ToListAsync(ct);
        foreach (var ur in current.Where(ur => !roleIds.ContainsValue(ur.RoleId))) _db.UserRoles.Remove(ur);
        foreach (var rid in roleIds.Values.Where(rid => current.All(c => c.RoleId != rid))) _db.UserRoles.Add(new AppUserRole { UserId = id, RoleId = rid });
        // Force re-login so new role claims take effect.
        await _db.RefreshTokens.Where(t => t.UserId == id && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, _clock.UtcNow).SetProperty(t => t.RevokedReason, "roles-changed"), ct);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("UpdateRoles", nameof(AppUser), id.ToString(), new { roles }, ct);
        return await GetUserAsync(id, ct);
    }

    public async Task<AdminUserDto> SetActiveAsync(Guid id, SetUserActiveRequest request, Guid actorId, CancellationToken ct)
    {
        if (id == actorId && !request.IsActive) throw new BusinessRuleException(ErrorCodes.Forbidden, "You cannot deactivate your own account.");
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted, ct) ?? throw new NotFoundException("User", id);
        if (await _db.UserRoles.AnyAsync(r => r.UserId == id && r.Role.Name == RoleNames.SuperAdmin, ct) &&
            !await _db.UserRoles.AnyAsync(r => r.UserId == actorId && r.Role.Name == RoleNames.SuperAdmin, ct))
            throw new ForbiddenException("Only a Super Admin can change another Super Admin.");
        user.IsActive = request.IsActive;
        if (!request.IsActive)
            await _db.RefreshTokens.Where(t => t.UserId == id && t.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, _clock.UtcNow).SetProperty(t => t.RevokedReason, "deactivated"), ct);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(request.IsActive ? "Activate" : "Deactivate", nameof(AppUser), id.ToString(), null, ct);
        return await GetUserAsync(id, ct);
    }

    public async Task<AdminSubscriptionDto> GrantSubscriptionAsync(Guid userId, GrantSubscriptionRequest request, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, ct) ?? throw new NotFoundException("User", userId);
        var plan = await _db.SubscriptionPlans.FirstOrDefaultAsync(p => p.Id == request.PlanId, ct) ?? throw new NotFoundException("Plan", request.PlanId);
        var now = _clock.UtcNow;
        var currentEnd = await _db.UserSubscriptions.Where(s => s.UserId == userId && s.ExpiresAt > now && s.Status == SubscriptionStatus.Active)
            .MaxAsync(s => (DateTime?)s.ExpiresAt, ct);
        var start = currentEnd ?? now;
        var sub = new UserSubscription
        {
            UserId = userId, PlanId = plan.Id, Status = SubscriptionStatus.Active, Provider = PaymentProvider.AdminGrant, StartsAt = start,
            ExpiresAt = start.AddMonths(request.Months), AutoRenewing = false, Notes = request.Notes
        };
        _db.UserSubscriptions.Add(sub);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("GrantSubscription", nameof(UserSubscription), sub.Id.ToString(), new { userId, plan = plan.Code, request.Months }, ct);
        await _notifications.NotifyAsync(userId, NotificationCategory.System, "Premium activated", $"You now have {plan.Name} until {sub.ExpiresAt:MMM d, yyyy}.", "devinterview://subscription", ct);
        return new AdminSubscriptionDto(sub.Id, userId, user.Email!, plan.Name, sub.Status.ToString(), sub.Provider.ToString(), sub.StartsAt, sub.ExpiresAt, false, null);
    }

    public async Task RevokeSubscriptionAsync(Guid subscriptionId, CancellationToken ct)
    {
        var sub = await _db.UserSubscriptions.FirstOrDefaultAsync(s => s.Id == subscriptionId, ct) ?? throw new NotFoundException("Subscription", subscriptionId);
        sub.Status = SubscriptionStatus.Revoked;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("RevokeSubscription", nameof(UserSubscription), subscriptionId.ToString(), null, ct);
    }

    public async Task<PagedResult<AdminSubscriptionDto>> GetSubscriptionsAsync(PageQuery query, CancellationToken ct)
    {
        var q = _db.UserSubscriptions.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
            q = q.Where(s => _db.Users.Any(u => u.Id == s.UserId && u.Email!.Contains(query.Search)));
        return await q.OrderByDescending(s => s.CreatedAt).Select(s => new AdminSubscriptionDto(s.Id, s.UserId,
            _db.Users.Where(u => u.Id == s.UserId).Select(u => u.Email!).FirstOrDefault() ?? "", s.Plan.Name, s.Status.ToString(), s.Provider.ToString(),
            s.StartsAt, s.ExpiresAt, s.AutoRenewing, s.ExternalOrderId)).ToPagedAsync(query, ct);
    }

    public async Task<IReadOnlyList<SubscriptionPlanDto>> GetPlansAsync(CancellationToken ct) =>
        (await _db.SubscriptionPlans.AsNoTracking().OrderBy(p => p.DisplayOrder).ToListAsync(ct)).Select(SubscriptionService.ToDto).ToList();

    public async Task<SubscriptionPlanDto> UpsertPlanAsync(Guid? id, SubscriptionPlanUpsertRequest r, CancellationToken ct)
    {
        if (await _db.SubscriptionPlans.AnyAsync(p => p.Code == r.Code && p.Id != id, ct)) throw new ConflictException($"Plan code '{r.Code}' already exists.");
        var plan = id is null ? new SubscriptionPlan() : await _db.SubscriptionPlans.FirstOrDefaultAsync(p => p.Id == id, ct) ?? throw new NotFoundException("Plan", id);
        if (id is null) _db.SubscriptionPlans.Add(plan);
        plan.Code = r.Code.Trim(); plan.Name = r.Name.Trim(); plan.Description = r.Description; plan.Tier = EnumParser.Parse<SubscriptionTier>(r.Tier, "tier");
        plan.PriceMicros = r.PriceMicros; plan.Currency = r.Currency.ToUpperInvariant(); plan.BillingPeriodMonths = r.BillingPeriodMonths;
        plan.GooglePlayProductId = r.GooglePlayProductId; plan.GooglePlayBasePlanId = r.GooglePlayBasePlanId; plan.FeaturesJson = Json.Serialize(r.Features);
        plan.IsActive = r.IsActive; plan.DisplayOrder = r.DisplayOrder;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(id is null ? "Create" : "Update", nameof(SubscriptionPlan), plan.Id.ToString(), new { plan.Code, plan.PriceMicros }, ct);
        return SubscriptionService.ToDto(plan);
    }

    public async Task<IReadOnlyList<AiPromptDto>> GetPromptsAsync(CancellationToken ct)
    {
        var stored = await _db.AiPromptTemplates.AsNoTracking().ToListAsync(ct);
        return DefaultPrompts.Keys.Select(key =>
        {
            var active = stored.Where(p => p.Key == key && p.IsActive).OrderByDescending(p => p.Version).FirstOrDefault();
            return active is null
                ? new AiPromptDto(Guid.Empty, key, 0, DefaultPrompts.Get(key), "Built-in default", true, DateTime.MinValue)
                : new AiPromptDto(active.Id, key, active.Version, active.SystemPrompt, active.Notes, true, active.UpdatedAt);
        }).ToList();
    }

    /// <summary>Saves a new version of a prompt (previous versions are kept for audit/rollback).</summary>
    public async Task<AiPromptDto> SavePromptAsync(AiPromptUpsertRequest request, CancellationToken ct)
    {
        if (!DefaultPrompts.Keys.Contains(request.Key)) throw ValidationAppException.For("key", "Unknown prompt key.");
        var latest = await _db.AiPromptTemplates.Where(p => p.Key == request.Key).MaxAsync(p => (int?)p.Version, ct) ?? 0;
        await _db.AiPromptTemplates.Where(p => p.Key == request.Key && p.IsActive).ExecuteUpdateAsync(s => s.SetProperty(p => p.IsActive, false), ct);
        var prompt = new AiPromptTemplate { Key = request.Key, Version = latest + 1, SystemPrompt = request.SystemPrompt, Notes = request.Notes, IsActive = request.IsActive };
        _db.AiPromptTemplates.Add(prompt);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("SavePrompt", nameof(AiPromptTemplate), prompt.Id.ToString(), new { prompt.Key, prompt.Version }, ct);
        return new AiPromptDto(prompt.Id, prompt.Key, prompt.Version, prompt.SystemPrompt, prompt.Notes, prompt.IsActive, prompt.UpdatedAt);
    }

    public Task<IReadOnlyList<AiProviderSettingDto>> GetProvidersAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<AiProviderSettingDto>>(_providers.Select((p, i) => new AiProviderSettingDto(Guid.Empty, p.Name, p.Model, p.IsConfigured, i,
            p.InputCostPerMillionTokens, p.OutputCostPerMillionTokens, p.IsConfigured)).ToList());

    public async Task<AiUsageSummaryDto> GetAiUsageAsync(int days, CancellationToken ct)
    {
        var since = _clock.UtcNow.AddDays(-Math.Clamp(days, 1, 365));
        var logs = await _db.AiUsageLogs.AsNoTracking().Where(l => l.CreatedAt >= since)
            .Select(l => new { l.Feature, l.Success, l.PromptTokens, l.CompletionTokens, l.EstimatedCostUsd, l.LatencyMs }).ToListAsync(ct);
        return new AiUsageSummaryDto(logs.Count, logs.Count(l => !l.Success), logs.Sum(l => (long)l.PromptTokens), logs.Sum(l => (long)l.CompletionTokens),
            logs.Sum(l => l.EstimatedCostUsd),
            logs.GroupBy(l => l.Feature).Select(g => new AiUsageByFeatureDto(g.Key.ToString(), g.Count(), g.Sum(l => l.EstimatedCostUsd), (int)g.Average(l => l.LatencyMs))).ToList());
    }

    public async Task<BroadcastResultDto> BroadcastAsync(BroadcastNotificationRequest request, CancellationToken ct)
    {
        var category = EnumParser.Parse<NotificationCategory>(request.Category, "category");
        var now = _clock.UtcNow;
        var users = _db.Users.Where(u => u.IsActive && !u.IsDeleted);
        IQueryable<Guid> premiumIds = _db.UserSubscriptions.Where(s => s.ExpiresAt > now && s.Status != SubscriptionStatus.Revoked && s.Status != SubscriptionStatus.Expired).Select(s => s.UserId);
        users = request.Audience.ToLowerInvariant() switch
        {
            "premium" => users.Where(u => premiumIds.Contains(u.Id)),
            "free" => users.Where(u => !premiumIds.Contains(u.Id)),
            "all" => users,
            _ => throw ValidationAppException.For("audience", "Audience must be All, Premium or Free.")
        };
        var ids = await users.Select(u => u.Id).ToListAsync(ct);
        var broadcastId = Guid.NewGuid();
        var pushed = 0;
        foreach (var userId in ids)
        {
            if (request.SendPush)
            {
                await _notifications.NotifyAsync(userId, category, request.Title, request.Body, request.DeepLink, ct, broadcastId);
                pushed++;
            }
            else
            {
                _db.Notifications.Add(new Notification { UserId = userId, Category = category, Title = request.Title, Body = request.Body, DeepLink = request.DeepLink, BroadcastId = broadcastId });
            }
        }
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Broadcast", nameof(Notification), broadcastId.ToString(), new { request.Title, request.Audience, Recipients = ids.Count }, ct);
        return new BroadcastResultDto(broadcastId, ids.Count, pushed);
    }

    public async Task<PagedResult<AuditLogDto>> GetAuditLogsAsync(AuditLogQuery query, CancellationToken ct)
    {
        var q = _db.AuditLogs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.EntityType)) q = q.Where(a => a.EntityType == query.EntityType);
        if (query.UserId is { } uid) q = q.Where(a => a.UserId == uid);
        if (!string.IsNullOrWhiteSpace(query.Search)) q = q.Where(a => a.Action.Contains(query.Search) || (a.Details != null && a.Details.Contains(query.Search)));
        return await q.OrderByDescending(a => a.CreatedAt).Select(a => new AuditLogDto(a.Id, a.UserId,
            _db.Users.Where(u => u.Id == a.UserId).Select(u => u.Email).FirstOrDefault(), a.Action, a.EntityType, a.EntityId, a.Details, a.IpAddress, a.CreatedAt))
            .ToPagedAsync(query, ct);
    }

    public async Task<IReadOnlyList<AppSettingDto>> GetSettingsAsync(CancellationToken ct) =>
        await _db.AppSettings.AsNoTracking().OrderBy(s => s.Key).Select(s => new AppSettingDto(s.Key, s.Value, s.Description)).ToListAsync(ct);

    public async Task<AppSettingDto> SaveSettingAsync(AppSettingDto request, CancellationToken ct)
    {
        var setting = await _db.AppSettings.FirstOrDefaultAsync(s => s.Key == request.Key, ct);
        if (setting is null) { setting = new AppSetting { Key = request.Key }; _db.AppSettings.Add(setting); }
        setting.Value = request.Value;
        setting.Description = request.Description;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("SaveSetting", nameof(AppSetting), request.Key, new { request.Value }, ct);
        return request;
    }

    public async Task<ContentReportDto> GetContentReportAsync(CancellationToken ct)
    {
        var tech = await _db.Technologies.AsNoTracking().Where(t => !t.IsDeleted).Select(t => new
        {
            t.Name,
            Questions = _db.Questions.Count(q => q.TechnologyId == t.Id && q.Status == ContentStatus.Published && !q.IsDeleted),
            Attempts = _db.QuestionAttempts.Count(a => a.TechnologyId == t.Id && a.IsCorrect != null),
            Correct = _db.QuestionAttempts.Count(a => a.TechnologyId == t.Id && a.IsCorrect == true)
        }).ToListAsync(ct);
        var hardest = await _db.QuestionAttempts.AsNoTracking().Where(a => a.IsCorrect != null).GroupBy(a => a.QuestionId)
            .Select(g => new { g.Key, Total = g.Count(), Correct = g.Count(a => a.IsCorrect == true) }).Where(g => g.Total >= 5)
            .OrderBy(g => (double)g.Correct / g.Total).Take(10).ToListAsync(ct);
        var titles = await _db.Questions.Where(q => hardest.Select(h => h.Key).Contains(q.Id)).ToDictionaryAsync(q => q.Id, q => q.Title, ct);
        var challenges = await _db.CodingChallenges.AsNoTracking().Where(c => !c.IsDeleted).Select(c => new
        {
            c.Id, c.Title,
            Total = _db.CodeSubmissions.Count(s => s.ChallengeId == c.Id && s.Kind == SubmissionKind.Submit),
            Accepted = _db.CodeSubmissions.Count(s => s.ChallengeId == c.Id && s.Kind == SubmissionKind.Submit && s.Status == SubmissionStatus.Accepted)
        }).ToListAsync(ct);
        static decimal Pct(int a, int b) => b == 0 ? 0 : Math.Round(a * 100m / b, 1);
        return new ContentReportDto(
            tech.Select(t => new TechnologyContentStatDto(t.Name, t.Questions, t.Attempts, Pct(t.Correct, t.Attempts))).OrderByDescending(t => t.Attempts).ToList(),
            hardest.Select(h => new HardestQuestionDto(h.Key, titles.GetValueOrDefault(h.Key, "?"), h.Total, Pct(h.Correct, h.Total))).ToList(),
            challenges.Select(c => new ChallengeStatDto(c.Id, c.Title, c.Total, Pct(c.Accepted, c.Total))).OrderByDescending(c => c.Submissions).ToList());
    }
}

public sealed class GrantSubscriptionRequestValidator : AbstractValidator<GrantSubscriptionRequest>
{
    public GrantSubscriptionRequestValidator()
    {
        RuleFor(x => x.PlanId).NotEmpty();
        RuleFor(x => x.Months).InclusiveBetween(1, 36);
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}

public sealed class SubscriptionPlanUpsertRequestValidator : AbstractValidator<SubscriptionPlanUpsertRequest>
{
    public SubscriptionPlanUpsertRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50).Matches("^[a-z0-9_-]+$");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Currency).NotEmpty().Length(3);
        RuleFor(x => x.PriceMicros).GreaterThanOrEqualTo(0);
        RuleFor(x => x.BillingPeriodMonths).InclusiveBetween(0, 36);
        RuleFor(x => x.Features).NotNull().Must(f => f.Count <= 20);
    }
}

public sealed class AiPromptUpsertRequestValidator : AbstractValidator<AiPromptUpsertRequest>
{
    public AiPromptUpsertRequestValidator()
    {
        RuleFor(x => x.Key).NotEmpty();
        RuleFor(x => x.SystemPrompt).NotEmpty().MinimumLength(50).MaximumLength(20000)
            .Must(p => p.Contains("JSON", StringComparison.OrdinalIgnoreCase)).WithMessage("Prompts must instruct the model to reply with JSON.");
        RuleFor(x => x.Notes).MaximumLength(1000);
    }
}

public sealed class BroadcastNotificationRequestValidator : AbstractValidator<BroadcastNotificationRequest>
{
    public BroadcastNotificationRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Body).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Category).NotEmpty();
        RuleFor(x => x.Audience).NotEmpty();
        RuleFor(x => x.DeepLink).MaximumLength(300).Must(l => l is null || l.StartsWith("devinterview://", StringComparison.Ordinal))
            .WithMessage("Deep links must use the devinterview:// scheme.");
    }
}

public sealed class UpdateUserRolesRequestValidator : AbstractValidator<UpdateUserRolesRequest>
{
    public UpdateUserRolesRequestValidator() => RuleFor(x => x.Roles).NotNull().Must(r => r.Count <= 4);
}
