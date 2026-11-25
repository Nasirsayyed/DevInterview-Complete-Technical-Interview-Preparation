package com.devinterview.domain.model

import kotlinx.serialization.Serializable

// The shapes below mirror DevInterview.Contracts (camelCase JSON). They are shared by the network layer and the UI
// to avoid three copies of identical data classes; Room entities live separately in core:database.

// ---------------- Auth & profile ----------------
@Serializable
data class UserSummary(
    val id: String, val email: String, val firstName: String, val lastName: String, val profilePhotoUrl: String? = null,
    val emailConfirmed: Boolean = false, val roles: List<String> = emptyList(), val tier: String = "Free",
) {
    val isPremium get() = tier == "Premium"
    val fullName get() = "$firstName $lastName".trim()
}

@Serializable
data class AuthSession(
    val accessToken: String, val accessTokenExpiresAt: String, val refreshToken: String, val refreshTokenExpiresAt: String, val user: UserSummary,
)

@Serializable
data class RegisterResult(val user: UserSummary, val emailVerificationRequired: Boolean, val tokens: AuthSession? = null)

@Serializable
data class UserProfile(
    val id: String, val email: String, val firstName: String, val lastName: String, val profilePhotoUrl: String? = null,
    val emailConfirmed: Boolean = false, val experienceLevel: String = "Beginner", val currentJobRole: String? = null,
    val targetJobRole: String? = null, val bio: String? = null, val preferredTechnologies: List<Ref> = emptyList(),
    val timeZoneId: String = "UTC", val dailyQuestionGoal: Int = 10, val weeklyGoalMinutes: Int = 180, val showOnLeaderboard: Boolean = true,
    val roles: List<String> = emptyList(), val hasPassword: Boolean = true, val googleLinked: Boolean = false, val createdAt: String = "",
)

@Serializable
data class RegistrationForm(
    val firstName: String, val lastName: String, val email: String, val password: String, val confirmPassword: String,
    val experienceLevel: String, val currentJobRole: String?, val targetJobRole: String?, val preferredTechnologyIds: List<String>,
)

// ---------------- Content ----------------
@Serializable
data class Technology(
    val id: String, val name: String, val slug: String, val description: String? = null, val group: String = "General",
    val iconKey: String? = null, val questionCount: Int = 0, val categoryCount: Int = 0,
)

@Serializable data class Topic(val id: String, val name: String, val slug: String)

@Serializable
data class Category(
    val id: String, val technologyId: String, val name: String, val slug: String, val description: String? = null,
    val questionCount: Int = 0, val topics: List<Topic> = emptyList(),
)

@Serializable
data class QuestionSummary(
    val id: String, val title: String, val technology: Ref, val category: Ref, val difficulty: String, val type: String,
    val estimatedSeconds: Int = 60, val tags: List<String> = emptyList(), val isPremium: Boolean = false,
    val isBookmarked: Boolean = false, val lastAttemptCorrect: Boolean? = null,
)

@Serializable data class QuestionOption(val id: String, val text: String, val displayOrder: Int = 0)

@Serializable
data class QuestionDetail(
    val id: String, val title: String, val body: String, val codeSnippet: String? = null, val codeLanguage: String? = null,
    val technology: Ref, val category: Ref, val topic: Ref? = null, val difficulty: String, val type: String, val estimatedSeconds: Int = 60,
    val tags: List<String> = emptyList(), val options: List<QuestionOption> = emptyList(), val isObjective: Boolean = true,
    val allowsMultipleAnswers: Boolean = false, val isBookmarked: Boolean = false, val note: String? = null, val isPremium: Boolean = false,
    val previousId: String? = null, val nextId: String? = null, val shareText: String = "",
)

@Serializable data class OptionExplanation(val optionId: String, val isCorrect: Boolean, val explanation: String? = null)

@Serializable
data class QuestionAnswer(
    val questionId: String, val shortAnswer: String, val detailedExplanation: String, val correctOptionIds: List<String> = emptyList(),
    val options: List<OptionExplanation> = emptyList(),
)

@Serializable
data class AttemptResult(val isCorrect: Boolean? = null, val correctOptionIds: List<String> = emptyList(), val xpAwarded: Int = 0, val answer: QuestionAnswer)

@Serializable data class Note(val questionId: String, val questionTitle: String, val technology: String, val content: String, val updatedAt: String)

data class QuestionFilter(
    val technologyId: String? = null, val categoryId: String? = null, val difficulty: String? = null, val type: String? = null,
    val search: String? = null, val bookmarkedOnly: Boolean = false, val attemptFilter: String? = null, val sort: String? = null,
)

// ---------------- Quizzes ----------------
@Serializable
data class QuizSummary(
    val id: String, val title: String, val description: String? = null, val mode: String, val questionCount: Int,
    val timeLimitSeconds: Int? = null, val passingPercentage: Int = 60, val negativeMarkingFraction: Double = 0.0, val difficulty: String? = null,
    val technology: Ref? = null, val isPremium: Boolean = false, val attemptCount: Int = 0, val bestPercentage: Double? = null,
)

@Serializable
data class CreateQuiz(
    val title: String? = null, val mode: String, val technologyIds: List<String> = emptyList(), val categoryIds: List<String> = emptyList(),
    val difficulty: String? = null, val questionCount: Int, val timeLimitSeconds: Int? = null, val negativeMarkingFraction: Double = 0.0,
    val passingPercentage: Int = 60,
)

@Serializable
data class QuizQuestion(
    val questionId: String, val order: Int, val title: String, val body: String, val codeSnippet: String? = null, val codeLanguage: String? = null,
    val type: String, val allowsMultipleAnswers: Boolean = false, val options: List<QuestionOption>, val points: Double = 1.0,
    val selectedOptionIds: List<String> = emptyList(), val isMarkedForReview: Boolean = false,
)

@Serializable
data class QuizAttempt(
    val attemptId: String, val quizId: String, val title: String, val status: String, val startedAt: String, val expiresAt: String? = null,
    val serverNow: String, val timeLimitSeconds: Int? = null, val negativeMarkingFraction: Double = 0.0, val passingPercentage: Int = 60,
    val questions: List<QuizQuestion>,
)

@Serializable
data class QuizAnswer(val questionId: String, val selectedOptionIds: List<String>, val isMarkedForReview: Boolean, val timeSpentSeconds: Int)

@Serializable data class ReviewOption(val id: String, val text: String, val isCorrect: Boolean, val selected: Boolean, val explanation: String? = null)

@Serializable
data class QuizReviewItem(
    val questionId: String, val order: Int, val title: String, val body: String, val codeSnippet: String? = null, val options: List<ReviewOption>,
    val isCorrect: Boolean? = null, val skipped: Boolean = false, val pointsAwarded: Double = 0.0, val explanation: String = "", val category: String = "",
)

@Serializable data class TopicPerformance(val name: String, val id: String? = null, val correct: Int, val total: Int, val accuracy: Double)

@Serializable
data class QuizResult(
    val attemptId: String, val quizId: String, val quizTitle: String, val status: String, val totalQuestions: Int, val attempted: Int,
    val correct: Int, val incorrect: Int, val skipped: Int, val accuracy: Double, val score: Double, val maxScore: Double,
    val percentage: Double, val passed: Boolean, val passingPercentage: Int, val totalTimeSeconds: Int, val startedAt: String,
    val submittedAt: String? = null, val review: List<QuizReviewItem> = emptyList(), val topicBreakdown: List<TopicPerformance> = emptyList(),
    val recommendedTopics: List<String> = emptyList(), val xpAwarded: Int = 0,
)

@Serializable
data class QuizAttemptSummary(
    val attemptId: String, val quizId: String, val quizTitle: String, val status: String, val percentage: Double, val passed: Boolean,
    val correct: Int, val totalQuestions: Int, val startedAt: String, val submittedAt: String? = null,
)

// ---------------- Coding ----------------
@Serializable data class Language(val code: String, val displayName: String, val version: String)

@Serializable
data class ChallengeSummary(
    val id: String, val title: String, val slug: String, val category: String, val difficulty: String, val isPremium: Boolean = false,
    val isSolved: Boolean = false, val isAttempted: Boolean = false, val acceptanceRate: Double = 0.0,
)

@Serializable data class SampleTestCase(val id: String, val input: String, val expectedOutput: String, val explanation: String? = null)

@Serializable
data class ChallengeDetail(
    val id: String, val title: String, val slug: String, val category: String, val difficulty: String, val problemStatement: String,
    val inputDescription: String, val outputDescription: String, val constraints: String, val expectedTimeComplexity: String? = null,
    val expectedSpaceComplexity: String? = null, val hintCount: Int = 0, val sampleTestCases: List<SampleTestCase> = emptyList(),
    val hiddenTestCaseCount: Int = 0, val starterCode: Map<String, String> = emptyMap(), val allowedLanguages: List<String> = emptyList(),
    val timeLimitMs: Int = 2000, val memoryLimitMb: Int = 256, val isSolved: Boolean = false, val isPremium: Boolean = false,
    val solutionAvailable: Boolean = false,
)

@Serializable data class Hint(val index: Int, val total: Int, val text: String)
@Serializable data class Solution(val challengeId: String, val explanation: String, val expectedTimeComplexity: String? = null, val expectedSpaceComplexity: String? = null)

@Serializable
data class TestResult(
    val ordinal: Int, val isSample: Boolean, val isCustom: Boolean = false, val input: String? = null, val expectedOutput: String? = null,
    val actualOutput: String? = null, val errorOutput: String? = null, val passed: Boolean, val status: String, val runtimeMs: Int = 0,
)

@Serializable
data class RunResult(val status: String, val compileOutput: String? = null, val results: List<TestResult> = emptyList(), val passedCount: Int = 0, val totalCount: Int = 0)

@Serializable
data class Submission(
    val id: String, val challengeId: String, val challengeTitle: String, val languageCode: String, val kind: String, val status: String,
    val passedCount: Int, val totalCount: Int, val maxRuntimeMs: Int? = null, val createdAt: String, val compileOutput: String? = null,
    val results: List<TestResult> = emptyList(), val xpAwarded: Int = 0, val sourceCode: String? = null,
)

// ---------------- AI ----------------
@Serializable data class AiStatus(val available: Boolean, val provider: String? = null, val reason: String? = null, val remainingToday: Int = 0, val isPremium: Boolean = false)

@Serializable
data class AnswerEvaluation(
    val id: String, val questionId: String? = null, val questionText: String, val userAnswer: String, val overallScore: Int,
    val technicalAccuracy: Int, val completeness: Int, val clarity: Int, val practicalExamples: Int, val strengths: List<String> = emptyList(),
    val missingConcepts: List<String> = emptyList(), val incorrectStatements: List<String> = emptyList(), val suggestions: List<String> = emptyList(),
    val improvedAnswer: String = "", val whyBetter: String = "", val scoreCappedForLength: Boolean = false, val createdAt: String = "",
)

@Serializable data class EvaluationSummary(val id: String, val questionText: String, val overallScore: Int, val createdAt: String)

@Serializable data class CodeReviewScores(val quality: Int, val performance: Int, val security: Int, val readability: Int, val maintainability: Int, val bestPractices: Int)
@Serializable data class CodeIssue(val severity: String, val category: String, val line: Int? = null, val title: String, val description: String, val recommendation: String)

@Serializable
data class CodeReview(
    val id: String, val languageCode: String, val originalCode: String, val overallScore: Int, val scores: CodeReviewScores,
    val timeComplexity: String = "", val spaceComplexity: String = "", val issues: List<CodeIssue> = emptyList(),
    val potentialBugs: List<String> = emptyList(), val optimizedCode: String = "", val explanation: String = "", val disclaimer: String = "",
    val createdAt: String = "",
)

@Serializable
data class StartInterview(
    val type: String, val technology: String, val experienceLevel: String, val difficulty: String, val durationMinutes: Int,
    val questionCount: Int, val targetRole: String,
)

@Serializable
data class InterviewMessage(
    val role: String, val text: String, val questionId: String? = null, val isFollowUp: Boolean = false, val timestamp: String = "",
    val score: Int? = null, val feedback: String? = null,
)

@Serializable data class InterviewQuestion(val id: String, val sequence: Int, val text: String, val isFollowUp: Boolean, val difficulty: String)

@Serializable
data class MockInterview(
    val id: String, val type: String, val technology: String, val experienceLevel: String, val currentDifficulty: String, val targetRole: String,
    val plannedQuestionCount: Int, val durationMinutes: Int, val status: String, val startedAt: String, val completedAt: String? = null,
    val endsAt: String, val messages: List<InterviewMessage> = emptyList(), val currentQuestion: InterviewQuestion? = null,
    val mainQuestionsAsked: Int = 0, val hasReport: Boolean = false,
)

@Serializable
data class InterviewSummary(val id: String, val type: String, val technology: String, val status: String, val startedAt: String, val completedAt: String? = null, val overallScore: Int? = null)

@Serializable data class SuggestedAnswer(val question: String, val candidateAnswerSummary: String, val suggestedAnswer: String)
@Serializable data class QuestionBreakdown(val sequence: Int, val question: String, val answer: String, val score: Int? = null, val feedback: String? = null, val isFollowUp: Boolean = false)

@Serializable
data class InterviewReport(
    val interviewId: String, val type: String, val technology: String, val targetRole: String, val overallScore: Int, val technicalScore: Int,
    val communicationScore: Int, val problemSolvingScore: Int, val completenessScore: Int, val practicalKnowledgeScore: Int, val summary: String,
    val strengths: List<String> = emptyList(), val weaknesses: List<String> = emptyList(), val missingConcepts: List<String> = emptyList(),
    val suggestedAnswers: List<SuggestedAnswer> = emptyList(), val recommendations: List<String> = emptyList(), val learningPath: List<String> = emptyList(),
    val recommendedPaths: List<Ref> = emptyList(), val interactionSignals: List<String> = emptyList(), val questions: List<QuestionBreakdown> = emptyList(),
    val completedAt: String = "",
)

// ---------------- Learning ----------------
@Serializable
data class LearningPathSummary(
    val id: String, val title: String, val slug: String, val description: String, val level: String, val estimatedHours: Int,
    val moduleCount: Int, val lessonCount: Int, val completedLessons: Int, val progressPercent: Int, val isPremium: Boolean = false,
    val technology: Ref? = null,
)

@Serializable data class LessonSummary(val id: String, val title: String, val type: String, val estimatedMinutes: Int, val status: String)
@Serializable data class LearningModule(val id: String, val title: String, val description: String? = null, val order: Int, val lessons: List<LessonSummary>, val completedLessons: Int)

@Serializable
data class LearningPathDetail(
    val id: String, val title: String, val slug: String, val description: String, val level: String, val estimatedHours: Int,
    val isPremium: Boolean = false, val technology: Ref? = null, val lessonCount: Int, val completedLessons: Int, val progressPercent: Int,
    val nextLessonId: String? = null, val modules: List<LearningModule>,
)

@Serializable
data class Lesson(
    val id: String, val pathId: String, val pathTitle: String, val moduleId: String, val moduleTitle: String, val title: String, val content: String,
    val type: String, val estimatedMinutes: Int, val exerciseInstructions: String? = null, val quizId: String? = null,
    val questions: List<QuestionSummary> = emptyList(), val status: String, val previousLessonId: String? = null, val nextLessonId: String? = null,
)

@Serializable data class LessonProgress(val lessonId: String, val status: String, val pathProgressPercent: Int, val xpAwarded: Int)

// ---------------- Flashcards ----------------
@Serializable
data class Flashcard(
    val id: String, val front: String, val back: String, val technology: Ref? = null, val category: Ref? = null, val difficulty: String,
    val isBookmarked: Boolean = false, val dueAt: String? = null, val repetitions: Int = 0, val intervalDays: Int = 0, val isNew: Boolean = true,
    val lastGrade: String? = null,
)

@Serializable data class FlashcardDeck(val technologyId: String? = null, val name: String, val totalCards: Int, val dueCount: Int, val newCount: Int, val masteredCount: Int)
@Serializable data class FlashcardReviewResult(val flashcardId: String, val nextDueAt: String, val intervalDays: Int, val easeFactor: Double, val xpAwarded: Int, val duplicate: Boolean)

enum class FlashcardGrade { Again, Hard, Good, Easy }

// ---------------- Companies, library, search ----------------
@Serializable
data class CompanySummary(val id: String, val name: String, val slug: String, val industry: String? = null, val logoUrl: String? = null, val difficultyLevel: String, val isPremium: Boolean = true, val topicCount: Int = 0, val collectionCount: Int = 0)
@Serializable data class InterviewRound(val order: Int, val name: String, val description: String, val source: String, val sourceReference: String? = null)
@Serializable data class CompanyTopic(val id: String, val name: String, val description: String? = null, val technology: Ref? = null)
@Serializable data class CompanyCollection(val id: String, val title: String, val description: String? = null, val kind: String, val source: String, val sourceReference: String? = null, val questionCount: Int = 0, val challengeCount: Int = 0)

@Serializable
data class CompanyDetail(
    val id: String, val name: String, val slug: String, val overview: String, val overviewSource: String, val industry: String? = null,
    val websiteUrl: String? = null, val difficultyLevel: String, val isPremium: Boolean = true, val rounds: List<InterviewRound> = emptyList(),
    val topics: List<CompanyTopic> = emptyList(), val collections: List<CompanyCollection> = emptyList(), val disclaimer: String = "",
)

@Serializable
data class CompanyCollectionDetail(val collection: CompanyCollection, val companyName: String, val questions: List<QuestionSummary> = emptyList(), val challenges: List<ChallengeSummary> = emptyList(), val disclaimer: String = "")

@Serializable data class SavedCollection(val id: String, val name: String, val description: String? = null, val itemCount: Int = 0, val createdAt: String = "")
@Serializable data class SavedCollectionItem(val id: String, val itemType: String, val itemId: String, val title: String, val subtitle: String? = null, val addedAt: String = "")
@Serializable data class SavedCollectionDetail(val id: String, val name: String, val description: String? = null, val items: List<SavedCollectionItem> = emptyList())

@Serializable data class SearchHit(val type: String, val id: String, val title: String, val subtitle: String? = null, val technology: String? = null, val difficulty: String? = null, val parentId: String? = null)
@Serializable data class SearchResults(val query: String, val hits: Page<SearchHit>, val countsByType: Map<String, Int> = emptyMap())

// ---------------- Dashboard, analytics, gamification ----------------
@Serializable data class Level(val level: Int, val title: String, val totalXp: Int, val currentLevelXp: Int, val nextLevelXp: Int, val progress: Double)
@Serializable data class ContinueLearning(val pathId: String, val pathTitle: String, val lessonId: String? = null, val lessonTitle: String? = null, val progressPercent: Int)
@Serializable data class RecentQuestion(val questionId: String, val title: String, val technology: String, val isCorrect: Boolean? = null, val attemptedAt: String)
@Serializable data class InterviewReminder(val id: String, val title: String, val companyName: String? = null, val scheduledAt: String, val notes: String? = null)

@Serializable
data class DashboardSummary(
    val firstName: String, val profilePhotoUrl: String? = null, val tier: String, val level: Level, val currentStreak: Int, val longestStreak: Int,
    val preparationScore: Int, val questionsAttempted: Int, val correctAnswers: Int, val incorrectAnswers: Int, val challengesCompleted: Int,
    val mockInterviewsCompleted: Int, val averageMockScore: Int, val learningMinutes: Int, val weeklyMinutes: Int, val weeklyGoalMinutes: Int,
    val todayQuestions: Int, val dailyQuestionGoal: Int, val continueLearning: List<ContinueLearning> = emptyList(),
    val recommendedPaths: List<LearningPathSummary> = emptyList(), val recentQuestions: List<RecentQuestion> = emptyList(),
    val dailyChallenge: ChallengeSummary? = null, val upcomingReminders: List<InterviewReminder> = emptyList(), val unreadNotifications: Int = 0,
)

@Serializable data class ActivityDay(val date: String, val questions: Int, val correct: Int, val quizzes: Int, val challenges: Int, val lessons: Int, val minutes: Int, val xp: Int)
@Serializable data class SkillProgress(val technologyId: String, val technology: String, val attempted: Int, val correct: Int, val accuracy: Double, val proficiency: String)

@Serializable data class ScoreComponents(val questions: Int, val coding: Int, val mockInterviews: Int, val learning: Int)
@Serializable data class DifficultyPerformance(val difficulty: String, val attempted: Int, val correct: Int, val accuracy: Double)
@Serializable data class QuizStats(val quizzesTaken: Int, val averagePercentage: Double, val passRate: Double)
@Serializable data class CodingStats(val challengesAttempted: Int, val challengesSolved: Int, val successRate: Double, val submissions: Int)
@Serializable data class ScorePoint(val date: String, val score: Int)
@Serializable data class MockStats(val completed: Int, val averageOverall: Int, val averageTechnical: Int, val averageCommunication: Int, val averageProblemSolving: Int, val trend: List<ScorePoint> = emptyList())
@Serializable data class Recommendation(val title: String, val reason: String, val actionType: String, val targetId: String? = null)

@Serializable
data class AnalyticsOverview(
    val preparationScore: Int, val components: ScoreComponents, val totalAttempted: Int, val accuracy: Double,
    val difficultyBreakdown: List<DifficultyPerformance>, val quizzes: QuizStats, val coding: CodingStats, val mockInterviews: MockStats,
    val learningMinutes: Int, val weakTopics: List<TopicPerformance>, val strongTopics: List<TopicPerformance>, val recommendations: List<Recommendation>,
)

@Serializable data class CategoryPerformance(val categoryId: String, val category: String, val attempted: Int, val correct: Int, val accuracy: Double)
@Serializable data class TechnologyAnalytics(val technologyId: String, val technology: String, val attempted: Int, val correct: Int, val accuracy: Double, val categories: List<CategoryPerformance>)
@Serializable data class PeriodActivity(val periodStart: String, val questions: Int, val correct: Int, val minutes: Int, val xp: Int, val challenges: Int, val quizzes: Int)

@Serializable data class Achievement(val code: String, val name: String, val description: String, val iconKey: String, val xpReward: Int, val earned: Boolean, val awardedAt: String? = null)
@Serializable data class XpTransaction(val amount: Int, val sourceType: String, val reason: String, val createdAt: String)
@Serializable data class WeeklyGoal(val targetMinutes: Int, val currentMinutes: Int, val achieved: Boolean, val weekStart: String)
@Serializable data class GamificationProfile(val level: Level, val currentStreak: Int, val longestStreak: Int, val weeklyGoal: WeeklyGoal, val achievements: List<Achievement>, val recentXp: List<XpTransaction>)
@Serializable data class LeaderboardEntry(val rank: Int, val userId: String, val displayName: String, val profilePhotoUrl: String? = null, val xp: Int, val level: Int, val isCurrentUser: Boolean)
@Serializable data class Leaderboard(val period: String, val entries: List<LeaderboardEntry>, val currentUser: LeaderboardEntry? = null)

// ---------------- Notifications & subscriptions ----------------
@Serializable data class AppNotification(val id: String, val category: String, val title: String, val body: String, val deepLink: String? = null, val createdAt: String, val readAt: String? = null)
@Serializable data class NotificationPreference(val category: String, val enabled: Boolean, val preferredHourLocal: Int)

@Serializable
data class SubscriptionPlan(
    val id: String, val code: String, val name: String, val description: String, val tier: String, val priceMicros: Long, val currency: String,
    val billingPeriodMonths: Int, val googlePlayProductId: String? = null, val googlePlayBasePlanId: String? = null, val features: List<String> = emptyList(),
)

@Serializable data class UsageLimit(val feature: String, val used: Int, val limit: Int? = null)
@Serializable data class SubscriptionStatus(val tier: String, val status: String? = null, val planName: String? = null, val expiresAt: String? = null, val autoRenewing: Boolean = false, val provider: String? = null, val usageToday: List<UsageLimit> = emptyList())
@Serializable data class PurchaseHistoryItem(val id: String, val planName: String, val status: String, val provider: String, val startsAt: String, val expiresAt: String, val orderId: String? = null)

/** Connectivity + pending local changes, shown as the online/offline banner. */
data class SyncState(val isOnline: Boolean, val pendingChanges: Int, val lastSyncedAt: Long?)
