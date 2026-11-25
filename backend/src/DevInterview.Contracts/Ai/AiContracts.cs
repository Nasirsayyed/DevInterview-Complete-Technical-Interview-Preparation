namespace DevInterview.Contracts.Ai;

public sealed record AiStatusDto(bool Available, string? Provider, string? Reason, int RemainingToday, bool IsPremium);

public sealed record EvaluateAnswerRequest(Guid? QuestionId, string? QuestionText, string Answer);

public sealed record AnswerEvaluationDto(
    Guid Id, Guid? QuestionId, string QuestionText, string UserAnswer, int OverallScore, int TechnicalAccuracy,
    int Completeness, int Clarity, int PracticalExamples, IReadOnlyList<string> Strengths,
    IReadOnlyList<string> MissingConcepts, IReadOnlyList<string> IncorrectStatements, IReadOnlyList<string> Suggestions,
    string ImprovedAnswer, string WhyBetter, bool ScoreCappedForLength, DateTime CreatedAt);

public sealed record AnswerEvaluationSummaryDto(Guid Id, string QuestionText, int OverallScore, DateTime CreatedAt);

public sealed record ReviewCodeRequest(string LanguageCode, string Code, string? Context);

public sealed record CodeReviewScoresDto(int Quality, int Performance, int Security, int Readability, int Maintainability, int BestPractices);

public sealed record CodeIssueDto(string Severity, string Category, int? Line, string Title, string Description, string Recommendation);

public sealed record CodeReviewDto(
    Guid Id, string LanguageCode, string OriginalCode, int OverallScore, CodeReviewScoresDto Scores, string TimeComplexity,
    string SpaceComplexity, IReadOnlyList<CodeIssueDto> Issues, IReadOnlyList<string> PotentialBugs,
    string OptimizedCode, string Explanation, string Disclaimer, DateTime CreatedAt);

public sealed record ExplainCodeRequest(string LanguageCode, string Code);

public sealed record CodeExplanationDto(string Summary, IReadOnlyList<string> Steps, string TimeComplexity, string SpaceComplexity, IReadOnlyList<string> KeyConcepts);

public sealed record GenerateQuestionsRequest(Guid TechnologyId, Guid CategoryId, string Difficulty, int Count, string Type);

public sealed record GeneratedQuestionsResponse(IReadOnlyList<Guid> DraftQuestionIds, int Rejected, string Note);

// ---------------- Mock interviews ----------------
public sealed record StartMockInterviewRequest(
    string Type, string Technology, string ExperienceLevel, string Difficulty, int DurationMinutes, int QuestionCount, string TargetRole);

public sealed record InterviewMessageDto(string Role, string Text, Guid? QuestionId, bool IsFollowUp, DateTime Timestamp, int? Score, string? Feedback);

public sealed record InterviewQuestionDto(Guid Id, int Sequence, string Text, bool IsFollowUp, string Difficulty);

public sealed record MockInterviewDto(
    Guid Id, string Type, string Technology, string ExperienceLevel, string CurrentDifficulty, string TargetRole,
    int PlannedQuestionCount, int DurationMinutes, string Status, DateTime StartedAt, DateTime? CompletedAt,
    DateTime EndsAt, IReadOnlyList<InterviewMessageDto> Messages, InterviewQuestionDto? CurrentQuestion,
    int MainQuestionsAsked, bool HasReport);

public sealed record SubmitInterviewAnswerRequest(Guid QuestionId, string Answer, int ResponseSeconds, bool UsedVoiceInput);

public sealed record MockInterviewListItemDto(
    Guid Id, string Type, string Technology, string Status, DateTime StartedAt, DateTime? CompletedAt, int? OverallScore);

public sealed record SuggestedAnswerDto(string Question, string CandidateAnswerSummary, string SuggestedAnswer);

public sealed record QuestionBreakdownDto(int Sequence, string Question, string Answer, int? Score, string? Feedback, bool IsFollowUp);

public sealed record MockInterviewReportDto(
    Guid InterviewId, string Type, string Technology, string TargetRole, int OverallScore, int TechnicalScore,
    int CommunicationScore, int ProblemSolvingScore, int CompletenessScore, int PracticalKnowledgeScore, string Summary,
    IReadOnlyList<string> Strengths, IReadOnlyList<string> Weaknesses, IReadOnlyList<string> MissingConcepts,
    IReadOnlyList<SuggestedAnswerDto> SuggestedAnswers, IReadOnlyList<string> Recommendations,
    IReadOnlyList<string> LearningPath, IReadOnlyList<Common.RefDto> RecommendedPaths,
    IReadOnlyList<string> InteractionSignals, IReadOnlyList<QuestionBreakdownDto> Questions, DateTime CompletedAt);
