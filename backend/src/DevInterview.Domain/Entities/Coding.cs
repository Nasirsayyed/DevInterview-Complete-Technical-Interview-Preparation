using DevInterview.Domain.Common;
using DevInterview.Domain.Enums;

namespace DevInterview.Domain.Entities;

public class SupportedLanguage : BaseEntity, IActivatable
{
    /// <summary>Stable code, e.g. "python", "csharp", "javascript", "java", "cpp", "kotlin", "sql".</summary>
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string FileExtension { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
}

public class CodingChallenge : ContentEntity
{
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public ChallengeCategory Category { get; set; }
    public ChallengeDifficulty Difficulty { get; set; }
    public string ProblemStatement { get; set; } = string.Empty;
    public string InputDescription { get; set; } = string.Empty;
    public string OutputDescription { get; set; } = string.Empty;
    public string Constraints { get; set; } = string.Empty;
    public string? ExpectedTimeComplexity { get; set; }
    public string? ExpectedSpaceComplexity { get; set; }
    /// <summary>JSON array of hint strings, revealed one at a time.</summary>
    public string HintsJson { get; set; } = "[]";
    public string SolutionExplanation { get; set; } = string.Empty;
    public bool IsPremium { get; set; }
    public ContentStatus Status { get; set; } = ContentStatus.Draft;
    public int TimeLimitMs { get; set; } = 2000;
    public int MemoryLimitMb { get; set; } = 256;
    /// <summary>Comma-separated language codes this challenge accepts (e.g. "sql" for SQL challenges).</summary>
    public string AllowedLanguages { get; set; } = "csharp,javascript,python,java,cpp,kotlin";
    public string? ExternalKey { get; set; }
    public ICollection<ChallengeTestCase> TestCases { get; set; } = new List<ChallengeTestCase>();
    public ICollection<ChallengeStarterCode> StarterCodes { get; set; } = new List<ChallengeStarterCode>();
}

public class ChallengeStarterCode : BaseEntity
{
    public Guid ChallengeId { get; set; }
    public CodingChallenge Challenge { get; set; } = null!;
    public string LanguageCode { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}

public class ChallengeTestCase : BaseEntity
{
    public Guid ChallengeId { get; set; }
    public CodingChallenge Challenge { get; set; } = null!;
    public string Input { get; set; } = string.Empty;
    public string ExpectedOutput { get; set; } = string.Empty;
    /// <summary>Sample cases are shown to the user; hidden cases are only used on submit.</summary>
    public bool IsSample { get; set; }
    public string? Explanation { get; set; }
    public int DisplayOrder { get; set; }
}

public class CodeSubmission : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid ChallengeId { get; set; }
    public CodingChallenge Challenge { get; set; } = null!;
    public string LanguageCode { get; set; } = string.Empty;
    public string SourceCode { get; set; } = string.Empty;
    public SubmissionKind Kind { get; set; }
    public SubmissionStatus Status { get; set; }
    public int PassedCount { get; set; }
    public int TotalCount { get; set; }
    public int? MaxRuntimeMs { get; set; }
    public string? CompileOutput { get; set; }
    /// <summary>Client-provided key; a repeated key returns the original submission instead of re-running.</summary>
    public string? IdempotencyKey { get; set; }
    public ICollection<CodeSubmissionResult> Results { get; set; } = new List<CodeSubmissionResult>();
}

public class CodeSubmissionResult : BaseEntity
{
    public Guid SubmissionId { get; set; }
    public CodeSubmission Submission { get; set; } = null!;
    public Guid? TestCaseId { get; set; }
    public int Ordinal { get; set; }
    public bool IsSample { get; set; }
    public SubmissionStatus Status { get; set; }
    public bool Passed { get; set; }
    /// <summary>Only stored for sample/custom cases so hidden test data is not leaked.</summary>
    public string? ActualOutput { get; set; }
    public string? ErrorOutput { get; set; }
    public int RuntimeMs { get; set; }
}
