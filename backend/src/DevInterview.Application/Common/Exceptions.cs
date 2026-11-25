using DevInterview.Contracts.Common;

namespace DevInterview.Application.Common;

/// <summary>Base for expected, client-facing errors. Mapped to HTTP responses by the API middleware.</summary>
public abstract class AppException : Exception
{
    protected AppException(string code, string message, int statusCode) : base(message)
    {
        Code = code;
        StatusCode = statusCode;
    }

    public string Code { get; }
    public int StatusCode { get; }
    public IReadOnlyDictionary<string, string[]>? Details { get; init; }
}

public sealed class NotFoundException : AppException
{
    public NotFoundException(string entity, object? key = null)
        : base(ErrorCodes.NotFound, key is null ? $"{entity} was not found." : $"{entity} '{key}' was not found.", 404) { }
}

public sealed class ValidationAppException : AppException
{
    public ValidationAppException(string message, IReadOnlyDictionary<string, string[]>? details = null)
        : base(ErrorCodes.Validation, message, 400) { Details = details; }

    public static ValidationAppException For(string field, string message) =>
        new(message, new Dictionary<string, string[]> { [field] = new[] { message } });
}

public sealed class UnauthorizedException : AppException
{
    public UnauthorizedException(string message, string code = ErrorCodes.Unauthorized) : base(code, message, 401) { }
}

public sealed class ForbiddenException : AppException
{
    public ForbiddenException(string message = "You do not have permission to perform this action.") : base(ErrorCodes.Forbidden, message, 403) { }
}

public sealed class ConflictException : AppException
{
    public ConflictException(string message) : base(ErrorCodes.Conflict, message, 409) { }
}

public sealed class PremiumRequiredException : AppException
{
    public PremiumRequiredException(string feature)
        : base(ErrorCodes.PremiumRequired, $"{feature} is available on the Premium plan.", 402) { }
}

public sealed class QuotaExceededException : AppException
{
    public QuotaExceededException(string message) : base(ErrorCodes.QuotaExceeded, message, 429) { }
}

public sealed class ServiceUnavailableException : AppException
{
    public ServiceUnavailableException(string code, string message) : base(code, message, 503) { }
}

public sealed class BusinessRuleException : AppException
{
    public BusinessRuleException(string code, string message) : base(code, message, 422) { }
}
