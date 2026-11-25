using DevInterview.Application.Abstractions;
using DevInterview.Application.Common;
using DevInterview.Contracts.Auth;
using DevInterview.Contracts.Common;
using DevInterview.Domain.Entities;
using DevInterview.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DevInterview.Application.Features.Users;

public interface IUserService
{
    Task<UserProfileDto> GetProfileAsync(Guid userId, CancellationToken ct);
    Task<UserProfileDto> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken ct);
    Task<UserProfileDto> UpdatePreferencesAsync(Guid userId, UpdatePreferencesRequest request, CancellationToken ct);
    Task<string> UploadPhotoAsync(Guid userId, Stream content, string contentType, long length, CancellationToken ct);
    Task RemovePhotoAsync(Guid userId, CancellationToken ct);
    Task DeleteAccountAsync(Guid userId, DeleteAccountRequest request, CancellationToken ct);
    Task<object> ExportDataAsync(Guid userId, CancellationToken ct);
    Task RegisterDeviceAsync(Guid userId, RegisterDeviceRequest request, CancellationToken ct);
    Task UnregisterDeviceAsync(Guid userId, string token, CancellationToken ct);
}

public sealed class UserService : IUserService
{
    public const long MaxPhotoBytes = 2 * 1024 * 1024;
    private readonly IAppDbContext _db;
    private readonly UserManager<AppUser> _users;
    private readonly IFileStorage _files;
    private readonly IClock _clock;
    private readonly IAuditLogger _audit;
    private readonly ILogger<UserService> _logger;

    public UserService(IAppDbContext db, UserManager<AppUser> users, IFileStorage files, IClock clock, IAuditLogger audit, ILogger<UserService> logger)
    {
        _db = db;
        _users = users;
        _files = files;
        _clock = clock;
        _audit = audit;
        _logger = logger;
    }

    public async Task<UserProfileDto> GetProfileAsync(Guid userId, CancellationToken ct)
    {
        var user = await _db.Users.AsNoTracking()
            .Include(u => u.Profile!).ThenInclude(p => p.PreferredTechnologies).ThenInclude(pt => pt.Technology)
            .Include(u => u.Preference)
            .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, ct) ?? throw new NotFoundException("User");
        var roles = await _db.UserRoles.Where(r => r.UserId == userId).Select(r => r.Role.Name!).ToListAsync(ct);
        var p = user.Profile;
        var pref = user.Preference;
        return new UserProfileDto(user.Id, user.Email!, user.FirstName, user.LastName, user.ProfilePhotoUrl, user.EmailConfirmed,
            (p?.ExperienceLevel ?? ExperienceLevel.Beginner).ToString(), p?.CurrentJobRole, p?.TargetJobRole, p?.Bio,
            p?.PreferredTechnologies.Select(t => new RefDto(t.TechnologyId, t.Technology.Name, t.Technology.Slug)).ToList() ?? new List<RefDto>(),
            pref?.TimeZoneId ?? "UTC", pref?.DailyQuestionGoal ?? 10, pref?.WeeklyGoalMinutes ?? 180, pref?.ShowOnLeaderboard ?? true,
            roles, user.PasswordHash is not null, user.GoogleSubjectId is not null, user.CreatedAt);
    }

    public async Task<UserProfileDto> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken ct)
    {
        var user = await _db.Users.Include(u => u.Profile!).ThenInclude(p => p.PreferredTechnologies)
            .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, ct) ?? throw new NotFoundException("User");
        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        var profile = user.Profile ?? new UserProfile { UserId = userId };
        if (user.Profile is null) _db.UserProfiles.Add(profile);
        profile.ExperienceLevel = EnumParser.Parse<ExperienceLevel>(request.ExperienceLevel, nameof(request.ExperienceLevel));
        profile.CurrentJobRole = request.CurrentJobRole?.Trim();
        profile.TargetJobRole = request.TargetJobRole?.Trim();
        profile.Bio = request.Bio?.Trim();

        if (request.PreferredTechnologyIds is not null)
        {
            var wanted = await _db.Technologies.Where(t => request.PreferredTechnologyIds.Contains(t.Id) && t.IsActive)
                .Select(t => t.Id).ToListAsync(ct);
            foreach (var existing in profile.PreferredTechnologies.Where(pt => !wanted.Contains(pt.TechnologyId)).ToList())
                profile.PreferredTechnologies.Remove(existing);
            foreach (var id in wanted.Where(id => profile.PreferredTechnologies.All(pt => pt.TechnologyId != id)))
                profile.PreferredTechnologies.Add(new UserPreferredTechnology { TechnologyId = id });
        }
        await _db.SaveChangesAsync(ct);
        return await GetProfileAsync(userId, ct);
    }

    public async Task<UserProfileDto> UpdatePreferencesAsync(Guid userId, UpdatePreferencesRequest request, CancellationToken ct)
    {
        var pref = await _db.UserPreferences.FirstOrDefaultAsync(p => p.UserId == userId, ct);
        if (pref is null)
        {
            pref = new UserPreference { UserId = userId };
            _db.UserPreferences.Add(pref);
        }
        pref.TimeZoneId = request.TimeZoneId;
        pref.DailyQuestionGoal = request.DailyQuestionGoal;
        pref.WeeklyGoalMinutes = request.WeeklyGoalMinutes;
        pref.ShowOnLeaderboard = request.ShowOnLeaderboard;
        await _db.SaveChangesAsync(ct);
        return await GetProfileAsync(userId, ct);
    }

    public async Task<string> UploadPhotoAsync(Guid userId, Stream content, string contentType, long length, CancellationToken ct)
    {
        if (length <= 0 || length > MaxPhotoBytes)
            throw ValidationAppException.For("file", "Profile photos must be between 1 byte and 2 MB.");

        // Validate by magic bytes, not by the client-supplied content type or extension.
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        if (buffer.Length > MaxPhotoBytes) throw ValidationAppException.For("file", "Profile photos must be at most 2 MB.");
        var extension = ImageSignature.Detect(buffer.GetBuffer().AsSpan(0, (int)Math.Min(buffer.Length, 16)))
            ?? throw ValidationAppException.For("file", "Only JPEG, PNG or WebP images are allowed.");
        buffer.Position = 0;

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct) ?? throw new NotFoundException("User");
        var old = user.ProfilePhotoUrl;
        user.ProfilePhotoUrl = await _files.SaveAsync(buffer, "avatars", extension, ct);
        await _db.SaveChangesAsync(ct);
        if (old is not null && old.StartsWith("/uploads/", StringComparison.Ordinal)) await _files.DeleteAsync(old, ct);
        return user.ProfilePhotoUrl;
    }

    public async Task RemovePhotoAsync(Guid userId, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct) ?? throw new NotFoundException("User");
        if (user.ProfilePhotoUrl is { } url && url.StartsWith("/uploads/", StringComparison.Ordinal)) await _files.DeleteAsync(url, ct);
        user.ProfilePhotoUrl = null;
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Deletes an account: personal content (notes, bookmarks, collections, reviews, devices, tokens) is removed,
    /// the identity is anonymised and soft-deleted. Aggregate learning records are kept without PII so that
    /// content statistics remain accurate.
    /// </summary>
    public async Task DeleteAccountAsync(Guid userId, DeleteAccountRequest request, CancellationToken ct)
    {
        var user = await _users.FindByIdAsync(userId.ToString()) ?? throw new NotFoundException("User");
        if (await _users.HasPasswordAsync(user) && !await _users.CheckPasswordAsync(user, request.Password ?? string.Empty))
            throw ValidationAppException.For(nameof(request.Password), "Password is incorrect.");

        await _db.QuestionNotes.Where(x => x.UserId == userId).ExecuteDeleteAsync(ct);
        await _db.QuestionBookmarks.Where(x => x.UserId == userId).ExecuteDeleteAsync(ct);
        await _db.SavedCollectionItems.Where(x => x.Collection.UserId == userId).ExecuteDeleteAsync(ct);
        await _db.SavedCollections.Where(x => x.UserId == userId).ExecuteDeleteAsync(ct);
        await _db.DeviceTokens.Where(x => x.UserId == userId).ExecuteDeleteAsync(ct);
        await _db.RefreshTokens.Where(x => x.UserId == userId).ExecuteDeleteAsync(ct);
        await _db.Notifications.Where(x => x.UserId == userId).ExecuteDeleteAsync(ct);
        await _db.InterviewReminders.Where(x => x.UserId == userId).ExecuteDeleteAsync(ct);
        await _db.AnswerEvaluations.Where(x => x.UserId == userId).ExecuteDeleteAsync(ct);
        await _db.CodeReviews.Where(x => x.UserId == userId).ExecuteDeleteAsync(ct);
        await _db.QuestionReports.Where(x => x.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Details, (string?)null), ct);

        if (user.ProfilePhotoUrl is { } url && url.StartsWith("/uploads/", StringComparison.Ordinal)) await _files.DeleteAsync(url, ct);
        var anonymous = $"deleted-{user.Id:N}@deleted.invalid";
        user.Email = anonymous;
        user.UserName = anonymous;
        user.NormalizedEmail = anonymous.ToUpperInvariant();
        user.NormalizedUserName = anonymous.ToUpperInvariant();
        user.FirstName = "Deleted";
        user.LastName = "User";
        user.ProfilePhotoUrl = null;
        user.PhoneNumber = null;
        user.GoogleSubjectId = null;
        user.PasswordHash = null;
        user.IsActive = false;
        user.IsDeleted = true;
        user.DeletedAt = _clock.UtcNow;
        user.SecurityStamp = Guid.NewGuid().ToString();
        await _users.UpdateAsync(user);

        var profile = await _db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId, ct);
        if (profile is not null)
        {
            profile.Bio = null;
            profile.CurrentJobRole = null;
            profile.TargetJobRole = null;
        }
        var pref = await _db.UserPreferences.FirstOrDefaultAsync(p => p.UserId == userId, ct);
        if (pref is not null) pref.ShowOnLeaderboard = false;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("AccountDeleted", nameof(AppUser), userId.ToString(), null, ct);
        _logger.LogInformation("Account {UserId} deleted and anonymised", userId);
    }

    public async Task<object> ExportDataAsync(Guid userId, CancellationToken ct)
    {
        var profile = await GetProfileAsync(userId, ct);
        var notes = await _db.QuestionNotes.AsNoTracking().Where(n => n.UserId == userId && !n.IsRemoved)
            .Select(n => new { n.QuestionId, Question = n.Question.Title, n.Content, n.UpdatedAt }).ToListAsync(ct);
        var bookmarks = await _db.QuestionBookmarks.AsNoTracking().Where(b => b.UserId == userId && !b.IsRemoved)
            .Select(b => new { b.QuestionId, Question = b.Question.Title, b.CreatedAt }).ToListAsync(ct);
        var quizzes = await _db.QuizAttempts.AsNoTracking().Where(a => a.UserId == userId)
            .Select(a => new { a.QuizId, Quiz = a.Quiz.Title, a.Status, a.Score, a.MaxScore, a.StartedAt, a.SubmittedAt }).ToListAsync(ct);
        var submissions = await _db.CodeSubmissions.AsNoTracking().Where(s => s.UserId == userId)
            .Select(s => new { s.ChallengeId, Challenge = s.Challenge.Title, s.LanguageCode, s.Kind, s.Status, s.CreatedAt }).ToListAsync(ct);
        var interviews = await _db.MockInterviews.AsNoTracking().Where(m => m.UserId == userId)
            .Select(m => new { m.Id, m.Type, m.Technology, m.Status, m.StartedAt, Score = m.Evaluation != null ? (int?)m.Evaluation.OverallScore : null }).ToListAsync(ct);
        var xp = await _db.UserXpTransactions.AsNoTracking().Where(x => x.UserId == userId)
            .Select(x => new { x.Amount, x.SourceType, x.Reason, x.CreatedAt }).ToListAsync(ct);
        var subscriptions = await _db.UserSubscriptions.AsNoTracking().Where(s => s.UserId == userId)
            .Select(s => new { Plan = s.Plan.Name, s.Status, s.Provider, s.StartsAt, s.ExpiresAt }).ToListAsync(ct);
        return new { exportedAt = _clock.UtcNow, profile, notes, bookmarks, quizzes, submissions, interviews, xp, subscriptions };
    }

    public async Task RegisterDeviceAsync(Guid userId, RegisterDeviceRequest request, CancellationToken ct)
    {
        var existing = await _db.DeviceTokens.FirstOrDefaultAsync(d => d.Token == request.Token, ct);
        if (existing is null)
        {
            _db.DeviceTokens.Add(new DeviceToken { UserId = userId, Token = request.Token, Platform = request.Platform, LastSeenAt = _clock.UtcNow });
        }
        else
        {
            existing.UserId = userId; // the device changed hands / account
            existing.Platform = request.Platform;
            existing.LastSeenAt = _clock.UtcNow;
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task UnregisterDeviceAsync(Guid userId, string token, CancellationToken ct) =>
        await _db.DeviceTokens.Where(d => d.UserId == userId && d.Token == token).ExecuteDeleteAsync(ct);
}

public static class ImageSignature
{
    /// <summary>Returns the file extension for JPEG/PNG/WebP magic bytes, or null.</summary>
    public static string? Detect(ReadOnlySpan<byte> header)
    {
        if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF) return ".jpg";
        if (header.Length >= 8 && header[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })) return ".png";
        if (header.Length >= 12 && header[..4].SequenceEqual("RIFF"u8) && header.Slice(8, 4).SequenceEqual("WEBP"u8)) return ".webp";
        return null;
    }
}
