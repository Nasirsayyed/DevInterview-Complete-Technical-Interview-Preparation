using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Asp.Versioning;
using DevInterview.API.Infrastructure;
using DevInterview.Application;
using DevInterview.Contracts.Common;
using DevInterview.Domain.Entities;
using DevInterview.Infrastructure;
using DevInterview.Infrastructure.Identity;
using DevInterview.Infrastructure.Jobs;
using DevInterview.Infrastructure.Persistence;
using DevInterview.Infrastructure.Persistence.Seed;
using Hangfire;
using Hangfire.Dashboard;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "DevInterview.API")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}"));

if (builder.Environment.IsProduction() && builder.Configuration["Email:Provider"]?.Equals("Log", StringComparison.OrdinalIgnoreCase) != false
    && builder.Configuration.GetValue("App:RequireEmailVerification", true))
    throw new InvalidOperationException("Configure Email:Provider=Smtp in Production; the Log email provider is for development only.");

if (!builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment("Testing")
    && (builder.Configuration["Jwt:SigningKey"] ?? "").Contains("development-only", StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException("The development JWT signing key must not be used outside Development. Set Jwt__SigningKey.");

builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);

builder.Services.AddControllers(o => o.Filters.Add<ValidationFilter>())
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()))
    .ConfigureApiBehaviorOptions(o =>
    {
        o.InvalidModelStateResponseFactory = context =>
        {
            var details = context.ModelState.Where(kv => kv.Value?.Errors.Count > 0)
                .ToDictionary(kv => kv.Key, kv => kv.Value!.Errors.Select(e => string.IsNullOrEmpty(e.ErrorMessage) ? "Invalid value." : e.ErrorMessage).ToArray());
            return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(
                ApiResponse<object>.Fail(new ApiError(ErrorCodes.Validation, "The request body is invalid.", details), context.HttpContext.TraceIdentifier));
        };
    });

builder.Services.AddApiVersioning(o =>
    {
        o.DefaultApiVersion = new ApiVersion(1, 0);
        o.AssumeDefaultVersionWhenUnspecified = true;
        o.ReportApiVersions = true;
        o.ApiVersionReader = new UrlSegmentApiVersionReader();
    })
    .AddMvc()
    .AddApiExplorer(o =>
    {
        o.GroupNameFormat = "'v'V";
        o.SubstituteApiVersionInUrl = true;
    });

var jwt = builder.Configuration.GetSection(JwtOptions.Section).Get<JwtOptions>() ?? new JwtOptions();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true, ValidIssuer = jwt.Issuer,
            ValidateAudience = true, ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey.PadRight(32))),
            ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(30),
            RoleClaimType = ClaimTypes.Role, NameClaimType = "sub"
        };
        o.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();
                var expired = context.AuthenticateFailure is SecurityTokenExpiredException;
                await ExceptionHandlingMiddleware.WriteAsync(context.HttpContext, 401,
                    new ApiError(expired ? ErrorCodes.InvalidToken : ErrorCodes.Unauthorized, expired ? "The access token has expired." : "Authentication is required."));
            },
            OnForbidden = context => ExceptionHandlingMiddleware.WriteAsync(context.HttpContext, 403,
                new ApiError(ErrorCodes.Forbidden, "You do not have permission to perform this action."))
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Policies.AdminPanel, p => p.RequireRole(RoleNames.AdminPanel))
    .AddPolicy(Policies.AdminOnly, p => p.RequireRole(RoleNames.SuperAdmin, RoleNames.Admin))
    .AddPolicy(Policies.SuperAdmin, p => p.RequireRole(RoleNames.SuperAdmin));

var rateLimits = builder.Configuration.GetSection("RateLimits");
builder.Services.AddRateLimiter(o =>
{
    static string Partition(HttpContext ctx) =>
        ctx.User.FindFirst("sub")?.Value ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "anonymous";

    o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx => RateLimitPartition.GetFixedWindowLimiter(Partition(ctx),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = rateLimits.GetValue("GlobalPerMinute", 300), Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    o.AddPolicy(RateLimitPolicies.Auth, ctx => RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = rateLimits.GetValue("AuthPerMinute", 10), Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    o.AddPolicy(RateLimitPolicies.Ai, ctx => RateLimitPartition.GetSlidingWindowLimiter(Partition(ctx),
        _ => new SlidingWindowRateLimiterOptions { PermitLimit = rateLimits.GetValue("AiPerMinute", 12), Window = TimeSpan.FromMinutes(1), SegmentsPerWindow = 4, QueueLimit = 0 }));
    o.AddPolicy(RateLimitPolicies.Code, ctx => RateLimitPartition.GetSlidingWindowLimiter(Partition(ctx),
        _ => new SlidingWindowRateLimiterOptions { PermitLimit = rateLimits.GetValue("CodePerMinute", 20), Window = TimeSpan.FromMinutes(1), SegmentsPerWindow = 4, QueueLimit = 0 }));
    o.OnRejected = async (context, _) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retry))
            context.HttpContext.Response.Headers.RetryAfter = ((int)retry.TotalSeconds).ToString();
        await ExceptionHandlingMiddleware.WriteAsync(context.HttpContext, 429, new ApiError(ErrorCodes.RateLimited, "Too many requests. Please slow down."));
    };
});

builder.Services.AddCors(o => o.AddPolicy("admin", p => p
    .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>())
    .AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database");
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownNetworks.Clear();
    o.KnownProxies.Clear();
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "DevInterview API", Version = "v1",
        Description = "Backend for the DevInterview technical interview preparation platform. All responses use the ApiResponse envelope."
    });
    o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT", In = ParameterLocation.Header,
        Description = "Paste the access token returned by /api/v1/auth/login."
    });
    o.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }, Array.Empty<string>() }
    });
    var xml = Path.Combine(AppContext.BaseDirectory, "DevInterview.API.xml");
    if (File.Exists(xml)) o.IncludeXmlComments(xml);
    o.CustomSchemaIds(t => t.FullName!.Replace("DevInterview.Contracts.", "").Replace("+", "."));
});

var app = builder.Build();

app.UseForwardedHeaders();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseSerilogRequestLogging(o => o.GetLevel = (ctx, _, ex) =>
    ex is not null || ctx.Response.StatusCode >= 500 ? Serilog.Events.LogEventLevel.Error :
    ctx.Request.Path.StartsWithSegments("/health") ? Serilog.Events.LogEventLevel.Verbose : Serilog.Events.LogEventLevel.Information);

if (app.Configuration.GetValue("Swagger:Enabled", app.Environment.IsDevelopment()))
{
    app.UseSwagger();
    app.UseSwaggerUI(o => o.SwaggerEndpoint("/swagger/v1/swagger.json", "DevInterview API v1"));
}
if (app.Configuration.GetValue("Https:Redirect", !app.Environment.IsDevelopment())) app.UseHsts();

var uploads = Path.Combine(app.Environment.ContentRootPath, "uploads");
Directory.CreateDirectory(uploads);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(uploads), RequestPath = "/uploads",
    OnPrepareResponse = ctx => ctx.Context.Response.Headers.CacheControl = "public,max-age=86400"
});

app.UseCors("admin");
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous().DisableRateLimiting();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    if (app.Configuration.GetValue("Database:ApplyMigrationsOnStartup", true))
    {
        if (db.Database.IsSqlServer()) await db.Database.MigrateAsync();
        else await db.Database.EnsureCreatedAsync();
    }
    if (app.Configuration.GetValue("Seed:Enabled", true))
    {
        try { await scope.ServiceProvider.GetRequiredService<DbSeeder>().SeedAsync(); }
        catch (Exception ex) { logger.LogError(ex, "Database seeding failed"); throw; }
    }
}

if (app.Configuration.GetValue("BackgroundJobs:Enabled", true))
{
    app.MapHangfireDashboard("/jobs", new DashboardOptions { Authorization = new[] { new HangfireAdminFilter() }, IsReadOnlyFunc = _ => true });
    RecurringJobs.Register(app.Services.GetRequiredService<IRecurringJobManager>());
}

app.Run();

/// <summary>Allows the Hangfire dashboard only for authenticated Super Admins (or local requests in development).</summary>
internal sealed class HangfireAdminFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var http = context.GetHttpContext();
        return http.User.IsInRole(RoleNames.SuperAdmin) ||
               (http.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment() && http.Connection.RemoteIpAddress is { } ip && System.Net.IPAddress.IsLoopback(ip));
    }
}

public partial class Program;
