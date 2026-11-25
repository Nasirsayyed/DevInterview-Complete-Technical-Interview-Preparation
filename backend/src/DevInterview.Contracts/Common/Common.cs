namespace DevInterview.Contracts.Common;

/// <summary>Envelope returned by every endpoint.</summary>
public sealed record ApiResponse<T>(bool Success, T? Data, ApiError? Error, string? TraceId = null)
{
    public static ApiResponse<T> Ok(T data, string? traceId = null) => new(true, data, null, traceId);
    public static ApiResponse<T> Fail(ApiError error, string? traceId = null) => new(false, default, error, traceId);
}

public sealed record ApiError(string Code, string Message, IReadOnlyDictionary<string, string[]>? Details = null);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasNext => Page < TotalPages;
}

public class PageQuery
{
    private int _page = 1;
    private int _pageSize = 20;
    public int Page { get => _page; set => _page = Math.Max(1, value); }
    public int PageSize { get => _pageSize; set => _pageSize = Math.Clamp(value, 1, 100); }
    public string? Sort { get; set; }
    public string? Search { get; set; }
}

public sealed record RefDto(Guid Id, string Name, string? Slug = null);

public sealed record IdResponse(Guid Id);

public static class ErrorCodes
{
    public const string Validation = "validation_error";
    public const string NotFound = "not_found";
    public const string Unauthorized = "unauthorized";
    public const string Forbidden = "forbidden";
    public const string Conflict = "conflict";
    public const string PremiumRequired = "premium_required";
    public const string QuotaExceeded = "quota_exceeded";
    public const string RateLimited = "rate_limited";
    public const string AiUnavailable = "ai_unavailable";
    public const string AiInvalidResponse = "ai_invalid_response";
    public const string ExecutionUnavailable = "execution_unavailable";
    public const string BillingUnavailable = "billing_unavailable";
    public const string InvalidCredentials = "invalid_credentials";
    public const string EmailNotVerified = "email_not_verified";
    public const string AccountLocked = "account_locked";
    public const string InvalidToken = "invalid_token";
    public const string QuizExpired = "quiz_expired";
    public const string Internal = "internal_error";
}
