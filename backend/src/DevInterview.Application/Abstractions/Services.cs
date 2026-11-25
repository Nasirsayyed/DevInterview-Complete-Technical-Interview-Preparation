using DevInterview.Domain.Entities;

namespace DevInterview.Application.Abstractions;

public interface IClock
{
    DateTime UtcNow { get; }
}

public interface ICurrentUser
{
    Guid? UserId { get; }
    bool IsAuthenticated { get; }
    IReadOnlyList<string> Roles { get; }
    string? IpAddress { get; }
    bool IsInRole(string role);
}

public static class CurrentUserExtensions
{
    public static Guid RequireUserId(this ICurrentUser user) =>
        user.UserId ?? throw new Common.UnauthorizedException("Authentication is required.");
}

public sealed record AccessToken(string Token, DateTime ExpiresAt);

public interface ITokenService
{
    AccessToken CreateAccessToken(AppUser user, IReadOnlyList<string> roles);
    /// <summary>Creates a cryptographically random opaque refresh token.</summary>
    string GenerateRefreshToken();
    string HashToken(string token);
    TimeSpan RefreshTokenLifetime { get; }
}

public sealed record GoogleIdentity(string Subject, string Email, bool EmailVerified, string? GivenName, string? FamilyName, string? PictureUrl);

public interface IGoogleTokenValidator
{
    /// <summary>Validates a Google ID token against the configured client ids. Returns null if invalid.</summary>
    Task<GoogleIdentity?> ValidateAsync(string idToken, CancellationToken ct);
    bool IsConfigured { get; }
}

public interface IEmailSender
{
    Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct);
}

public interface IFileStorage
{
    /// <summary>Saves a file and returns its public relative URL.</summary>
    Task<string> SaveAsync(Stream content, string folder, string extension, CancellationToken ct);
    Task DeleteAsync(string url, CancellationToken ct);
}

public sealed record PushMessage(string Title, string Body, IReadOnlyDictionary<string, string>? Data = null);

public interface IPushNotificationSender
{
    bool IsConfigured { get; }
    /// <summary>Sends to device tokens; returns tokens that the provider reports as invalid.</summary>
    Task<IReadOnlyList<string>> SendAsync(IReadOnlyList<string> deviceTokens, PushMessage message, CancellationToken ct);
}

public interface IAuditLogger
{
    Task LogAsync(string action, string entityType, string? entityId, object? details, CancellationToken ct);
}
