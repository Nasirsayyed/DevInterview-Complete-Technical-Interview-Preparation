using DevInterview.Application.Abstractions;
using DevInterview.Application.Features.Subscriptions;
using DevInterview.Domain.Entities;
using DevInterview.Infrastructure.Ai;
using DevInterview.Infrastructure.Billing;
using DevInterview.Infrastructure.CodeExecution;
using DevInterview.Infrastructure.Identity;
using DevInterview.Infrastructure.Persistence;
using DevInterview.Infrastructure.Persistence.Seed;
using DevInterview.Infrastructure.Services;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DevInterview.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();

        var databaseProvider = configuration["Database:Provider"] ?? "SqlServer";
        var connectionString = configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "ConnectionStrings:Default is not set. Use user-secrets locally (see docs/SetupGuide.md) or the ConnectionStrings__Default environment variable.");
        services.AddDbContext<AppDbContext>(options =>
        {
            if (databaseProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
                options.UseSqlite(connectionString);
            else
                options.UseSqlServer(connectionString, sql =>
                {
                    sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                    sql.UseQuerySplittingBehavior(QuerySplittingBehavior.SingleQuery);
                    sql.CommandTimeout(30);
                });
            if (environment.IsDevelopment()) options.EnableDetailedErrors();
        });
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddIdentityCore<AppUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.AllowedForNewUsers = true;
                // 6-digit codes that users type into the mobile app.
                options.Tokens.EmailConfirmationTokenProvider = TokenOptions.DefaultEmailProvider;
                options.Tokens.PasswordResetTokenProvider = TokenOptions.DefaultEmailProvider;
            })
            .AddRoles<AppRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.Section));
        services.Configure<GoogleAuthOptions>(configuration.GetSection(GoogleAuthOptions.Section));
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.Section));
        services.Configure<FirebaseOptions>(configuration.GetSection(FirebaseOptions.Section));
        services.Configure<GooglePlayOptions>(configuration.GetSection(GooglePlayOptions.Section));
        services.Configure<CodeExecutionOptions>(configuration.GetSection(CodeExecutionOptions.Section));
        services.Configure<SeedOptions>(configuration.GetSection(SeedOptions.Section));

        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton<IGoogleTokenValidator, GoogleIdTokenValidator>();
        services.AddScoped<IAuditLogger, DbAuditLogger>();
        services.AddSingleton<IFileStorage, LocalFileStorage>();

        var emailProvider = configuration["Email:Provider"] ?? "Log";
        if (emailProvider.Equals("Smtp", StringComparison.OrdinalIgnoreCase)) services.AddSingleton<IEmailSender, SmtpEmailSender>();
        else services.AddSingleton<IEmailSender, LoggingEmailSender>();

        if (!string.IsNullOrEmpty(configuration["Firebase:ProjectId"])) services.AddHttpClient<IPushNotificationSender, FcmPushSender>();
        else services.AddSingleton<IPushNotificationSender, NullPushSender>();

        services.AddSingleton<IPurchaseVerifier, GooglePlayPurchaseVerifier>();

        // AI providers: each named options section AI:Providers:{Name}; order and enablement via AI:ProviderOrder.
        foreach (var name in new[] { AnthropicAiProvider.ProviderName, OpenAiCompatibleProvider.ProviderName })
            services.Configure<AiProviderOptions>(name, configuration.GetSection($"AI:Providers:{name}"));
        services.AddSingleton<IAiProvider, AnthropicAiProvider>();
        services.AddHttpClient<OpenAiCompatibleProvider>(c => c.Timeout = TimeSpan.FromSeconds(120));
        services.AddTransient<IAiProvider>(sp => sp.GetRequiredService<OpenAiCompatibleProvider>());

        var executionProvider = configuration["CodeExecution:Provider"] ?? "Disabled";
        if (executionProvider.Equals("Docker", StringComparison.OrdinalIgnoreCase))
            services.AddSingleton<ICodeExecutionProvider, DockerSandboxExecutionProvider>();
        else if (executionProvider.Equals("Judge0", StringComparison.OrdinalIgnoreCase))
            services.AddHttpClient<ICodeExecutionProvider, Judge0ExecutionProvider>(c => c.Timeout = TimeSpan.FromSeconds(30));
        else
            services.AddSingleton<ICodeExecutionProvider, DisabledExecutionProvider>();

        services.AddScoped<DbSeeder>();

        var jobsEnabled = configuration.GetValue("BackgroundJobs:Enabled", true);
        if (jobsEnabled)
        {
            services.AddHangfire(config =>
            {
                config.SetDataCompatibilityLevel(CompatibilityLevel.Version_180).UseSimpleAssemblyNameTypeSerializer().UseRecommendedSerializerSettings();
                if (databaseProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
                    config.UseSqlServerStorage(connectionString, new SqlServerStorageOptions { PrepareSchemaIfNecessary = true, QueuePollInterval = TimeSpan.FromSeconds(15) });
                else
                    config.UseInMemoryStorage();
            });
            services.AddHangfireServer(o => o.WorkerCount = Math.Min(Environment.ProcessorCount * 2, 8));
        }
        return services;
    }
}
