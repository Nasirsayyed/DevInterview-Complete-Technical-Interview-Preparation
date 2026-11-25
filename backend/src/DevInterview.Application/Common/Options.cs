namespace DevInterview.Application.Common;

/// <summary>Free-plan limits and premium fair-use quotas. Bound from configuration section "PlanLimits".</summary>
public sealed class PlanLimitsOptions
{
    public const string Section = "PlanLimits";
    public int FreeDailyQuestionViews { get; set; } = 40;
    public int FreeDailyCodeRuns { get; set; } = 30;
    public int FreeDailyCodeSubmissions { get; set; } = 15;
    public int PremiumDailyCodeRuns { get; set; } = 300;
    public int PremiumDailyCodeSubmissions { get; set; } = 150;
    public int PremiumDailyAnswerEvaluations { get; set; } = 40;
    public int PremiumDailyCodeReviews { get; set; } = 30;
    public int PremiumDailyCodeExplanations { get; set; } = 30;
    public int PremiumDailyMockInterviews { get; set; } = 6;
    public int AdminDailyQuestionGenerations { get; set; } = 50;
    /// <summary>Free users may open this many learning paths that are not marked premium (all non-premium paths are free).</summary>
    public int FreeMaxQuizQuestions { get; set; } = 20;
}

/// <summary>AI orchestration settings. Section "AI". API keys come from environment/secrets only.</summary>
public sealed class AiOptions
{
    public const string Section = "AI";
    /// <summary>Ordered provider names to try, e.g. ["Anthropic", "OpenAI"]. Empty = AI disabled.</summary>
    public List<string> ProviderOrder { get; set; } = new();
    public int TimeoutSeconds { get; set; } = 60;
    public int MaxRetries { get; set; } = 2;
    public int MaxInputCharacters { get; set; } = 12000;
    public int MaxTranscriptTurns { get; set; } = 24;
}

public sealed class AppUrlOptions
{
    public const string Section = "App";
    public string PublicBaseUrl { get; set; } = "http://localhost:5080";
    public string DeepLinkScheme { get; set; } = "devinterview";
    /// <summary>When true, newly registered users must verify their email before login.</summary>
    public bool RequireEmailVerification { get; set; } = true;
    public string SupportEmail { get; set; } = "support@devinterview.app";
}
