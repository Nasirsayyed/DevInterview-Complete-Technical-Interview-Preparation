// TypeScript mirrors of DevInterview.Contracts admin DTOs (camelCase JSON, enums as strings).

export const Difficulties = ['Beginner', 'Intermediate', 'Advanced', 'Expert'] as const;
export const ChallengeDifficulties = ['Easy', 'Medium', 'Hard'] as const;
export const QuestionTypes = ['MultipleChoice', 'SingleChoice', 'MultipleSelect', 'TrueFalse', 'WrittenAnswer', 'CodeBased', 'ScenarioBased'] as const;
export const ObjectiveTypes = ['MultipleChoice', 'SingleChoice', 'MultipleSelect', 'TrueFalse'];
export const ContentStatuses = ['Draft', 'Published', 'Archived'] as const;
export const QuizModes = ['Topic', 'Technology', 'Difficulty', 'Random', 'Custom', 'Daily', 'Assessment'] as const;
export const ChallengeCategories = ['Arrays', 'Strings', 'LinkedLists', 'Stacks', 'Queues', 'Trees', 'Graphs', 'Sorting', 'Searching',
  'Recursion', 'DynamicProgramming', 'Hashing', 'Mathematics', 'Sql'] as const;
export const ExecutionLanguages = ['csharp', 'python', 'javascript', 'java', 'kotlin', 'cpp', 'sql'] as const;
export const LessonTypes = ['Reading', 'Exercise', 'Quiz'] as const;
export const ContentSources = ['Verified', 'CommunityContributed', 'Illustrative'] as const;
export const CollectionKinds = ['Technical', 'Coding', 'Hr'] as const;
export const NotificationCategories = ['System', 'DailyPractice', 'DailyChallenge', 'StreakReminder', 'MockInterviewReminder', 'WeeklySummary', 'LearningGoal'] as const;
export const AssignableRoles = ['SuperAdmin', 'Admin', 'ContentManager', 'User'] as const;

export interface TrendPoint { date: string; value: number }
export interface AdminDashboard {
  totalUsers: number; activeUsers7d: number; newUsers7d: number; premiumUsers: number; totalQuestions: number; publishedQuestions: number;
  draftQuestions: number; totalChallenges: number; quizAttempts7d: number; submissions7d: number; mockInterviews7d: number; aiCalls7d: number;
  aiCost7d: number; openReports: number; signupsTrend: TrendPoint[]; activityTrend: TrendPoint[];
}

export interface AdminUser {
  id: string; email: string; fullName: string; roles: string[]; isActive: boolean; emailConfirmed: boolean; createdAt: string;
  lastLoginAt: string | null; tier: string; premiumExpiresAt: string | null;
}

export interface AdminTopic { id: string; categoryId: string; name: string; slug: string; displayOrder: number; isActive: boolean }
export interface AdminTechnology { id: string; name: string; slug: string; description: string | null; group: string; iconKey: string | null; displayOrder: number; isActive: boolean; categoryCount: number; questionCount: number }
export interface AdminCategory { id: string; technologyId: string; technologyName: string; name: string; slug: string; description: string | null; displayOrder: number; isActive: boolean; questionCount: number; topics: AdminTopic[] }

export interface OptionUpsert { text: string; isCorrect: boolean; explanation: string | null }
export interface QuestionUpsert {
  technologyId: string; categoryId: string; topicId: string | null; title: string; body: string; codeSnippet: string | null; codeLanguage: string | null;
  type: string; difficulty: string; estimatedSeconds: number; isPremium: boolean; status: string; tags: string[]; options: OptionUpsert[];
  shortAnswer: string; detailedExplanation: string; referenceAnswer: string | null;
}
export interface AdminQuestion extends QuestionUpsert { id: string; technologyName: string; categoryName: string; createdAt: string; updatedAt: string; publishedAt: string | null }
export interface AdminQuestionListItem { id: string; title: string; technologyName: string; categoryName: string; type: string; difficulty: string; status: string; isPremium: boolean; updatedAt: string }
export interface ImportResult { created: number; updated: number; skipped: number; errors: { row: number; message: string }[]; dryRun: boolean }
export interface QuestionReport { id: string; questionId: string; questionTitle: string; reason: string; details: string | null; status: string; resolutionNote: string | null; reporterEmail: string; createdAt: string }

export interface QuizUpsert {
  title: string; description: string | null; mode: string; timeLimitSeconds: number | null; passingPercentage: number; negativeMarkingFraction: number;
  isPublished: boolean; isPremium: boolean; technologyId: string | null; difficulty: string | null; questionIds: string[];
}
export interface AdminQuiz extends QuizUpsert { id: string; attemptCount: number; updatedAt: string }

export interface TestCase { id: string | null; input: string; expectedOutput: string; isSample: boolean; explanation: string | null }
export interface ChallengeUpsert {
  title: string; slug: string | null; category: string; difficulty: string; problemStatement: string; inputDescription: string; outputDescription: string;
  constraints: string; expectedTimeComplexity: string | null; expectedSpaceComplexity: string | null; hints: string[]; solutionExplanation: string;
  isPremium: boolean; status: string; timeLimitMs: number; memoryLimitMb: number; allowedLanguages: string[]; starterCode: Record<string, string>; testCases: TestCase[];
}
export interface AdminChallenge extends ChallengeUpsert { id: string; slug: string; updatedAt: string }
export interface AdminChallengeListItem { id: string; title: string; category: string; difficulty: string; status: string; isPremium: boolean; testCaseCount: number; submissionCount: number }

export interface LessonUpsert { id: string | null; title: string; content: string; type: string; estimatedMinutes: number; exerciseInstructions: string | null; quizId: string | null; questionIds: string[] }
export interface ModuleUpsert { id: string | null; title: string; description: string | null; lessons: LessonUpsert[] }
export interface LearningPathUpsert {
  title: string; slug: string | null; description: string; level: string; estimatedHours: number; isPremium: boolean; isPublished: boolean;
  displayOrder: number; technologyId: string | null; modules: ModuleUpsert[];
}
export interface AdminLearningPath extends LearningPathUpsert { id: string; enrolledUsers: number }
export interface AdminLearningPathListItem { id: string; title: string; level: string; isPublished: boolean; isPremium: boolean; moduleCount: number; lessonCount: number }

export interface FlashcardUpsert { technologyId: string | null; categoryId: string | null; front: string; back: string; difficulty: string; isPublished: boolean }
export interface AdminFlashcard extends FlashcardUpsert { id: string; technologyName: string | null }

export interface RoundUpsert { name: string; description: string; source: string; sourceReference: string | null }
export interface CompanyTopicUpsert { name: string; description: string | null; technologyId: string | null }
export interface CollectionUpsert { id: string | null; title: string; description: string | null; kind: string; source: string; sourceReference: string | null; questionIds: string[]; challengeIds: string[] }
export interface CompanyUpsert {
  name: string; slug: string | null; overview: string; overviewSource: string; industry: string | null; logoUrl: string | null; websiteUrl: string | null;
  difficultyLevel: string; displayOrder: number; isPremium: boolean; isActive: boolean; rounds: RoundUpsert[]; topics: CompanyTopicUpsert[]; collections: CollectionUpsert[];
}
export interface AdminCompany extends CompanyUpsert { id: string; slug: string }

export interface AiPrompt { id: string; key: string; version: number; systemPrompt: string; notes: string | null; isActive: boolean; updatedAt: string }
export interface AiProviderSetting { id: string; providerName: string; model: string; isEnabled: boolean; priority: number; inputCostPerMillionTokens: number; outputCostPerMillionTokens: number; apiKeyConfigured: boolean }
export interface AiUsageSummary { calls: number; failedCalls: number; promptTokens: number; completionTokens: number; estimatedCostUsd: number; byFeature: { feature: string; calls: number; estimatedCostUsd: number; averageLatencyMs: number }[] }

export interface SubscriptionPlan { id: string; code: string; name: string; description: string; tier: string; priceMicros: number; currency: string; billingPeriodMonths: number; googlePlayProductId: string | null; googlePlayBasePlanId: string | null; features: string[] }
export interface AdminSubscription { id: string; userId: string; userEmail: string; planName: string; status: string; provider: string; startsAt: string; expiresAt: string; autoRenewing: boolean; orderId: string | null }

export interface AuditLog { id: string; userId: string | null; userEmail: string | null; action: string; entityType: string; entityId: string | null; details: string | null; ipAddress: string | null; createdAt: string }
export interface AppSetting { key: string; value: string; description: string | null }
export interface ContentReport {
  technologies: { technology: string; questions: number; attempts: number; accuracy: number }[];
  hardestQuestions: { questionId: string; title: string; attempts: number; accuracy: number }[];
  challenges: { challengeId: string; title: string; submissions: number; acceptanceRate: number }[];
}

export const humanize = (s: string) => s.replace(/([a-z])([A-Z])/g, '$1 $2');
