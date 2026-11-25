package com.devinterview.core.network

import com.devinterview.domain.model.*
import kotlinx.serialization.Serializable
import kotlinx.serialization.json.JsonElement
import okhttp3.MultipartBody
import okhttp3.ResponseBody
import retrofit2.http.*

@Serializable
data class ApiErrorBody(val code: String = "unknown", val message: String = "", val details: Map<String, List<String>>? = null)

/** Envelope used by every DevInterview endpoint. */
@Serializable
data class Envelope<T>(val success: Boolean = false, val data: T? = null, val error: ApiErrorBody? = null, val traceId: String? = null)

@Serializable data class Message(val message: String = "")

// ---------------- request bodies ----------------
@Serializable data class LoginBody(val email: String, val password: String, val deviceName: String?)
@Serializable data class GoogleBody(val idToken: String, val deviceName: String?)
@Serializable data class RefreshBody(val refreshToken: String)
@Serializable data class LogoutBody(val refreshToken: String, val allDevices: Boolean = false)
@Serializable data class EmailBody(val email: String)
@Serializable data class CodeBody(val email: String, val code: String)
@Serializable data class ResetBody(val email: String, val code: String, val newPassword: String, val confirmPassword: String)
@Serializable data class ChangePasswordBody(val currentPassword: String, val newPassword: String, val confirmPassword: String)
@Serializable data class ProfileBody(val firstName: String, val lastName: String, val experienceLevel: String, val currentJobRole: String?, val targetJobRole: String?, val bio: String?, val preferredTechnologyIds: List<String>)
@Serializable data class PreferencesBody(val timeZoneId: String, val dailyQuestionGoal: Int, val weeklyGoalMinutes: Int, val showOnLeaderboard: Boolean)
@Serializable data class DeleteAccountBody(val password: String?, val confirmation: String = "DELETE")
@Serializable data class DeviceBody(val token: String, val platform: String = "android")
@Serializable data class AttemptBody(val selectedOptionIds: List<String>, val timeSpentSeconds: Int, val selfAssessedCorrect: Boolean?)
@Serializable data class NoteBody(val content: String)
@Serializable data class ReportBody(val reason: String, val details: String?)
@Serializable data class SubmitQuizBody(val attemptId: String, val answers: List<QuizAnswer>)
@Serializable data class RunBody(val challengeId: String, val languageCode: String, val sourceCode: String, val customInput: String?)
@Serializable data class SubmitCodeBody(val challengeId: String, val languageCode: String, val sourceCode: String, val idempotencyKey: String)
@Serializable data class EvaluateBody(val questionId: String?, val questionText: String?, val answer: String)
@Serializable data class ReviewBody(val languageCode: String, val code: String, val context: String?)
@Serializable data class InterviewAnswerBody(val questionId: String, val answer: String, val responseSeconds: Int, val usedVoiceInput: Boolean)
@Serializable data class ProgressBody(val lessonId: String, val status: String, val timeSpentSeconds: Int)
@Serializable data class ReviewCardBody(val flashcardId: String, val grade: String, val clientReviewId: String, val reviewedAt: String?)
@Serializable data class BookmarkCardBody(val isBookmarked: Boolean)
@Serializable data class CollectionBody(val name: String, val description: String?)
@Serializable data class CollectionItemBody(val itemType: String, val itemId: String)
@Serializable data class NotificationPrefsBody(val preferences: List<NotificationPreference>)
@Serializable data class ReminderBody(val title: String, val companyName: String?, val scheduledAt: String, val notes: String?)
@Serializable data class VerifyPurchaseBody(val productId: String, val purchaseToken: String)
@Serializable data class BookmarkChange(val questionId: String, val isBookmarked: Boolean, val clientUpdatedAt: String)
@Serializable data class NoteChange(val questionId: String, val content: String, val isDeleted: Boolean, val clientUpdatedAt: String)
@Serializable data class SyncPushBody(val bookmarks: List<BookmarkChange>, val notes: List<NoteChange>, val flashcardReviews: List<ReviewCardBody>)
@Serializable data class SyncConflict(val entityType: String, val entityId: String, val resolution: String)
@Serializable data class SyncPushResult(val appliedBookmarks: Int, val appliedNotes: Int, val appliedReviews: Int, val conflicts: List<SyncConflict> = emptyList(), val serverTime: String)
@Serializable data class BookmarkState(val questionId: String, val isBookmarked: Boolean, val updatedAt: String)
@Serializable data class NoteState(val questionId: String, val content: String, val isDeleted: Boolean, val updatedAt: String)
@Serializable data class SyncPullResult(val bookmarks: List<BookmarkState> = emptyList(), val notes: List<NoteState> = emptyList(), val serverTime: String)
@Serializable data class PhotoResult(val profilePhotoUrl: String)

interface AuthApi {
    @POST("api/v1/auth/login") suspend fun login(@Body body: LoginBody): Envelope<AuthSession>
    @POST("api/v1/auth/google") suspend fun google(@Body body: GoogleBody): Envelope<AuthSession>
    @POST("api/v1/auth/register") suspend fun register(@Body body: RegistrationForm): Envelope<RegisterResult>
    @POST("api/v1/auth/verify-email") suspend fun verifyEmail(@Body body: CodeBody): Envelope<AuthSession>
    @POST("api/v1/auth/resend-verification") suspend fun resend(@Body body: EmailBody): Envelope<Message>
    @POST("api/v1/auth/forgot-password") suspend fun forgot(@Body body: EmailBody): Envelope<Message>
    @POST("api/v1/auth/reset-password") suspend fun reset(@Body body: ResetBody): Envelope<Message>
    @POST("api/v1/auth/change-password") suspend fun changePassword(@Body body: ChangePasswordBody): Envelope<AuthSession>
    @POST("api/v1/auth/logout") suspend fun logout(@Body body: LogoutBody): Envelope<Message>
}

/** Used only by the token authenticator (no authenticator attached, so no recursion). */
interface RefreshApi {
    @POST("api/v1/auth/refresh") suspend fun refresh(@Body body: RefreshBody): Envelope<AuthSession>
}

interface UserApi {
    @GET("api/v1/users/me") suspend fun me(): Envelope<UserProfile>
    @PUT("api/v1/users/me") suspend fun update(@Body body: ProfileBody): Envelope<UserProfile>
    @PUT("api/v1/users/me/preferences") suspend fun preferences(@Body body: PreferencesBody): Envelope<UserProfile>
    @Multipart @POST("api/v1/users/me/photo") suspend fun photo(@Part file: MultipartBody.Part): Envelope<PhotoResult>
    @POST("api/v1/users/me/delete") suspend fun delete(@Body body: DeleteAccountBody): Envelope<Message>
    @GET("api/v1/users/me/export") suspend fun export(): Envelope<JsonElement>
    @POST("api/v1/users/me/devices") suspend fun registerDevice(@Body body: DeviceBody): Envelope<Message>
}

interface ContentApi {
    @GET("api/v1/technologies") suspend fun technologies(): Envelope<List<Technology>>
    @GET("api/v1/technologies/{id}/categories") suspend fun categories(@Path("id") id: String): Envelope<List<Category>>
    @GET("api/v1/questions") suspend fun questions(@QueryMap query: Map<String, String>): Envelope<Page<QuestionSummary>>
    @GET("api/v1/questions/{id}") suspend fun question(@Path("id") id: String): Envelope<QuestionDetail>
    @GET("api/v1/questions/random") suspend fun random(@Query("technologyId") technologyId: String?, @Query("difficulty") difficulty: String?): Envelope<QuestionDetail>
    @GET("api/v1/questions/{id}/answer") suspend fun answer(@Path("id") id: String): Envelope<QuestionAnswer>
    @POST("api/v1/questions/{id}/attempt") suspend fun attempt(@Path("id") id: String, @Body body: AttemptBody): Envelope<AttemptResult>
    @POST("api/v1/questions/{id}/report") suspend fun report(@Path("id") id: String, @Body body: ReportBody): Envelope<Message>
    @GET("api/v1/questions/report-reasons") suspend fun reportReasons(): Envelope<List<String>>
    @GET("api/v1/questions/notes") suspend fun notes(@Query("page") page: Int, @Query("pageSize") pageSize: Int = 100): Envelope<Page<Note>>
    @GET("api/v1/questions/notes/export") suspend fun exportNotes(@Query("format") format: String): ResponseBody
    @GET("api/v1/collections") suspend fun collections(): Envelope<List<SavedCollection>>
    @GET("api/v1/collections/{id}") suspend fun collection(@Path("id") id: String): Envelope<SavedCollectionDetail>
    @POST("api/v1/collections") suspend fun createCollection(@Body body: CollectionBody): Envelope<SavedCollection>
    @DELETE("api/v1/collections/{id}") suspend fun deleteCollection(@Path("id") id: String): Envelope<Message>
    @POST("api/v1/collections/{id}/items") suspend fun addItem(@Path("id") id: String, @Body body: CollectionItemBody): Envelope<SavedCollectionItem>
    @DELETE("api/v1/collections/{id}/items/{itemId}") suspend fun removeItem(@Path("id") id: String, @Path("itemId") itemId: String): Envelope<Message>
    @GET("api/v1/search") suspend fun search(@Query("q") q: String, @Query("type") type: String?, @Query("page") page: Int, @Query("pageSize") pageSize: Int = 20): Envelope<SearchResults>
    @GET("api/v1/companies") suspend fun companies(): Envelope<List<CompanySummary>>
    @GET("api/v1/companies/{id}") suspend fun company(@Path("id") id: String): Envelope<CompanyDetail>
    @GET("api/v1/companies/collections/{id}") suspend fun companyCollection(@Path("id") id: String): Envelope<CompanyCollectionDetail>
}

interface PracticeApi {
    @GET("api/v1/quizzes") suspend fun quizzes(@Query("mode") mode: String?, @Query("technologyId") technologyId: String?, @Query("page") page: Int, @Query("pageSize") pageSize: Int = 20): Envelope<Page<QuizSummary>>
    @GET("api/v1/quizzes/daily") suspend fun daily(): Envelope<QuizSummary>
    @POST("api/v1/quizzes") suspend fun create(@Body body: CreateQuiz): Envelope<QuizSummary>
    @POST("api/v1/quizzes/{id}/start") suspend fun start(@Path("id") id: String): Envelope<QuizAttempt>
    @PUT("api/v1/quizzes/attempts/{attemptId}/answers") suspend fun saveAnswer(@Path("attemptId") attemptId: String, @Body body: QuizAnswer): Envelope<Message>
    @POST("api/v1/quizzes/{id}/submit") suspend fun submit(@Path("id") id: String, @Body body: SubmitQuizBody): Envelope<QuizResult>
    @GET("api/v1/quizzes/attempts/{attemptId}") suspend fun result(@Path("attemptId") attemptId: String): Envelope<QuizResult>
    @GET("api/v1/quizzes/attempts") suspend fun history(@Query("page") page: Int, @Query("pageSize") pageSize: Int = 20): Envelope<Page<QuizAttemptSummary>>

    @GET("api/v1/code/languages") suspend fun languages(): Envelope<List<Language>>
    @GET("api/v1/challenges") suspend fun challenges(@QueryMap query: Map<String, String>): Envelope<Page<ChallengeSummary>>
    @GET("api/v1/challenges/{id}") suspend fun challenge(@Path("id") id: String): Envelope<ChallengeDetail>
    @GET("api/v1/challenges/{id}/hints/{index}") suspend fun hint(@Path("id") id: String, @Path("index") index: Int): Envelope<Hint>
    @GET("api/v1/challenges/{id}/solution") suspend fun solution(@Path("id") id: String): Envelope<Solution>
    @POST("api/v1/code/run") suspend fun run(@Body body: RunBody): Envelope<RunResult>
    @POST("api/v1/code/submit") suspend fun submit(@Body body: SubmitCodeBody): Envelope<Submission>
    @GET("api/v1/code/submissions") suspend fun submissions(@Query("challengeId") challengeId: String?, @Query("page") page: Int, @Query("pageSize") pageSize: Int = 20): Envelope<Page<Submission>>
    @GET("api/v1/code/submissions/{id}") suspend fun submission(@Path("id") id: String): Envelope<Submission>

    @GET("api/v1/flashcards/decks") suspend fun decks(): Envelope<List<FlashcardDeck>>
    @GET("api/v1/flashcards") suspend fun flashcards(@Query("technologyId") technologyId: String?, @Query("dueOnly") dueOnly: Boolean, @Query("bookmarkedOnly") bookmarkedOnly: Boolean, @Query("limit") limit: Int = 30): Envelope<List<Flashcard>>
    @POST("api/v1/flashcards/review") suspend fun review(@Body body: ReviewCardBody): Envelope<FlashcardReviewResult>
    @PUT("api/v1/flashcards/{id}/bookmark") suspend fun bookmarkCard(@Path("id") id: String, @Body body: BookmarkCardBody): Envelope<Message>
}

interface AiApi {
    @GET("api/v1/ai/status") suspend fun status(): Envelope<AiStatus>
    @POST("api/v1/ai/evaluate-answer") suspend fun evaluate(@Body body: EvaluateBody): Envelope<AnswerEvaluation>
    @GET("api/v1/ai/evaluations") suspend fun evaluations(@Query("page") page: Int): Envelope<Page<EvaluationSummary>>
    @GET("api/v1/ai/evaluations/{id}") suspend fun evaluation(@Path("id") id: String): Envelope<AnswerEvaluation>
    @POST("api/v1/ai/review-code") suspend fun review(@Body body: ReviewBody): Envelope<CodeReview>
    @POST("api/v1/mock-interviews/start") suspend fun startInterview(@Body body: StartInterview): Envelope<MockInterview>
    @GET("api/v1/mock-interviews/{id}") suspend fun interview(@Path("id") id: String): Envelope<MockInterview>
    @GET("api/v1/mock-interviews") suspend fun interviews(@Query("page") page: Int): Envelope<Page<InterviewSummary>>
    @POST("api/v1/mock-interviews/{id}/answer") suspend fun answer(@Path("id") id: String, @Body body: InterviewAnswerBody): Envelope<MockInterview>
    @POST("api/v1/mock-interviews/{id}/next-question") suspend fun next(@Path("id") id: String): Envelope<MockInterview>
    @POST("api/v1/mock-interviews/{id}/complete") suspend fun complete(@Path("id") id: String): Envelope<InterviewReport>
    @GET("api/v1/mock-interviews/{id}/report") suspend fun report(@Path("id") id: String): Envelope<InterviewReport>
}

interface EngagementApi {
    @GET("api/v1/dashboard/summary") suspend fun dashboard(): Envelope<DashboardSummary>
    @GET("api/v1/dashboard/activity") suspend fun activity(@Query("days") days: Int): Envelope<List<ActivityDay>>
    @GET("api/v1/dashboard/skill-progress") suspend fun skills(): Envelope<List<SkillProgress>>
    @GET("api/v1/analytics/overview") suspend fun overview(): Envelope<AnalyticsOverview>
    @GET("api/v1/analytics/technology") suspend fun technologyAnalytics(): Envelope<List<TechnologyAnalytics>>
    @GET("api/v1/analytics/weekly") suspend fun weekly(@Query("weeks") weeks: Int): Envelope<List<PeriodActivity>>
    @GET("api/v1/analytics/monthly") suspend fun monthly(@Query("months") months: Int): Envelope<List<PeriodActivity>>
    @GET("api/v1/gamification/profile") suspend fun gamification(): Envelope<GamificationProfile>
    @GET("api/v1/gamification/leaderboard") suspend fun leaderboard(@Query("period") period: String): Envelope<Leaderboard>
    @GET("api/v1/learning/paths") suspend fun paths(): Envelope<List<LearningPathSummary>>
    @GET("api/v1/learning/paths/{id}") suspend fun path(@Path("id") id: String): Envelope<LearningPathDetail>
    @GET("api/v1/learning/lessons/{id}") suspend fun lesson(@Path("id") id: String): Envelope<Lesson>
    @POST("api/v1/learning/progress") suspend fun progress(@Body body: ProgressBody): Envelope<LessonProgress>
    @GET("api/v1/notifications") suspend fun notifications(@Query("page") page: Int, @Query("pageSize") pageSize: Int = 30): Envelope<Page<AppNotification>>
    @POST("api/v1/notifications/{id}/read") suspend fun read(@Path("id") id: String): Envelope<Message>
    @POST("api/v1/notifications/read-all") suspend fun readAll(): Envelope<Message>
    @GET("api/v1/notifications/preferences") suspend fun notificationPreferences(): Envelope<List<NotificationPreference>>
    @PUT("api/v1/notifications/preferences") suspend fun updateNotificationPreferences(@Body body: NotificationPrefsBody): Envelope<List<NotificationPreference>>
    @GET("api/v1/notifications/reminders") suspend fun reminders(): Envelope<List<InterviewReminder>>
    @POST("api/v1/notifications/reminders") suspend fun createReminder(@Body body: ReminderBody): Envelope<InterviewReminder>
    @DELETE("api/v1/notifications/reminders/{id}") suspend fun deleteReminder(@Path("id") id: String): Envelope<Message>
    @GET("api/v1/subscriptions/plans") suspend fun plans(): Envelope<List<SubscriptionPlan>>
    @GET("api/v1/subscriptions/status") suspend fun subscriptionStatus(): Envelope<SubscriptionStatus>
    @GET("api/v1/subscriptions/history") suspend fun purchaseHistory(): Envelope<List<PurchaseHistoryItem>>
    @POST("api/v1/subscriptions/google-play/verify") suspend fun verifyPurchase(@Body body: VerifyPurchaseBody): Envelope<SubscriptionStatus>
    @POST("api/v1/sync/push") suspend fun push(@Body body: SyncPushBody): Envelope<SyncPushResult>
    @GET("api/v1/sync/pull") suspend fun pull(@Query("since") since: String?): Envelope<SyncPullResult>
}
