package com.devinterview.core.navigation

import androidx.navigation.NavController
import androidx.navigation.NavOptionsBuilder
import kotlinx.serialization.Serializable

/** Type-safe destinations for Navigation Compose. Feature modules navigate only through these routes. */
object Routes {
    // Auth
    @Serializable data object Splash
    @Serializable data object Onboarding
    @Serializable data object Login
    @Serializable data object Register
    @Serializable data class VerifyEmail(val email: String)
    @Serializable data object ForgotPassword

    // Top-level tabs
    @Serializable data object Home
    @Serializable data object Learn
    @Serializable data object Practice
    @Serializable data object MockInterviewHome
    @Serializable data object Profile

    // Dashboard & discovery
    @Serializable data object Notifications
    @Serializable data class Search(val query: String = "")

    // Question bank
    @Serializable data object TechnologyExplorer
    @Serializable data class Categories(val technologyId: String, val technologyName: String)
    @Serializable data class QuestionList(
        val technologyId: String? = null, val categoryId: String? = null, val title: String = "Questions",
        val difficulty: String? = null, val bookmarkedOnly: Boolean = false,
    )
    @Serializable data class QuestionDetail(val questionId: String)
    @Serializable data class RandomQuestion(val technologyId: String? = null)
    @Serializable data object Bookmarks
    @Serializable data object Notes
    @Serializable data object Collections
    @Serializable data class CollectionDetail(val collectionId: String, val name: String)

    // Quiz
    @Serializable data object QuizHome
    @Serializable data class QuizConfig(val technologyId: String? = null)
    @Serializable data class QuizPlayer(val quizId: String)
    @Serializable data class QuizResult(val attemptId: String)

    // Coding
    @Serializable data class ChallengeList(val category: String? = null)
    @Serializable data class ChallengeDetail(val challengeId: String)
    @Serializable data class CodeEditor(val challengeId: String)
    @Serializable data class Submissions(val challengeId: String? = null)
    @Serializable data class SubmissionDetail(val submissionId: String)

    // AI
    @Serializable data class InterviewSetup(val type: String? = null)
    @Serializable data class InterviewChat(val interviewId: String)
    @Serializable data class InterviewReport(val interviewId: String)
    @Serializable data class AnswerEvaluator(val questionId: String? = null, val questionText: String? = null)
    @Serializable data class EvaluationDetail(val evaluationId: String)
    @Serializable data object CodeReviewer

    // Learning & companies
    @Serializable data class LearningPathDetail(val pathId: String)
    @Serializable data class LessonDetail(val lessonId: String)
    @Serializable data object Companies
    @Serializable data class CompanyDetail(val companyId: String)
    @Serializable data class CompanyCollection(val collectionId: String)

    // Flashcards
    @Serializable data object FlashcardDecks
    @Serializable data class FlashcardStudy(val technologyId: String? = null, val deckName: String = "All cards", val bookmarkedOnly: Boolean = false)

    // Analytics & gamification
    @Serializable data object Analytics
    @Serializable data object Achievements
    @Serializable data object Leaderboard

    // Profile & settings
    @Serializable data object EditProfile
    @Serializable data object Subscription
    @Serializable data object Settings
    @Serializable data object ChangePassword
    @Serializable data object NotificationSettings
    @Serializable data object InterviewReminders
    @Serializable data object Help
}

/** Navigates to a top-level destination clearing intermediate history. */
fun NavController.navigateTopLevel(route: Any) = navigate(route) {
    popUpTo(graph.startDestinationId) { saveState = true }
    launchSingleTop = true
    restoreState = true
}

fun NavOptionsBuilder.clearAll(nav: NavController) = popUpTo(nav.graph.id) { inclusive = true }

/** Parses devinterview:// deep links from notifications into routes. */
object DeepLinks {
    const val SCHEME = "devinterview"

    fun toRoute(uri: String): Any? {
        if (!uri.startsWith("$SCHEME://")) return null
        val parts = uri.removePrefix("$SCHEME://").split('/').filter { it.isNotBlank() }
        return when (parts.firstOrNull()) {
            "challenge" -> parts.getOrNull(1)?.let { Routes.ChallengeDetail(it) } ?: Routes.ChallengeList()
            "question" -> parts.getOrNull(1)?.let { Routes.QuestionDetail(it) }
            "quiz" -> if (parts.getOrNull(1) == "daily") Routes.QuizHome else Routes.QuizHome
            "practice" -> Routes.Practice
            "learn" -> Routes.Learn
            "flashcards" -> Routes.FlashcardDecks
            "analytics" -> Routes.Analytics
            "achievements" -> Routes.Achievements
            "mock-interview" -> Routes.MockInterviewHome
            "subscription" -> Routes.Subscription
            else -> Routes.Home
        }
    }
}
