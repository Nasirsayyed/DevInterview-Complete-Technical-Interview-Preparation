using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using DevInterview.Application.Abstractions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace DevInterview.IntegrationTests;

/// <summary>Captures outgoing emails so tests can read verification and reset codes.</summary>
public sealed partial class CapturingEmailSender : IEmailSender
{
    public ConcurrentDictionary<string, string> LastCodeByEmail { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct)
    {
        var match = Code().Match(htmlBody);
        if (match.Success) LastCodeByEmail[toEmail] = match.Groups[1].Value;
        return Task.CompletedTask;
    }

    [GeneratedRegex(@">(\d{6})<")]
    private static partial Regex Code();
}

public class ApiFactory : WebApplicationFactory<Program>
{
    public const string AdminEmail = "admin@test.local";
    public const string AdminPassword = "Admin#Test123";
    private readonly SqliteConnection _keepAlive;
    private readonly string _connectionString;
    public CapturingEmailSender Emails { get; } = new();
    protected virtual IDictionary<string, string?> Overrides => new Dictionary<string, string?>();

    public ApiFactory()
    {
        _connectionString = $"DataSource=file:di{Guid.NewGuid():N}?mode=memory&cache=shared";
        _keepAlive = new SqliteConnection(_connectionString);
        _keepAlive.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = _connectionString,
            ["Database:Provider"] = "Sqlite",
            ["Jwt:SigningKey"] = "integration-test-signing-key-0123456789-abcdefghijkl",
            ["App:RequireEmailVerification"] = "true",
            ["BackgroundJobs:Enabled"] = "false",
            ["Swagger:Enabled"] = "true",
            ["Seed:Enabled"] = "true",
            ["Seed:AdminEmail"] = AdminEmail,
            ["Seed:AdminPassword"] = AdminPassword,
            ["CodeExecution:Provider"] = "Disabled",
            ["AI:ProviderOrder:0"] = "Anthropic",
            ["RateLimits:AuthPerMinute"] = "1000",
            ["RateLimits:GlobalPerMinute"] = "10000",
        };
        foreach (var (k, v) in Overrides) settings[k] = v;
        foreach (var (k, v) in settings) builder.UseSetting(k, v);
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IEmailSender>(Emails);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _keepAlive.Dispose();
    }
}

public static class Http
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static async Task<JsonElement> ReadAsync(this HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(Json));

    public static async Task<JsonElement> DataAsync(this HttpResponseMessage response)
    {
        var body = await response.ReadAsync();
        Assert.True(body.GetProperty("success").GetBoolean(), $"Expected success but got {(int)response.StatusCode}: {body}");
        return body.GetProperty("data");
    }

    public static async Task<string> ErrorCodeAsync(this HttpResponseMessage response) =>
        (await response.ReadAsync()).GetProperty("error").GetProperty("code").GetString()!;

    public static void UseToken(this HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    /// <summary>Registers, verifies (with the captured code) and returns an authenticated client and its auth payload.</summary>
    public static async Task<(HttpClient Client, JsonElement Auth)> NewUserAsync(this ApiFactory factory, string? email = null)
    {
        email ??= $"user{Guid.NewGuid():N}@test.local";
        var client = factory.CreateClient();
        var register = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            firstName = "Ada", lastName = "Lovelace", email, password = "Passw0rd!", confirmPassword = "Passw0rd!",
            experienceLevel = "Intermediate", currentJobRole = "Developer", targetJobRole = "Senior Developer", preferredTechnologyIds = Array.Empty<Guid>()
        });
        Assert.Equal(System.Net.HttpStatusCode.Created, register.StatusCode);
        var verify = await client.PostAsJsonAsync("/api/v1/auth/verify-email", new { email, code = factory.Emails.LastCodeByEmail[email] });
        var auth = await verify.DataAsync();
        client.UseToken(auth.GetProperty("accessToken").GetString()!);
        return (client, auth);
    }

    public static async Task<HttpClient> AdminAsync(this ApiFactory factory)
    {
        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = ApiFactory.AdminEmail, password = ApiFactory.AdminPassword });
        client.UseToken((await login.DataAsync()).GetProperty("accessToken").GetString()!);
        return client;
    }
}
