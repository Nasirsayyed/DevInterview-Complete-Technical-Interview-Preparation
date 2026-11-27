package com.devinterview.data

import com.devinterview.core.network.Envelope
import com.devinterview.core.network.NetworkJson
import com.devinterview.core.network.SyncPullResult
import com.devinterview.domain.model.*
import com.google.common.truth.Truth.assertThat
import kotlinx.serialization.KSerializer
import kotlinx.serialization.builtins.ListSerializer
import kotlinx.serialization.builtins.serializer
import org.junit.Test

/**
 * Contract test: the fixtures in `src/test/resources/contract` are real responses captured from the running API
 * (seeded database). Decoding them with the app's models and JSON configuration catches drift between the C# DTOs and
 * the Kotlin models (renamed/missing required fields, enum spellings, nullability) before it reaches users.
 * Refresh the fixtures with `scripts/DevelopmentUtilities/capture-contract-fixtures.sh` when the API changes.
 */
class ApiContractTest {
    private fun <T> decode(name: String, serializer: KSerializer<T>): T {
        val text = javaClass.classLoader!!.getResource("contract/$name.json")?.readText() ?: error("Missing fixture $name.json")
        val envelope = NetworkJson.decodeFromString(Envelope.serializer(serializer), text)
        assertThat(envelope.success).isTrue()
        return envelope.data ?: error("$name.json has no data")
    }

    private fun <T> list(s: KSerializer<T>) = ListSerializer(s)
    private fun <T> page(s: KSerializer<T>) = Page.serializer(s)

    @Test fun profile() { assertThat(decode("me", UserProfile.serializer()).roles).isNotEmpty() }

    @Test fun questionBank() {
        val techs = decode("technologies", list(Technology.serializer()))
        assertThat(techs).isNotEmpty()
        assertThat(decode("categories", list(Category.serializer()))).isNotEmpty()
        assertThat(decode("questions", page(QuestionSummary.serializer())).items).isNotEmpty()
        val q = decode("question", QuestionDetail.serializer())
        assertThat(q.options).isNotEmpty()
        assertThat(decode("answer", QuestionAnswer.serializer()).correctOptionIds).isNotEmpty()
        assertThat(decode("attempt", AttemptResult.serializer()).answer.questionId).isEqualTo(q.id)
        assertThat(decode("report-reasons", list(String.serializer()))).isNotEmpty()
        decode("notes", page(Note.serializer()))
        decode("collections", list(SavedCollection.serializer()))
        assertThat(decode("search", SearchResults.serializer()).hits.items).isNotEmpty()
    }

    @Test fun companies() {
        assertThat(decode("companies", list(CompanySummary.serializer()))).isNotEmpty()
        val company = decode("company", CompanyDetail.serializer())
        assertThat(company.disclaimer).isNotEmpty()
        assertThat(company.rounds.map { it.source }.toSet()).containsAnyOf("Verified", "CommunityContributed", "Illustrative")
        decode("company-collection", CompanyCollectionDetail.serializer())
    }

    @Test fun quizzes() {
        decode("quizzes", page(QuizSummary.serializer()))
        decode("daily", QuizSummary.serializer())
        val attempt = decode("quiz-start", QuizAttempt.serializer())
        assertThat(attempt.questions).isNotEmpty()
        assertThat(attempt.serverNow).isNotEmpty()
        val result = decode("quiz-submit", QuizResult.serializer())
        assertThat(result.attemptId).isEqualTo(attempt.attemptId)
        assertThat(decode("quiz-result", QuizResult.serializer()).review).isNotEmpty()
        decode("quiz-history", page(QuizAttemptSummary.serializer()))
    }

    @Test fun coding() {
        assertThat(decode("languages", list(Language.serializer()))).isNotEmpty()
        assertThat(decode("challenges", page(ChallengeSummary.serializer())).items).isNotEmpty()
        val challenge = decode("challenge", ChallengeDetail.serializer())
        assertThat(challenge.sampleTestCases).isNotEmpty()
        assertThat(challenge.starterCode).isNotEmpty()
        decode("hint", Hint.serializer())
        decode("solution", Solution.serializer())
        decode("submissions", page(Submission.serializer()))
    }

    @Test fun learningAndFlashcards() {
        assertThat(decode("paths", list(LearningPathSummary.serializer()))).isNotEmpty()
        assertThat(decode("path", LearningPathDetail.serializer()).modules).isNotEmpty()
        assertThat(decode("lesson", Lesson.serializer()).content).isNotEmpty()
        assertThat(decode("decks", list(FlashcardDeck.serializer()))).isNotEmpty()
        assertThat(decode("flashcards", list(Flashcard.serializer()))).isNotEmpty()
    }

    @Test fun aiAndInterviews() {
        decode("ai-status", AiStatus.serializer())
        decode("evaluations", page(EvaluationSummary.serializer()))
        decode("interviews", page(InterviewSummary.serializer()))
    }

    @Test fun dashboardAnalyticsGamification() {
        decode("dashboard", DashboardSummary.serializer())
        assertThat(decode("activity", list(ActivityDay.serializer()))).hasSize(14)
        decode("skills", list(SkillProgress.serializer()))
        decode("analytics", AnalyticsOverview.serializer())
        decode("analytics-tech", list(TechnologyAnalytics.serializer()))
        decode("weekly", list(PeriodActivity.serializer()))
        decode("monthly", list(PeriodActivity.serializer()))
        assertThat(decode("gamification", GamificationProfile.serializer()).achievements).isNotEmpty()
        decode("leaderboard", Leaderboard.serializer())
    }

    @Test fun notificationsSubscriptionsSync() {
        decode("notifications", page(AppNotification.serializer()))
        assertThat(decode("notification-prefs", list(NotificationPreference.serializer()))).isNotEmpty()
        decode("reminders", list(InterviewReminder.serializer()))
        val plans = decode("plans", list(SubscriptionPlan.serializer()))
        assertThat(plans.mapNotNull { it.googlePlayProductId }).contains("devinterview_premium")
        decode("subscription-status", SubscriptionStatus.serializer())
        decode("purchase-history", list(PurchaseHistoryItem.serializer()))
        decode("sync-pull", SyncPullResult.serializer())
    }
}
