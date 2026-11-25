using DevInterview.Domain.Common;
using DevInterview.Domain.Enums;

namespace DevInterview.Domain.Entities;

public class MockInterview : BaseEntity
{
    public Guid UserId { get; set; }
    public MockInterviewType Type { get; set; }
    public string Technology { get; set; } = string.Empty;
    public ExperienceLevel ExperienceLevel { get; set; }
    public Difficulty InitialDifficulty { get; set; }
    public Difficulty CurrentDifficulty { get; set; }
    public string TargetRole { get; set; } = string.Empty;
    public int PlannedQuestionCount { get; set; }
    public int DurationMinutes { get; set; }
    public MockInterviewStatus Status { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string IntroMessage { get; set; } = string.Empty;
    public ICollection<MockInterviewQuestion> Questions { get; set; } = new List<MockInterviewQuestion>();
    public MockInterviewEvaluation? Evaluation { get; set; }

    public int MainQuestionsAsked => Questions.Count(q => !q.IsFollowUp);
}

public class MockInterviewQuestion : BaseEntity
{
    public Guid InterviewId { get; set; }
    public MockInterview Interview { get; set; } = null!;
    public int Sequence { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool IsFollowUp { get; set; }
    public Guid? ParentQuestionId { get; set; }
    public Difficulty Difficulty { get; set; }
    public DateTime AskedAt { get; set; }
    public MockInterviewAnswer? Answer { get; set; }
}

public class MockInterviewAnswer : BaseEntity
{
    public Guid QuestionId { get; set; }
    public MockInterviewQuestion Question { get; set; } = null!;
    public string AnswerText { get; set; } = string.Empty;
    public DateTime AnsweredAt { get; set; }
    /// <summary>Seconds the candidate took to answer (from question shown to answer sent).</summary>
    public int ResponseSeconds { get; set; }
    public bool UsedVoiceInput { get; set; }
    public int? Score { get; set; }
    public string? Feedback { get; set; }
}

public class MockInterviewEvaluation : BaseEntity
{
    public Guid InterviewId { get; set; }
    public MockInterview Interview { get; set; } = null!;
    public int OverallScore { get; set; }
    public int TechnicalScore { get; set; }
    public int CommunicationScore { get; set; }
    public int ProblemSolvingScore { get; set; }
    public int CompletenessScore { get; set; }
    public int PracticalKnowledgeScore { get; set; }
    public string Summary { get; set; } = string.Empty;
    /// <summary>Validated JSON payload with strengths, weaknesses, missing concepts, suggested answers,
    /// recommendations and learning path.</summary>
    public string ReportJson { get; set; } = "{}";
}

/// <summary>A stored AI answer evaluation (Module 7), also savable as an "interview answer".</summary>
public class AnswerEvaluation : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid? QuestionId { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public string UserAnswer { get; set; } = string.Empty;
    public int OverallScore { get; set; }
    public string ResultJson { get; set; } = "{}";
}

public class CodeReview : BaseEntity
{
    public Guid UserId { get; set; }
    public string LanguageCode { get; set; } = string.Empty;
    public string SourceCode { get; set; } = string.Empty;
    public int OverallScore { get; set; }
    public string ResultJson { get; set; } = "{}";
}

public class AiUsageLog : BaseEntity
{
    public Guid? UserId { get; set; }
    public AiFeature Feature { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public decimal EstimatedCostUsd { get; set; }
    public int LatencyMs { get; set; }
    public bool Success { get; set; }
    public string? ErrorCode { get; set; }
}

/// <summary>Versioned prompt templates editable from the admin panel.</summary>
public class AiPromptTemplate : BaseEntity, IActivatable
{
    public string Key { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public string SystemPrompt { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Non-secret provider settings. API keys are never stored in the database.</summary>
public class AiProviderSetting : BaseEntity
{
    public string ProviderName { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public int Priority { get; set; }
    public decimal InputCostPerMillionTokens { get; set; }
    public decimal OutputCostPerMillionTokens { get; set; }
}
