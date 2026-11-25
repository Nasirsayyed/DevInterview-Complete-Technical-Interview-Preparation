using DevInterview.Contracts.Common;

namespace DevInterview.Contracts.Auth;

public sealed record RegisterRequest(
    string FirstName, string LastName, string Email, string Password, string ConfirmPassword,
    string ExperienceLevel, string? CurrentJobRole, string? TargetJobRole, IReadOnlyList<Guid>? PreferredTechnologyIds);

public sealed record LoginRequest(string Email, string Password, string? DeviceName);
public sealed record GoogleLoginRequest(string IdToken, string? DeviceName);
public sealed record RefreshTokenRequest(string RefreshToken);
public sealed record LogoutRequest(string RefreshToken, bool AllDevices = false);
public sealed record ForgotPasswordRequest(string Email);
public sealed record ResetPasswordRequest(string Email, string Code, string NewPassword, string ConfirmPassword);
public sealed record VerifyEmailRequest(string Email, string Code);
public sealed record ResendVerificationRequest(string Email);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword, string ConfirmPassword);

public sealed record AuthResponse(
    string AccessToken, DateTime AccessTokenExpiresAt, string RefreshToken, DateTime RefreshTokenExpiresAt, UserSummaryDto User);

public sealed record RegisterResponse(UserSummaryDto User, bool EmailVerificationRequired, AuthResponse? Tokens);

public sealed record UserSummaryDto(
    Guid Id, string Email, string FirstName, string LastName, string? ProfilePhotoUrl,
    bool EmailConfirmed, IReadOnlyList<string> Roles, string Tier);

public sealed record MessageResponse(string Message);

public sealed record UserProfileDto(
    Guid Id, string Email, string FirstName, string LastName, string? ProfilePhotoUrl, bool EmailConfirmed,
    string ExperienceLevel, string? CurrentJobRole, string? TargetJobRole, string? Bio,
    IReadOnlyList<RefDto> PreferredTechnologies, string TimeZoneId, int DailyQuestionGoal, int WeeklyGoalMinutes,
    bool ShowOnLeaderboard, IReadOnlyList<string> Roles, bool HasPassword, bool GoogleLinked, DateTime CreatedAt);

public sealed record UpdateProfileRequest(
    string FirstName, string LastName, string ExperienceLevel, string? CurrentJobRole, string? TargetJobRole,
    string? Bio, IReadOnlyList<Guid>? PreferredTechnologyIds);

public sealed record UpdatePreferencesRequest(string TimeZoneId, int DailyQuestionGoal, int WeeklyGoalMinutes, bool ShowOnLeaderboard);

/// <summary>Confirmation must be the literal "DELETE". Password is required for password-based accounts.</summary>
public sealed record DeleteAccountRequest(string? Password, string Confirmation);

public sealed record RegisterDeviceRequest(string Token, string Platform = "android");
