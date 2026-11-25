namespace DevInterview.Domain.Enums;

public enum ExperienceLevel { Beginner = 0, Intermediate = 1, Advanced = 2, Expert = 3 }

/// <summary>Difficulty of theory questions, quizzes, flashcards and mock interviews.</summary>
public enum Difficulty { Beginner = 0, Intermediate = 1, Advanced = 2, Expert = 3 }

/// <summary>Difficulty of coding challenges.</summary>
public enum ChallengeDifficulty { Easy = 0, Medium = 1, Hard = 2 }

public enum QuestionType
{
    MultipleChoice = 0,
    SingleChoice = 1,
    MultipleSelect = 2,
    TrueFalse = 3,
    WrittenAnswer = 4,
    CodeBased = 5,
    ScenarioBased = 6,
    OutputPrediction = 7,
    Debugging = 8
}

public enum ContentStatus { Draft = 0, Published = 1, Archived = 2 }

public enum QuestionReportStatus { Open = 0, Resolved = 1, Rejected = 2 }

public enum QuizMode { Topic = 0, Technology = 1, Difficulty = 2, Random = 3, Custom = 4, Daily = 5, Assessment = 6 }

public enum QuizAttemptStatus { InProgress = 0, Submitted = 1, Expired = 2 }

public enum ChallengeCategory
{
    Arrays, Strings, LinkedLists, Stacks, Queues, Trees, Graphs, Sorting, Searching,
    Recursion, DynamicProgramming, Hashing, Mathematics, Sql
}

public enum SubmissionStatus
{
    Pending = 0, Accepted = 1, WrongAnswer = 2, RuntimeError = 3, CompilationError = 4,
    TimeLimitExceeded = 5, MemoryLimitExceeded = 6, InternalError = 7
}

public enum SubmissionKind { Run = 0, Submit = 1 }

public enum MockInterviewType
{
    Hr, Technical, Coding, Sql, DotNetDeveloper, ReactDeveloper, FullStackDeveloper, SystemDesign, Managerial
}

public enum MockInterviewStatus { InProgress = 0, Completed = 1, Abandoned = 2 }

public enum LessonType { Reading = 0, Exercise = 1, Quiz = 2 }

public enum ProgressStatus { NotStarted = 0, InProgress = 1, Completed = 2 }

/// <summary>Grade given by a user when reviewing a flashcard.</summary>
public enum FlashcardGrade { Again = 0, Hard = 1, Good = 2, Easy = 3 }

/// <summary>Where a piece of company-specific content comes from. Displayed to users.</summary>
public enum ContentSource { Verified = 0, CommunityContributed = 1, Illustrative = 2 }

public enum CompanyCollectionKind { Technical = 0, Coding = 1, Hr = 2 }

public enum SubscriptionTier { Free = 0, Premium = 1 }

public enum SubscriptionStatus { Pending = 0, Active = 1, GracePeriod = 2, Cancelled = 3, Expired = 4, Revoked = 5 }

public enum PaymentProvider { GooglePlay = 0, AdminGrant = 1 }

public enum ActivityType
{
    QuestionAttempted, QuizCompleted, ChallengeSubmitted, ChallengeSolved, MockInterviewCompleted,
    LessonCompleted, FlashcardReviewed, AnswerEvaluated, CodeReviewed, Login
}

public enum XpSourceType
{
    QuestionCorrect, QuizCompleted, QuizPassed, ChallengeSolved, MockInterviewCompleted,
    LessonCompleted, FlashcardReview, Achievement, DailyStreak, WeeklyGoal
}

public enum NotificationCategory
{
    DailyPractice, DailyChallenge, StreakReminder, MockInterviewReminder, WeeklySummary, LearningGoal, System
}

public enum AiFeature { AnswerEvaluation, CodeReview, CodeExplanation, QuestionGeneration, MockInterview }

public enum UsageFeature
{
    QuestionView, CodeRun, CodeSubmit, AiAnswerEvaluation, AiCodeReview, AiCodeExplanation,
    AiQuestionGeneration, AiMockInterview
}

public enum SavedItemType { Question = 0, CodingChallenge = 1, AnswerEvaluation = 2 }
