using Asp.Versioning;
using DevInterview.API.Infrastructure;
using DevInterview.Application.Abstractions;
using DevInterview.Application.Features.Auth;
using DevInterview.Application.Features.Users;
using DevInterview.Contracts.Auth;
using DevInterview.Contracts.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DevInterview.API.Controllers;

/// <summary>Registration, login, token refresh, email verification and password management.</summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth")]
[EnableRateLimiting(RateLimitPolicies.Auth)]
public sealed class AuthController : ApiControllerBase
{
    private readonly IAuthService _auth;
    private readonly ICurrentUser _user;

    public AuthController(IAuthService auth, ICurrentUser user)
    {
        _auth = auth;
        _user = user;
    }

    /// <summary>Creates an account. When email verification is required, a 6-digit code is emailed and no tokens are returned.</summary>
    [HttpPost("register")]
    public async Task<ActionResult<ApiResponse<RegisterResponse>>> Register(RegisterRequest request, CancellationToken ct) =>
        Created(await _auth.RegisterAsync(request, ct));

    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Login(LoginRequest request, CancellationToken ct) =>
        Success(await _auth.LoginAsync(request, ct));

    /// <summary>Signs in with a Google ID token obtained on the device via Credential Manager.</summary>
    [HttpPost("google")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Google(GoogleLoginRequest request, CancellationToken ct) =>
        Success(await _auth.GoogleLoginAsync(request, ct));

    /// <summary>Exchanges a refresh token for a new access/refresh token pair (rotation).</summary>
    [HttpPost("refresh")]
    [DisableRateLimiting]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Refresh(RefreshTokenRequest request, CancellationToken ct) =>
        Success(await _auth.RefreshAsync(request, ct));

    [HttpPost("logout")]
    [DisableRateLimiting]
    public async Task<ActionResult<ApiResponse<object>>> Logout(LogoutRequest request, CancellationToken ct)
    {
        await _auth.LogoutAsync(request, ct);
        return Done("Signed out.");
    }

    [HttpPost("forgot-password")]
    public async Task<ActionResult<ApiResponse<object>>> ForgotPassword(ForgotPasswordRequest request, CancellationToken ct)
    {
        await _auth.ForgotPasswordAsync(request, ct);
        return Done("If an account exists for this email, a reset code has been sent.");
    }

    [HttpPost("reset-password")]
    public async Task<ActionResult<ApiResponse<object>>> ResetPassword(ResetPasswordRequest request, CancellationToken ct)
    {
        await _auth.ResetPasswordAsync(request, ct);
        return Done("Your password has been reset. Please sign in.");
    }

    [HttpPost("verify-email")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> VerifyEmail(VerifyEmailRequest request, CancellationToken ct) =>
        Success(await _auth.VerifyEmailAsync(request, ct));

    [HttpPost("resend-verification")]
    public async Task<ActionResult<ApiResponse<object>>> ResendVerification(ResendVerificationRequest request, CancellationToken ct)
    {
        await _auth.ResendVerificationAsync(request, ct);
        return Done("If the account needs verification, a new code has been sent.");
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> ChangePassword(ChangePasswordRequest request, CancellationToken ct) =>
        Success(await _auth.ChangePasswordAsync(_user.RequireUserId(), request, ct));
}

/// <summary>The signed-in user's profile, preferences, devices and privacy controls.</summary>
[ApiVersion("1.0")]
[Authorize]
[Route("api/v{version:apiVersion}/users/me")]
public sealed class MeController : ApiControllerBase
{
    private readonly IUserService _users;
    private readonly ICurrentUser _user;

    public MeController(IUserService users, ICurrentUser user)
    {
        _users = users;
        _user = user;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<UserProfileDto>>> Get(CancellationToken ct) => Success(await _users.GetProfileAsync(_user.RequireUserId(), ct));

    [HttpPut]
    public async Task<ActionResult<ApiResponse<UserProfileDto>>> Update(UpdateProfileRequest request, CancellationToken ct) =>
        Success(await _users.UpdateProfileAsync(_user.RequireUserId(), request, ct));

    [HttpPut("preferences")]
    public async Task<ActionResult<ApiResponse<UserProfileDto>>> UpdatePreferences(UpdatePreferencesRequest request, CancellationToken ct) =>
        Success(await _users.UpdatePreferencesAsync(_user.RequireUserId(), request, ct));

    /// <summary>Uploads a JPEG/PNG/WebP profile photo (max 2 MB). File type is verified by content.</summary>
    [HttpPost("photo")]
    [RequestSizeLimit(UserService.MaxPhotoBytes + 64 * 1024)]
    public async Task<ActionResult<ApiResponse<object>>> UploadPhoto(IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        var url = await _users.UploadPhotoAsync(_user.RequireUserId(), stream, file.ContentType, file.Length, ct);
        return Success<object>(new { profilePhotoUrl = url });
    }

    [HttpDelete("photo")]
    public async Task<ActionResult<ApiResponse<object>>> DeletePhoto(CancellationToken ct)
    {
        await _users.RemovePhotoAsync(_user.RequireUserId(), ct);
        return Done("Photo removed.");
    }

    /// <summary>Permanently deletes the account and personal data (requires password for password accounts).</summary>
    [HttpPost("delete")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(DeleteAccountRequest request, CancellationToken ct)
    {
        await _users.DeleteAccountAsync(_user.RequireUserId(), request, ct);
        return Done("Your account has been deleted.");
    }

    /// <summary>Exports the user's personal data as JSON (data portability).</summary>
    [HttpGet("export")]
    public async Task<ActionResult<ApiResponse<object>>> Export(CancellationToken ct) => Success(await _users.ExportDataAsync(_user.RequireUserId(), ct));

    [HttpPost("devices")]
    public async Task<ActionResult<ApiResponse<object>>> RegisterDevice(RegisterDeviceRequest request, CancellationToken ct)
    {
        await _users.RegisterDeviceAsync(_user.RequireUserId(), request, ct);
        return Done("Device registered.");
    }

    [HttpDelete("devices")]
    public async Task<ActionResult<ApiResponse<object>>> UnregisterDevice([FromQuery] string token, CancellationToken ct)
    {
        await _users.UnregisterDeviceAsync(_user.RequireUserId(), token, ct);
        return Done("Device removed.");
    }
}
