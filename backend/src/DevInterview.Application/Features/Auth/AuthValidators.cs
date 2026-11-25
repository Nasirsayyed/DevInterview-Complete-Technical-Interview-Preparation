using DevInterview.Contracts.Auth;
using DevInterview.Domain.Enums;
using FluentValidation;

namespace DevInterview.Application.Features.Auth;

internal static class PasswordRules
{
    public static IRuleBuilderOptions<T, string> StrongPassword<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .MaximumLength(128)
            .Matches("[A-Z]").WithMessage("Password must contain an uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must contain a lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain a digit.");
}

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(60);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(60);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Password).StrongPassword();
        RuleFor(x => x.ConfirmPassword).Equal(x => x.Password).WithMessage("Passwords do not match.");
        RuleFor(x => x.ExperienceLevel).NotEmpty()
            .Must(v => Enum.TryParse<ExperienceLevel>(v, true, out _)).WithMessage("Experience level is invalid.");
        RuleFor(x => x.CurrentJobRole).MaximumLength(100);
        RuleFor(x => x.TargetJobRole).MaximumLength(100);
        RuleFor(x => x.PreferredTechnologyIds).Must(l => l is null || l.Count <= 20).WithMessage("Select at most 20 technologies.");
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MaximumLength(128);
        RuleFor(x => x.DeviceName).MaximumLength(100);
    }
}

public sealed class GoogleLoginRequestValidator : AbstractValidator<GoogleLoginRequest>
{
    public GoogleLoginRequestValidator() => RuleFor(x => x.IdToken).NotEmpty().MaximumLength(4096);
}

public sealed class RefreshTokenRequestValidator : AbstractValidator<RefreshTokenRequest>
{
    public RefreshTokenRequestValidator() => RuleFor(x => x.RefreshToken).NotEmpty().MaximumLength(512);
}

public sealed class LogoutRequestValidator : AbstractValidator<LogoutRequest>
{
    public LogoutRequestValidator() => RuleFor(x => x.RefreshToken).NotEmpty().MaximumLength(512);
}

public sealed class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator() => RuleFor(x => x.Email).NotEmpty().EmailAddress();
}

public sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Code).NotEmpty().Length(6).Matches("^[0-9]{6}$").WithMessage("Enter the 6-digit code from the email.");
        RuleFor(x => x.NewPassword).StrongPassword();
        RuleFor(x => x.ConfirmPassword).Equal(x => x.NewPassword).WithMessage("Passwords do not match.");
    }
}

public sealed class VerifyEmailRequestValidator : AbstractValidator<VerifyEmailRequest>
{
    public VerifyEmailRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Code).NotEmpty().Matches("^[0-9]{6}$").WithMessage("Enter the 6-digit code from the email.");
    }
}

public sealed class ResendVerificationRequestValidator : AbstractValidator<ResendVerificationRequest>
{
    public ResendVerificationRequestValidator() => RuleFor(x => x.Email).NotEmpty().EmailAddress();
}

public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword).MaximumLength(128);
        RuleFor(x => x.NewPassword).StrongPassword()
            .NotEqual(x => x.CurrentPassword).WithMessage("New password must differ from the current password.");
        RuleFor(x => x.ConfirmPassword).Equal(x => x.NewPassword).WithMessage("Passwords do not match.");
    }
}

public sealed class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(60);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(60);
        RuleFor(x => x.ExperienceLevel).Must(v => Enum.TryParse<ExperienceLevel>(v, true, out _)).WithMessage("Experience level is invalid.");
        RuleFor(x => x.CurrentJobRole).MaximumLength(100);
        RuleFor(x => x.TargetJobRole).MaximumLength(100);
        RuleFor(x => x.Bio).MaximumLength(1000);
        RuleFor(x => x.PreferredTechnologyIds).Must(l => l is null || l.Count <= 20);
    }
}

public sealed class UpdatePreferencesRequestValidator : AbstractValidator<UpdatePreferencesRequest>
{
    public UpdatePreferencesRequestValidator()
    {
        RuleFor(x => x.TimeZoneId).NotEmpty().Must(Common.TimeZones.IsValid).WithMessage("Unknown time zone.");
        RuleFor(x => x.DailyQuestionGoal).InclusiveBetween(1, 200);
        RuleFor(x => x.WeeklyGoalMinutes).InclusiveBetween(15, 3000);
    }
}

public sealed class DeleteAccountRequestValidator : AbstractValidator<DeleteAccountRequest>
{
    public DeleteAccountRequestValidator() =>
        RuleFor(x => x.Confirmation).Equal("DELETE").WithMessage("Type DELETE to confirm account deletion.");
}

public sealed class RegisterDeviceRequestValidator : AbstractValidator<RegisterDeviceRequest>
{
    public RegisterDeviceRequestValidator()
    {
        RuleFor(x => x.Token).NotEmpty().MaximumLength(4096);
        RuleFor(x => x.Platform).NotEmpty().MaximumLength(20);
    }
}
