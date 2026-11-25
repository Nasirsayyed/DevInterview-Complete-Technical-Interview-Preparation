using System.Net;
using System.Net.Http.Headers;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DevInterview.Application.Abstractions;
using DevInterview.Domain.Entities;
using DevInterview.Infrastructure.Persistence;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DevInterview.Infrastructure.Services;

public sealed class EmailOptions
{
    public const string Section = "Email";
    /// <summary>"Smtp" or "Log". "Log" writes messages to the application log (development only).</summary>
    public string Provider { get; set; } = "Log";
    public string FromAddress { get; set; } = "no-reply@devinterview.app";
    public string FromName { get; set; } = "DevInterview";
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string? Username { get; set; }
    public string? Password { get; set; }
}

public sealed class SmtpEmailSender : IEmailSender
{
    private readonly EmailOptions _options;
    public SmtpEmailSender(IOptions<EmailOptions> options) => _options = options.Value;

    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct)
    {
        using var client = new SmtpClient(_options.Host, _options.Port) { EnableSsl = _options.EnableSsl };
        if (!string.IsNullOrEmpty(_options.Username)) client.Credentials = new NetworkCredential(_options.Username, _options.Password);
        using var message = new MailMessage(new MailAddress(_options.FromAddress, _options.FromName), new MailAddress(toEmail))
        {
            Subject = subject, Body = htmlBody, IsBodyHtml = true
        };
        await client.SendMailAsync(message, ct);
    }
}

/// <summary>
/// Development email sender: writes the message (including one-time codes) to the log so flows can be tested locally.
/// Registered only when Email:Provider = "Log"; startup refuses this in Production.
/// </summary>
public sealed partial class LoggingEmailSender : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> _logger;
    public LoggingEmailSender(ILogger<LoggingEmailSender> logger) => _logger = logger;

    public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct)
    {
        var code = CodeRegex().Match(htmlBody);
        _logger.LogInformation("[DEV EMAIL] To: {To} | Subject: {Subject} | Code: {Code}", toEmail, subject, code.Success ? code.Groups[1].Value : "(none)");
        return Task.CompletedTask;
    }

    [GeneratedRegex(@">(\d{6})<")]
    private static partial Regex CodeRegex();
}

public sealed class LocalFileStorage : IFileStorage
{
    private readonly string _root;

    public LocalFileStorage(IWebHostEnvironment env)
    {
        _root = Path.Combine(env.ContentRootPath, "uploads");
        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveAsync(Stream content, string folder, string extension, CancellationToken ct)
    {
        if (!Regex.IsMatch(folder, "^[a-z0-9-]+$") || !Regex.IsMatch(extension, @"^\.[a-z0-9]{2,5}$"))
            throw new ArgumentException("Invalid storage path.");
        var dir = Path.Combine(_root, folder);
        Directory.CreateDirectory(dir);
        var name = $"{Guid.NewGuid():N}{extension}";
        await using (var file = File.Create(Path.Combine(dir, name)))
            await content.CopyToAsync(file, ct);
        return $"/uploads/{folder}/{name}";
    }

    public Task DeleteAsync(string url, CancellationToken ct)
    {
        var relative = url.Replace("/uploads/", string.Empty, StringComparison.Ordinal);
        var full = Path.GetFullPath(Path.Combine(_root, relative));
        if (full.StartsWith(Path.GetFullPath(_root), StringComparison.Ordinal) && File.Exists(full)) File.Delete(full);
        return Task.CompletedTask;
    }
}

public sealed class FirebaseOptions
{
    public const string Section = "Firebase";
    public string? ProjectId { get; set; }
    /// <summary>Path to a service-account JSON file (mounted secret). Never commit this file.</summary>
    public string? CredentialsPath { get; set; }
}

/// <summary>Firebase Cloud Messaging HTTP v1 sender authenticated with a service account.</summary>
public sealed class FcmPushSender : IPushNotificationSender
{
    private readonly FirebaseOptions _options;
    private readonly HttpClient _http;
    private readonly ILogger<FcmPushSender> _logger;
    private readonly Lazy<GoogleCredential?> _credential;

    public FcmPushSender(IOptions<FirebaseOptions> options, HttpClient http, ILogger<FcmPushSender> logger)
    {
        _options = options.Value;
        _http = http;
        _logger = logger;
        _credential = new Lazy<GoogleCredential?>(() =>
            string.IsNullOrEmpty(_options.CredentialsPath) || !File.Exists(_options.CredentialsPath)
                ? null
                : CredentialFactory.FromFile<ServiceAccountCredential>(_options.CredentialsPath).ToGoogleCredential().CreateScoped("https://www.googleapis.com/auth/firebase.messaging"));
    }

    public bool IsConfigured => !string.IsNullOrEmpty(_options.ProjectId) && _credential.Value is not null;

    public async Task<IReadOnlyList<string>> SendAsync(IReadOnlyList<string> deviceTokens, PushMessage message, CancellationToken ct)
    {
        if (!IsConfigured) return Array.Empty<string>();
        var accessToken = await _credential.Value!.UnderlyingCredential.GetAccessTokenForRequestAsync(cancellationToken: ct);
        var invalid = new List<string>();
        foreach (var token in deviceTokens)
        {
            var payload = new
            {
                message = new
                {
                    token,
                    notification = new { title = message.Title, body = message.Body },
                    data = message.Data ?? new Dictionary<string, string>(),
                    android = new { priority = "normal", notification = new { channel_id = "devinterview_reminders" } }
                }
            };
            using var request = new HttpRequestMessage(HttpMethod.Post, $"https://fcm.googleapis.com/v1/projects/{_options.ProjectId}/messages:send")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var response = await _http.SendAsync(request, ct);
            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest) invalid.Add(token);
            else if (!response.IsSuccessStatusCode) _logger.LogWarning("FCM send failed with {Status}", (int)response.StatusCode);
        }
        return invalid;
    }
}

public sealed class NullPushSender : IPushNotificationSender
{
    public bool IsConfigured => false;
    public Task<IReadOnlyList<string>> SendAsync(IReadOnlyList<string> deviceTokens, PushMessage message, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
}

public sealed class DbAuditLogger : IAuditLogger
{
    private static readonly string[] SensitiveKeys = { "password", "token", "secret", "apikey", "signingkey", "credential" };
    private readonly AppDbContext _db;
    private readonly ICurrentUser _user;
    private readonly ILogger<DbAuditLogger> _logger;

    public DbAuditLogger(AppDbContext db, ICurrentUser user, ILogger<DbAuditLogger> logger)
    {
        _db = db;
        _user = user;
        _logger = logger;
    }

    public async Task LogAsync(string action, string entityType, string? entityId, object? details, CancellationToken ct)
    {
        try
        {
            _db.AuditLogs.Add(new AuditLog
            {
                UserId = _user.UserId, Action = action, EntityType = entityType, EntityId = entityId,
                Details = details is null ? null : Mask(JsonSerializer.Serialize(details)), IpAddress = _user.IpAddress
            });
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to write audit log for {Action} {EntityType}", action, entityType);
        }
    }

    /// <summary>Masks values of JSON properties whose names look sensitive.</summary>
    internal static string Mask(string json)
    {
        foreach (var key in SensitiveKeys)
            json = Regex.Replace(json, $"(\"[A-Za-z]*{key}[A-Za-z]*\"\\s*:\\s*)\"[^\"]*\"", "$1\"***\"", RegexOptions.IgnoreCase);
        return json.Length > 4000 ? json[..4000] : json;
    }
}
