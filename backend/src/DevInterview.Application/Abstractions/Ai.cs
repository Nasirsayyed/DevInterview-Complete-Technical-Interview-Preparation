namespace DevInterview.Application.Abstractions;

public enum AiRole { User, Assistant }

public sealed record AiMessage(AiRole Role, string Content);

public sealed record AiCompletionRequest(
    string SystemPrompt,
    IReadOnlyList<AiMessage> Messages,
    int MaxOutputTokens = 4000,
    /// <summary>When true the provider is asked to return a single JSON object.</summary>
    bool JsonOutput = true);

public sealed record AiCompletionResult(string Content, string Model, int InputTokens, int OutputTokens, string? StopReason);

public enum AiFailureKind { NotConfigured, Timeout, RateLimited, ProviderError, Refused, InvalidRequest }

/// <summary>Thrown by providers for any upstream failure. The orchestrator decides whether to fail over.</summary>
public sealed class AiProviderException : Exception
{
    public AiProviderException(AiFailureKind kind, string message, Exception? inner = null) : base(message, inner) => Kind = kind;
    public AiFailureKind Kind { get; }
    public bool IsTransient => Kind is AiFailureKind.Timeout or AiFailureKind.RateLimited or AiFailureKind.ProviderError;
}

/// <summary>An LLM provider (Anthropic, OpenAI-compatible, …). Implementations never log prompt contents.</summary>
public interface IAiProvider
{
    string Name { get; }
    string Model { get; }
    bool IsConfigured { get; }
    decimal InputCostPerMillionTokens { get; }
    decimal OutputCostPerMillionTokens { get; }
    Task<AiCompletionResult> CompleteAsync(AiCompletionRequest request, CancellationToken ct);
}
