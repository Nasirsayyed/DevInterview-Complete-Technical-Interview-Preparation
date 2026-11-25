using DevInterview.Application.Common;
using DevInterview.Application.Features.Subscriptions;
using DevInterview.Contracts.Engagement;
using DevInterview.Domain.Entities;
using DevInterview.Domain.Enums;
using DevInterview.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DevInterview.UnitTests.Application;

/// <summary>Premium is granted only from a server-verified Google Play purchase that belongs to the caller.</summary>
public sealed class SubscriptionVerificationTests : IDisposable
{
    private readonly TestDb _db = new();

    private sealed class FakeVerifier : IPurchaseVerifier
    {
        public VerifiedPurchase? Result { get; set; }
        public int Acknowledged { get; private set; }
        public bool IsConfigured => true;
        public Task<VerifiedPurchase?> VerifySubscriptionAsync(string productId, string purchaseToken, CancellationToken ct) => Task.FromResult(Result);
        public Task AcknowledgeAsync(string productId, string purchaseToken, CancellationToken ct) { Acknowledged++; return Task.CompletedTask; }
    }

    public void Dispose() => _db.Dispose();

    private async Task SeedPlanAsync()
    {
        await using var db = _db.Create();
        db.SubscriptionPlans.Add(new SubscriptionPlan { Code = "premium-monthly", Name = "Premium Monthly", Tier = SubscriptionTier.Premium, GooglePlayProductId = "devinterview_premium", GooglePlayBasePlanId = "monthly" });
        await db.SaveChangesAsync();
    }

    private SubscriptionService Service(FakeVerifier verifier)
    {
        var ctx = _db.Create();
        var jwt = new JwtTokenService(TestDb.Options(new JwtOptions { SigningKey = "unit-test-signing-key-0123456789abcdef0123" }), _db.Clock);
        return new SubscriptionService(ctx, verifier, new EntitlementService(ctx, _db.Clock, TestDb.Options(Limits.Default)), jwt, _db.Clock, NullLogger<SubscriptionService>.Instance);
    }

    private VerifiedPurchase Purchase(string? owner) => new("devinterview_premium", "monthly", SubscriptionStatus.Active, _db.Clock.UtcNow, _db.Clock.UtcNow.AddDays(30),
        true, "GPA.1", NeedsAcknowledgement: true, ObfuscatedAccountId: owner);

    [Fact]
    public async Task Verified_purchase_for_the_caller_grants_premium_and_is_acknowledged()
    {
        var (user, _, _) = await _db.SeedBasicsAsync(0);
        await SeedPlanAsync();
        var verifier = new FakeVerifier { Result = Purchase(user.ToString()) };

        var status = await Service(verifier).VerifyGooglePlayAsync(user, new VerifyGooglePlayPurchaseRequest("devinterview_premium", "token-1"), default);

        Assert.Equal("Premium", status.Tier);
        Assert.Equal(1, verifier.Acknowledged);
        await using var check = _db.Create();
        var sub = await check.UserSubscriptions.SingleAsync();
        Assert.NotEqual("token-1", sub.PurchaseTokenHash); // only a hash is used for lookups
    }

    [Fact]
    public async Task Purchase_made_by_another_account_is_rejected()
    {
        var (user, _, _) = await _db.SeedBasicsAsync(0);
        await SeedPlanAsync();
        var verifier = new FakeVerifier { Result = Purchase(Guid.NewGuid().ToString()) };

        await Assert.ThrowsAsync<ConflictException>(() =>
            Service(verifier).VerifyGooglePlayAsync(user, new VerifyGooglePlayPurchaseRequest("devinterview_premium", "token-2"), default));
        await using var check = _db.Create();
        Assert.False(await check.UserSubscriptions.AnyAsync());
    }

    [Fact]
    public async Task Token_rejected_by_google_does_not_grant_anything()
    {
        var (user, _, _) = await _db.SeedBasicsAsync(0);
        await SeedPlanAsync();
        await Assert.ThrowsAsync<ValidationAppException>(() =>
            Service(new FakeVerifier { Result = null }).VerifyGooglePlayAsync(user, new VerifyGooglePlayPurchaseRequest("devinterview_premium", "forged"), default));
    }

    [Fact]
    public async Task Same_token_cannot_be_claimed_by_a_second_account()
    {
        var (first, _, _) = await _db.SeedBasicsAsync(0);
        var second = Guid.NewGuid();
        await using (var db = _db.Create())
        {
            db.Users.Add(new AppUser { Id = second, UserName = "second@x.io", Email = "second@x.io", FirstName = "S", LastName = "U", SecurityStamp = "s" });
            await db.SaveChangesAsync();
        }
        await SeedPlanAsync();
        var verifier = new FakeVerifier { Result = Purchase(null) }; // older purchases may lack an account id
        await Service(verifier).VerifyGooglePlayAsync(first, new VerifyGooglePlayPurchaseRequest("devinterview_premium", "shared"), default);

        await Assert.ThrowsAsync<ConflictException>(() =>
            Service(verifier).VerifyGooglePlayAsync(second, new VerifyGooglePlayPurchaseRequest("devinterview_premium", "shared"), default));
    }
}
