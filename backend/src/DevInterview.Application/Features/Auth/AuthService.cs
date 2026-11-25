using System.Net;
using DevInterview.Application.Abstractions;
using DevInterview.Application.Common;
using DevInterview.Application.Features.Subscriptions;
using DevInterview.Contracts.Auth;
using DevInterview.Contracts.Common;
using DevInterview.Domain.Entities;
using DevInterview.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DevInterview.Application.Features.Auth;

public interface IAuthService
{
    Task<RegisterResponse> RegisterAsync(RegisterRequest request, CancellationToken ct);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct);
    Task<AuthResponse> GoogleLoginAsync(GoogleLoginRequest request, CancellationToken ct);
    Task<AuthResponse> RefreshAsync(RefreshTokenRequest request, CancellationToken ct);
    Task LogoutAsync(LogoutRequest request, CancellationToken ct);
    Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct);
    Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct);
    Task<AuthResponse> VerifyEmailAsync(VerifyEmailRequest request, CancellationToken ct);
    Task ResendVerificationAsync(ResendVerificationRequest request, CancellationToken ct);
    Task<AuthResponse> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct);
    Task<UserSummaryDto> GetSummaryAsync(AppUser user, CancellationToken ct);
}

public sealed class AuthService : IAuthService
{
    private readonly UserManager<AppUser> _users;
    private readonly IAppDbContext _db;
    private readonly ITokenService _tokens;
    private readonly IGoogleTokenValidator _google;
    private readonly IEmailSender _email;
    private readonly IEntitlementService _entitlements;
    private readonly IClock _clock;
    private readonly ICurrentUser _current;
    private readonly IAuditLogger _audit;
    private readonly AppUrlOptions _app;
    private readonly ILogger<AuthService> _logger;

    public AuthService(UserManager<AppUser> users, IAppDbContext db, ITokenService tokens, IGoogleTokenValidator google,
        IEmailSender email, IEntitlementService entitlements, IClock clock, ICurrentUser current, IAuditLogger audit,
        IOptions<AppUrlOptions> app, ILogger<AuthService> logger)
    {
        _users = users;
        _db = db;
        _tokens = tokens;
        _google = google;
        _email = email;
        _entitlements = entitlements;
        _clock = clock;
        _current = current;
        _audit = audit;
        _app = app.Value;
        _logger = logger;
    }

    public async Task<RegisterResponse> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim();
        if (await _users.FindByEmailAsync(email) is not null)
            throw new ConflictException("An account with this email already exists.");

        var level = EnumParser.Parse<ExperienceLevel>(request.ExperienceLevel, nameof(request.ExperienceLevel));
        var techIds = (request.PreferredTechnologyIds ?? Array.Empty<Guid>()).Distinct().ToList();
        var validTechIds = await _db.Technologies.Where(t => techIds.Contains(t.Id) && t.IsActive).Select(t => t.Id).ToListAsync(ct);

        var user = new AppUser
        {
            UserName = email,
            Email = email,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            EmailConfirmed = !_app.RequireEmailVerification
        };
        var result = await _users.CreateAsync(user, request.Password);
        ThrowIfFailed(result);
        await _users.AddToRoleAsync(user, RoleNames.User);

        var profile = new UserProfile
        {
            UserId = user.Id, ExperienceLevel = level,
            CurrentJobRole = request.CurrentJobRole?.Trim(), TargetJobRole = request.TargetJobRole?.Trim()
        };
        foreach (var id in validTechIds) profile.PreferredTechnologies.Add(new UserPreferredTechnology { TechnologyId = id });
        _db.UserProfiles.Add(profile);
        _db.UserPreferences.Add(new UserPreference { UserId = user.Id });
        foreach (var category in Enum.GetValues<NotificationCategory>())
            _db.NotificationPreferences.Add(new NotificationPreference { UserId = user.Id, Category = category, Enabled = true });
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("User {UserId} registered", user.Id);

        if (_app.RequireEmailVerification)
        {
            await SendVerificationCodeAsync(user, ct);
            return new RegisterResponse(await GetSummaryAsync(user, ct), true, null);
        }

        var tokens = await IssueTokensAsync(user, null, ct);
        return new RegisterResponse(tokens.User, false, tokens);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var user = await _users.FindByEmailAsync(request.Email.Trim());
        if (user is null || user.IsDeleted || !user.IsActive)
            throw new UnauthorizedException("Invalid email or password.", ErrorCodes.InvalidCredentials);

        if (await _users.IsLockedOutAsync(user))
            throw new UnauthorizedException("Too many failed attempts. Try again in a few minutes.", ErrorCodes.AccountLocked);

        if (!await _users.HasPasswordAsync(user) || !await _users.CheckPasswordAsync(user, request.Password))
        {
            await _users.AccessFailedAsync(user);
            _logger.LogWarning("Failed login for user {UserId}", user.Id);
            throw new UnauthorizedException("Invalid email or password.", ErrorCodes.InvalidCredentials);
        }

        if (!user.EmailConfirmed && _app.RequireEmailVerification)
            throw new ForbiddenEmailException();

        await _users.ResetAccessFailedCountAsync(user);
        return await IssueTokensAsync(user, request.DeviceName, ct);
    }

    public async Task<AuthResponse> GoogleLoginAsync(GoogleLoginRequest request, CancellationToken ct)
    {
        if (!_google.IsConfigured)
            throw new ServiceUnavailableException(ErrorCodes.Internal, "Google Sign-In is not configured on this server.");
        var identity = await _google.ValidateAsync(request.IdToken, ct)
            ?? throw new UnauthorizedException("Google sign-in failed: the token is invalid or expired.", ErrorCodes.InvalidToken);
        if (!identity.EmailVerified)
            throw new UnauthorizedException("Your Google account email is not verified.", ErrorCodes.InvalidToken);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.GoogleSubjectId == identity.Subject, ct)
                   ?? await _users.FindByEmailAsync(identity.Email);
        if (user is not null && (user.IsDeleted || !user.IsActive))
            throw new UnauthorizedException("This account is disabled.", ErrorCodes.InvalidCredentials);

        if (user is null)
        {
            user = new AppUser
            {
                UserName = identity.Email, Email = identity.Email, EmailConfirmed = true,
                FirstName = identity.GivenName ?? identity.Email.Split('@')[0], LastName = identity.FamilyName ?? string.Empty,
                ProfilePhotoUrl = identity.PictureUrl, GoogleSubjectId = identity.Subject
            };
            ThrowIfFailed(await _users.CreateAsync(user));
            await _users.AddToRoleAsync(user, RoleNames.User);
            _db.UserProfiles.Add(new UserProfile { UserId = user.Id, ExperienceLevel = ExperienceLevel.Beginner });
            _db.UserPreferences.Add(new UserPreference { UserId = user.Id });
            foreach (var category in Enum.GetValues<NotificationCategory>())
                _db.NotificationPreferences.Add(new NotificationPreference { UserId = user.Id, Category = category });
            await _db.SaveChangesAsync(ct);
        }
        else if (user.GoogleSubjectId is null)
        {
            user.GoogleSubjectId = identity.Subject;
            user.EmailConfirmed = true;
            await _users.UpdateAsync(user);
        }
        return await IssueTokensAsync(user, request.DeviceName, ct);
    }

    public async Task<AuthResponse> RefreshAsync(RefreshTokenRequest request, CancellationToken ct)
    {
        var hash = _tokens.HashToken(request.RefreshToken);
        var token = await _db.RefreshTokens.Include(t => t.User).FirstOrDefaultAsync(t => t.TokenHash == hash, ct)
                    ?? throw new UnauthorizedException("Refresh token is invalid.", ErrorCodes.InvalidToken);
        var now = _clock.UtcNow;

        if (token.RevokedAt is not null)
        {
            if (token.ReplacedByTokenHash is not null)
            {
                // A rotated token was presented again: treat as theft and revoke the whole family.
                await RevokeFamilyAsync(token.FamilyId, "reuse-detected", ct);
                _logger.LogWarning("Refresh token reuse detected for user {UserId}", token.UserId);
            }
            throw new UnauthorizedException("Refresh token has been revoked.", ErrorCodes.InvalidToken);
        }
        if (token.ExpiresAt <= now || token.User.IsDeleted || !token.User.IsActive)
            throw new UnauthorizedException("Refresh token has expired.", ErrorCodes.InvalidToken);

        var newRaw = _tokens.GenerateRefreshToken();
        var newHash = _tokens.HashToken(newRaw);
        token.RevokedAt = now;
        token.RevokedReason = "rotated";
        token.ReplacedByTokenHash = newHash;
        var replacement = new RefreshToken
        {
            UserId = token.UserId, TokenHash = newHash, FamilyId = token.FamilyId,
            ExpiresAt = now.Add(_tokens.RefreshTokenLifetime), CreatedByIp = _current.IpAddress, DeviceName = token.DeviceName
        };
        _db.RefreshTokens.Add(replacement);
        await _db.SaveChangesAsync(ct);

        var roles = await _users.GetRolesAsync(token.User);
        var access = _tokens.CreateAccessToken(token.User, roles.ToList());
        return new AuthResponse(access.Token, access.ExpiresAt, newRaw, replacement.ExpiresAt, await GetSummaryAsync(token.User, ct));
    }

    public async Task LogoutAsync(LogoutRequest request, CancellationToken ct)
    {
        var hash = _tokens.HashToken(request.RefreshToken);
        var token = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (token is null) return;
        if (_current.UserId is { } uid && uid != token.UserId) return;

        if (request.AllDevices)
        {
            var all = await _db.RefreshTokens.Where(t => t.UserId == token.UserId && t.RevokedAt == null).ToListAsync(ct);
            foreach (var t in all) Revoke(t, "logout-all");
        }
        else if (token.RevokedAt is null)
        {
            Revoke(token, "logout");
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct)
    {
        var user = await _users.FindByEmailAsync(request.Email.Trim());
        if (user is null || user.IsDeleted || !user.IsActive) return; // do not reveal whether the account exists

        var code = await _users.GeneratePasswordResetTokenAsync(user);
        await _email.SendAsync(user.Email!, "Reset your DevInterview password",
            EmailTemplates.Code(user.FirstName, "Use this code to reset your password", code, "If you did not request a reset, you can ignore this email."), ct);
        _logger.LogInformation("Password reset code issued for user {UserId}", user.Id);
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct)
    {
        var user = await _users.FindByEmailAsync(request.Email.Trim());
        if (user is null || user.IsDeleted)
            throw new ValidationAppException("The code is invalid or has expired.");
        if (await _users.IsLockedOutAsync(user))
            throw new UnauthorizedException("Too many failed attempts. Try again later.", ErrorCodes.AccountLocked);

        var result = await _users.ResetPasswordAsync(user, request.Code.Trim(), request.NewPassword);
        if (!result.Succeeded)
        {
            if (result.Errors.Any(e => e.Code == "InvalidToken"))
            {
                await _users.AccessFailedAsync(user);
                throw new ValidationAppException("The code is invalid or has expired.");
            }
            ThrowIfFailed(result);
        }
        await _users.ResetAccessFailedCountAsync(user);
        if (!user.EmailConfirmed)
        {
            user.EmailConfirmed = true; // proving mailbox ownership via the code also verifies the email
            await _users.UpdateAsync(user);
        }
        await RevokeAllAsync(user.Id, "password-reset", ct);
        await _audit.LogAsync("PasswordReset", nameof(AppUser), user.Id.ToString(), null, ct);
    }

    public async Task<AuthResponse> VerifyEmailAsync(VerifyEmailRequest request, CancellationToken ct)
    {
        var user = await _users.FindByEmailAsync(request.Email.Trim())
                   ?? throw new ValidationAppException("The code is invalid or has expired.");
        if (await _users.IsLockedOutAsync(user))
            throw new UnauthorizedException("Too many failed attempts. Try again later.", ErrorCodes.AccountLocked);
        if (!user.EmailConfirmed)
        {
            var result = await _users.ConfirmEmailAsync(user, request.Code.Trim());
            if (!result.Succeeded)
            {
                await _users.AccessFailedAsync(user);
                throw new ValidationAppException("The code is invalid or has expired.");
            }
            await _users.ResetAccessFailedCountAsync(user);
        }
        return await IssueTokensAsync(user, null, ct);
    }

    public async Task ResendVerificationAsync(ResendVerificationRequest request, CancellationToken ct)
    {
        var user = await _users.FindByEmailAsync(request.Email.Trim());
        if (user is null || user.EmailConfirmed || user.IsDeleted) return;
        await SendVerificationCodeAsync(user, ct);
    }

    public async Task<AuthResponse> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct)
    {
        var user = await _users.FindByIdAsync(userId.ToString()) ?? throw new NotFoundException("User");
        var result = await _users.HasPasswordAsync(user)
            ? await _users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword)
            : await _users.AddPasswordAsync(user, request.NewPassword);
        if (!result.Succeeded && result.Errors.Any(e => e.Code == "PasswordMismatch"))
            throw ValidationAppException.For(nameof(request.CurrentPassword), "Current password is incorrect.");
        ThrowIfFailed(result);
        await RevokeAllAsync(user.Id, "password-changed", ct);
        await _audit.LogAsync("PasswordChanged", nameof(AppUser), user.Id.ToString(), null, ct);
        return await IssueTokensAsync(user, null, ct);
    }

    public async Task<UserSummaryDto> GetSummaryAsync(AppUser user, CancellationToken ct)
    {
        var roles = await _users.GetRolesAsync(user);
        var tier = await _entitlements.GetTierAsync(user.Id, ct);
        return new UserSummaryDto(user.Id, user.Email!, user.FirstName, user.LastName, user.ProfilePhotoUrl,
            user.EmailConfirmed, roles.ToList(), tier.ToString());
    }

    private async Task<AuthResponse> IssueTokensAsync(AppUser user, string? deviceName, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var roles = (await _users.GetRolesAsync(user)).ToList();
        var access = _tokens.CreateAccessToken(user, roles);
        var raw = _tokens.GenerateRefreshToken();
        var refresh = new RefreshToken
        {
            UserId = user.Id, TokenHash = _tokens.HashToken(raw), FamilyId = Guid.NewGuid(),
            ExpiresAt = now.Add(_tokens.RefreshTokenLifetime), CreatedByIp = _current.IpAddress,
            DeviceName = Text.Truncate(deviceName, 100)
        };
        _db.RefreshTokens.Add(refresh);
        var tracked = await _db.Users.FirstAsync(u => u.Id == user.Id, ct);
        tracked.LastLoginAt = now;
        await _db.SaveChangesAsync(ct);
        return new AuthResponse(access.Token, access.ExpiresAt, raw, refresh.ExpiresAt, await GetSummaryAsync(user, ct));
    }

    private async Task SendVerificationCodeAsync(AppUser user, CancellationToken ct)
    {
        var code = await _users.GenerateEmailConfirmationTokenAsync(user);
        await _email.SendAsync(user.Email!, "Verify your DevInterview email",
            EmailTemplates.Code(user.FirstName, "Enter this code in the app to verify your email", code, "The code expires in about 10 minutes."), ct);
    }

    private void Revoke(RefreshToken token, string reason)
    {
        token.RevokedAt = _clock.UtcNow;
        token.RevokedReason = reason;
    }

    private async Task RevokeFamilyAsync(Guid familyId, string reason, CancellationToken ct)
    {
        var tokens = await _db.RefreshTokens.Where(t => t.FamilyId == familyId && t.RevokedAt == null).ToListAsync(ct);
        foreach (var t in tokens) Revoke(t, reason);
        await _db.SaveChangesAsync(ct);
    }

    private async Task RevokeAllAsync(Guid userId, string reason, CancellationToken ct)
    {
        var tokens = await _db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAt == null).ToListAsync(ct);
        foreach (var t in tokens) Revoke(t, reason);
        await _db.SaveChangesAsync(ct);
    }

    private static void ThrowIfFailed(IdentityResult result)
    {
        if (result.Succeeded) return;
        var details = result.Errors
            .GroupBy(e => e.Code.StartsWith("Password", StringComparison.Ordinal) ? "password" :
                          e.Code.Contains("Email", StringComparison.Ordinal) || e.Code.Contains("UserName", StringComparison.Ordinal) ? "email" : "general")
            .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());
        throw new ValidationAppException(result.Errors.First().Description, details);
    }
}

public sealed class ForbiddenEmailException : AppException
{
    public ForbiddenEmailException() : base(ErrorCodes.EmailNotVerified, "Please verify your email address before signing in.", 403) { }
}

public static class EmailTemplates
{
    public static string Code(string firstName, string headline, string code, string footer) => $"""
        <div style="font-family:Segoe UI,Roboto,Arial,sans-serif;max-width:480px;margin:auto;padding:24px">
          <h2 style="color:#4F46E5;margin:0 0 16px">DevInterview</h2>
          <p>Hi {WebUtility.HtmlEncode(firstName)},</p>
          <p>{WebUtility.HtmlEncode(headline)}:</p>
          <p style="font-size:32px;letter-spacing:8px;font-weight:700;margin:24px 0">{WebUtility.HtmlEncode(code)}</p>
          <p style="color:#6B7280;font-size:13px">{WebUtility.HtmlEncode(footer)}</p>
        </div>
        """;

    public static string Simple(string title, string body) => $"""
        <div style="font-family:Segoe UI,Roboto,Arial,sans-serif;max-width:480px;margin:auto;padding:24px">
          <h2 style="color:#4F46E5;margin:0 0 16px">{WebUtility.HtmlEncode(title)}</h2>
          <p>{WebUtility.HtmlEncode(body)}</p>
        </div>
        """;
}
