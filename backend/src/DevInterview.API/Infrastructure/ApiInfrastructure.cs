using System.Diagnostics;
using System.Text.Json;
using DevInterview.Application.Common;
using DevInterview.Contracts.Common;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DevInterview.API.Infrastructure;

/// <summary>Maps exceptions to the standard <see cref="ApiResponse{T}"/> error envelope. Unexpected errors never leak details.</summary>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger, IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Client went away (e.g. Android cancelled the request); nothing to send.
            context.Response.StatusCode = 499;
        }
        catch (AppException ex)
        {
            if (ex.StatusCode >= 500) _logger.LogWarning("Service unavailable: {Code} {Message}", ex.Code, ex.Message);
            await WriteAsync(context, ex.StatusCode, new ApiError(ex.Code, ex.Message, ex.Details));
        }
        catch (ValidationException ex)
        {
            await WriteAsync(context, 400, ValidationFilter.ToError(ex.Errors));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
            var message = _env.IsDevelopment() ? ex.Message : "An unexpected error occurred. Please try again.";
            await WriteAsync(context, 500, new ApiError(ErrorCodes.Internal, message));
        }
    }

    internal static async Task WriteAsync(HttpContext context, int status, ApiError error)
    {
        if (context.Response.HasStarted) return;
        context.Response.Clear();
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";
        var body = ApiResponse<object>.Fail(error, Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier);
        await JsonSerializer.SerializeAsync(context.Response.Body, body, JsonDefaults.Options);
    }
}

public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        return options;
    }
}

/// <summary>Runs registered FluentValidation validators for every action argument before the action executes.</summary>
public sealed class ValidationFilter : IAsyncActionFilter
{
    private readonly IServiceProvider _services;
    public ValidationFilter(IServiceProvider services) => _services = services;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null) continue;
            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (_services.GetService(validatorType) is not IValidator validator) continue;
            var result = await validator.ValidateAsync(new ValidationContext<object>(argument), context.HttpContext.RequestAborted);
            if (!result.IsValid)
            {
                context.Result = new ObjectResult(ApiResponse<object>.Fail(ToError(result.Errors), context.HttpContext.TraceIdentifier)) { StatusCode = 400 };
                return;
            }
        }
        await next();
    }

    internal static ApiError ToError(IEnumerable<FluentValidation.Results.ValidationFailure> failures)
    {
        var list = failures.ToList();
        var details = list.GroupBy(f => JsonNamingPolicy.CamelCase.ConvertName(f.PropertyName.Split('.').Last()))
            .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).Distinct().ToArray());
        return new ApiError(ErrorCodes.Validation, list.FirstOrDefault()?.ErrorMessage ?? "Validation failed.", details);
    }
}

[ApiController]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    protected ActionResult<ApiResponse<T>> Success<T>(T data) => Ok(ApiResponse<T>.Ok(data, HttpContext.TraceIdentifier));

    protected ActionResult<ApiResponse<T>> Created<T>(T data, string? location = null) =>
        StatusCode(201, ApiResponse<T>.Ok(data, HttpContext.TraceIdentifier));

    protected ActionResult<ApiResponse<object>> Done(string message = "OK") =>
        Ok(ApiResponse<object>.Ok(new { message }, HttpContext.TraceIdentifier));
}

public static class Policies
{
    public const string AdminPanel = "AdminPanel";
    public const string AdminOnly = "AdminOnly";
    public const string SuperAdmin = "SuperAdmin";
}

public static class RateLimitPolicies
{
    public const string Auth = "auth";
    public const string Ai = "ai";
    public const string Code = "code";
}

/// <summary>Adds security headers to every response.</summary>
public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        headers["X-Permitted-Cross-Domain-Policies"] = "none";
        return _next(context);
    }
}
