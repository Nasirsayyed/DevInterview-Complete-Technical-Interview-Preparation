namespace DevInterview.Application.Abstractions;

public sealed record ExecutionLimits(int TimeLimitMs, int MemoryLimitMb);

public sealed record ExecutionRequest(string LanguageCode, string SourceCode, IReadOnlyList<string> Inputs, ExecutionLimits Limits);

public enum CaseOutcome { Ok, RuntimeError, TimeLimitExceeded, MemoryLimitExceeded, InternalError }

public sealed record CaseResult(CaseOutcome Outcome, string Stdout, string Stderr, int ExitCode, int RuntimeMs);

/// <summary>Result of compiling once and running the program against every input.</summary>
public sealed record ExecutionResult(bool CompiledOk, string? CompileOutput, IReadOnlyList<CaseResult> Cases, string? InternalError = null)
{
    public static ExecutionResult CompileError(string output) => new(false, output, Array.Empty<CaseResult>());
    public static ExecutionResult Failure(string error) => new(false, null, Array.Empty<CaseResult>(), error);
}

/// <summary>
/// Executes untrusted user code outside the API process. Implementations must isolate execution
/// (container / remote sandbox), enforce time, memory, CPU and process limits, disable networking and
/// clean up all resources.
/// </summary>
public interface ICodeExecutionProvider
{
    string Name { get; }
    bool IsAvailable { get; }
    IReadOnlyCollection<string> SupportedLanguages { get; }
    Task<ExecutionResult> ExecuteAsync(ExecutionRequest request, CancellationToken ct);
}
