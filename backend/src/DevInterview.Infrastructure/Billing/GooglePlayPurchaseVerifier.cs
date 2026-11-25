using DevInterview.Application.Features.Subscriptions;
using DevInterview.Domain.Enums;
using Google;
using Google.Apis.AndroidPublisher.v3;
using Google.Apis.AndroidPublisher.v3.Data;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DevInterview.Infrastructure.Billing;

public sealed class GooglePlayOptions
{
    public const string Section = "Billing:GooglePlay";
    public string? PackageName { get; set; }
    /// <summary>Path to the Play Console service-account JSON (mounted secret, never committed).</summary>
    public string? ServiceAccountPath { get; set; }
    /// <summary>Shared secret expected in the RTDN push endpoint query string (?token=...).</summary>
    public string? NotificationSecret { get; set; }
}

/// <summary>Verifies subscriptions with the Google Play Developer API (purchases.subscriptionsv2).</summary>
public sealed class GooglePlayPurchaseVerifier : IPurchaseVerifier
{
    private readonly GooglePlayOptions _options;
    private readonly ILogger<GooglePlayPurchaseVerifier> _logger;
    private readonly Lazy<AndroidPublisherService?> _service;

    public GooglePlayPurchaseVerifier(IOptions<GooglePlayOptions> options, ILogger<GooglePlayPurchaseVerifier> logger)
    {
        _options = options.Value;
        _logger = logger;
        _service = new Lazy<AndroidPublisherService?>(() =>
        {
            if (string.IsNullOrEmpty(_options.ServiceAccountPath) || !File.Exists(_options.ServiceAccountPath)) return null;
            var credential = GoogleCredential.FromFile(_options.ServiceAccountPath).CreateScoped(AndroidPublisherService.Scope.Androidpublisher);
            return new AndroidPublisherService(new BaseClientService.Initializer { HttpClientInitializer = credential, ApplicationName = "DevInterview" });
        });
    }

    public bool IsConfigured => !string.IsNullOrEmpty(_options.PackageName) && _service.Value is not null;

    public async Task<VerifiedPurchase?> VerifySubscriptionAsync(string productId, string purchaseToken, CancellationToken ct)
    {
        if (!IsConfigured) return null;
        SubscriptionPurchaseV2 purchase;
        try
        {
            purchase = await _service.Value!.Purchases.Subscriptionsv2.Get(_options.PackageName, purchaseToken).ExecuteAsync(ct);
        }
        catch (GoogleApiException ex) when (ex.HttpStatusCode is System.Net.HttpStatusCode.NotFound or System.Net.HttpStatusCode.BadRequest or System.Net.HttpStatusCode.Gone)
        {
            _logger.LogWarning("Google Play rejected a purchase token: {Status}", ex.HttpStatusCode);
            return null;
        }

        var line = purchase.LineItems?.FirstOrDefault(l => l.ProductId == productId) ?? purchase.LineItems?.FirstOrDefault();
        if (line is null) return null;
        var expires = line.ExpiryTimeDateTimeOffset?.UtcDateTime ?? DateTime.UtcNow;
        var starts = purchase.StartTimeDateTimeOffset?.UtcDateTime ?? DateTime.UtcNow;
        var status = purchase.SubscriptionState switch
        {
            "SUBSCRIPTION_STATE_ACTIVE" => SubscriptionStatus.Active,
            "SUBSCRIPTION_STATE_IN_GRACE_PERIOD" => SubscriptionStatus.GracePeriod,
            "SUBSCRIPTION_STATE_CANCELED" => SubscriptionStatus.Cancelled, // still entitled until expiry
            "SUBSCRIPTION_STATE_PENDING" => SubscriptionStatus.Pending,
            "SUBSCRIPTION_STATE_ON_HOLD" or "SUBSCRIPTION_STATE_PAUSED" or "SUBSCRIPTION_STATE_EXPIRED" => SubscriptionStatus.Expired,
            _ => SubscriptionStatus.Expired
        };
        return new VerifiedPurchase(line.ProductId, line.OfferDetails?.BasePlanId, status, starts, expires,
            line.AutoRenewingPlan?.AutoRenewEnabled ?? false, line.LatestSuccessfulOrderId,
            purchase.AcknowledgementState == "ACKNOWLEDGEMENT_STATE_PENDING");
    }

    public async Task AcknowledgeAsync(string productId, string purchaseToken, CancellationToken ct)
    {
        if (!IsConfigured) return;
        await _service.Value!.Purchases.Subscriptions.Acknowledge(new SubscriptionPurchasesAcknowledgeRequest(), _options.PackageName, productId, purchaseToken)
            .ExecuteAsync(ct);
    }
}
