package com.devinterview.domain.repository

import com.devinterview.domain.model.*
import kotlinx.coroutines.flow.Flow

interface AuthRepository {
    /** Emits the signed-in user (cached locally) or null. */
    val currentUser: Flow<UserSummary?>
    suspend fun login(email: String, password: String): AppResult<UserSummary>
    suspend fun loginWithGoogle(idToken: String): AppResult<UserSummary>
    suspend fun register(form: RegistrationForm): AppResult<RegisterResult>
    suspend fun verifyEmail(email: String, code: String): AppResult<UserSummary>
    suspend fun resendVerification(email: String): AppResult<Unit>
    suspend fun forgotPassword(email: String): AppResult<Unit>
    suspend fun resetPassword(email: String, code: String, newPassword: String): AppResult<Unit>
    suspend fun changePassword(current: String, newPassword: String): AppResult<Unit>
    suspend fun logout(allDevices: Boolean = false)
    suspend fun refreshUser(): AppResult<UserSummary>
}

interface ProfileRepository {
    suspend fun getProfile(): AppResult<UserProfile>
    suspend fun updateProfile(firstName: String, lastName: String, experienceLevel: String, currentRole: String?, targetRole: String?, bio: String?, technologyIds: List<String>): AppResult<UserProfile>
    suspend fun updatePreferences(timeZoneId: String, dailyGoal: Int, weeklyGoalMinutes: Int, showOnLeaderboard: Boolean): AppResult<UserProfile>
    suspend fun uploadPhoto(bytes: ByteArray, mimeType: String): AppResult<String>
    suspend fun deleteAccount(password: String?): AppResult<Unit>
    suspend fun exportData(): AppResult<String>
    suspend fun registerDevice(token: String): AppResult<Unit>
}

interface QuestionRepository {
    suspend fun technologies(): AppResult<List<Technology>>
    suspend fun categories(technologyId: String): AppResult<List<Category>>
    suspend fun questions(filter: QuestionFilter, page: Int, pageSize: Int): AppResult<Page<QuestionSummary>>
    /** Network first; falls back to the offline copy for previously opened/bookmarked questions. */
    suspend fun question(id: String): AppResult<QuestionDetail>
    suspend fun random(technologyId: String?, difficulty: String?): AppResult<QuestionDetail>
    suspend fun answer(id: String): AppResult<QuestionAnswer>
    suspend fun attempt(id: String, selectedOptionIds: List<String>, timeSpentSeconds: Int, selfAssessedCorrect: Boolean?): AppResult<AttemptResult>
    suspend fun report(id: String, reason: String, details: String?): AppResult<Unit>
    suspend fun reportReasons(): AppResult<List<String>>
}

/** Offline-first personal data: writes go to Room immediately and are synchronised in the background. */
interface LibraryRepository {
    fun observeBookmarks(): Flow<List<QuestionSummary>>
    fun observeNotes(): Flow<List<Note>>
    fun observeIsBookmarked(questionId: String): Flow<Boolean>
    fun observeNote(questionId: String): Flow<String?>
    suspend fun setBookmark(question: QuestionSummary, bookmarked: Boolean)
    suspend fun saveNote(questionId: String, questionTitle: String, technology: String, content: String)
    suspend fun deleteNote(questionId: String)
    suspend fun exportNotes(format: String): AppResult<String>
    suspend fun collections(): AppResult<List<SavedCollection>>
    suspend fun collection(id: String): AppResult<SavedCollectionDetail>
    suspend fun createCollection(name: String, description: String?): AppResult<SavedCollection>
    suspend fun deleteCollection(id: String): AppResult<Unit>
    suspend fun addToCollection(collectionId: String, itemType: String, itemId: String): AppResult<Unit>
    suspend fun removeFromCollection(collectionId: String, itemId: String): AppResult<Unit>
}

interface QuizRepository {
    suspend fun quizzes(mode: String?, technologyId: String?, page: Int): AppResult<Page<QuizSummary>>
    suspend fun daily(): AppResult<QuizSummary>
    suspend fun create(request: CreateQuiz): AppResult<QuizSummary>
    suspend fun start(quizId: String): AppResult<QuizAttempt>
    suspend fun saveAnswer(attemptId: String, answer: QuizAnswer): AppResult<Unit>
    suspend fun submit(quizId: String, attemptId: String, answers: List<QuizAnswer>): AppResult<QuizResult>
    suspend fun result(attemptId: String): AppResult<QuizResult>
    suspend fun history(page: Int): AppResult<Page<QuizAttemptSummary>>
}

interface CodingRepository {
    suspend fun languages(): AppResult<List<Language>>
    suspend fun challenges(category: String?, difficulty: String?, status: String?, search: String?, page: Int): AppResult<Page<ChallengeSummary>>
    suspend fun challenge(id: String): AppResult<ChallengeDetail>
    suspend fun hint(id: String, index: Int): AppResult<Hint>
    suspend fun solution(id: String): AppResult<Solution>
    suspend fun run(challengeId: String, language: String, source: String, customInput: String?): AppResult<RunResult>
    suspend fun submit(challengeId: String, language: String, source: String, idempotencyKey: String): AppResult<Submission>
    suspend fun submissions(challengeId: String?, page: Int): AppResult<Page<Submission>>
    suspend fun submission(id: String): AppResult<Submission>
    /** Locally saved draft code per challenge/language. */
    suspend fun draft(challengeId: String, language: String): String?
    suspend fun saveDraft(challengeId: String, language: String, code: String)
}

interface AiRepository {
    suspend fun status(): AppResult<AiStatus>
    suspend fun evaluateAnswer(questionId: String?, questionText: String?, answer: String): AppResult<AnswerEvaluation>
    suspend fun evaluations(page: Int): AppResult<Page<EvaluationSummary>>
    suspend fun evaluation(id: String): AppResult<AnswerEvaluation>
    suspend fun reviewCode(language: String, code: String, context: String?): AppResult<CodeReview>
}

interface InterviewRepository {
    suspend fun start(request: StartInterview): AppResult<MockInterview>
    suspend fun get(id: String): AppResult<MockInterview>
    suspend fun list(page: Int): AppResult<Page<InterviewSummary>>
    suspend fun answer(id: String, questionId: String, answer: String, responseSeconds: Int, usedVoice: Boolean): AppResult<MockInterview>
    suspend fun skip(id: String): AppResult<MockInterview>
    suspend fun complete(id: String): AppResult<InterviewReport>
    suspend fun report(id: String): AppResult<InterviewReport>
}

interface LearningRepository {
    suspend fun paths(): AppResult<List<LearningPathSummary>>
    suspend fun path(id: String): AppResult<LearningPathDetail>
    /** Network first with offline fallback to downloaded lessons. */
    suspend fun lesson(id: String): AppResult<Lesson>
    suspend fun updateProgress(lessonId: String, status: String, timeSpentSeconds: Int): AppResult<LessonProgress>
    /** Downloads every lesson of a path for offline reading. Returns the number of lessons saved. */
    suspend fun downloadPath(id: String): AppResult<Int>
    fun observeDownloadedPathIds(): Flow<Set<String>>
    suspend fun companies(): AppResult<List<CompanySummary>>
    suspend fun company(id: String): AppResult<CompanyDetail>
    suspend fun companyCollection(id: String): AppResult<CompanyCollectionDetail>
}

interface FlashcardRepository {
    suspend fun decks(): AppResult<List<FlashcardDeck>>
    /** Loads a study session from the network and caches it; offline returns cached cards not yet reviewed locally. */
    suspend fun session(technologyId: String?, dueOnly: Boolean, bookmarkedOnly: Boolean): AppResult<List<Flashcard>>
    /** Records a review locally (always succeeds) and uploads it when online. */
    suspend fun review(card: Flashcard, grade: FlashcardGrade)
    suspend fun setBookmark(cardId: String, bookmarked: Boolean): AppResult<Unit>
}

interface DashboardRepository {
    suspend fun summary(): AppResult<DashboardSummary>
    suspend fun activity(days: Int): AppResult<List<ActivityDay>>
    suspend fun skills(): AppResult<List<SkillProgress>>
    suspend fun search(query: String, type: String?, page: Int): AppResult<SearchResults>
}

interface AnalyticsRepository {
    suspend fun overview(): AppResult<AnalyticsOverview>
    suspend fun technologies(): AppResult<List<TechnologyAnalytics>>
    suspend fun weekly(weeks: Int): AppResult<List<PeriodActivity>>
    suspend fun monthly(months: Int): AppResult<List<PeriodActivity>>
    suspend fun gamification(): AppResult<GamificationProfile>
    suspend fun leaderboard(period: String): AppResult<Leaderboard>
}

interface NotificationRepository {
    suspend fun notifications(page: Int): AppResult<Page<AppNotification>>
    suspend fun markRead(id: String): AppResult<Unit>
    suspend fun markAllRead(): AppResult<Unit>
    suspend fun preferences(): AppResult<List<NotificationPreference>>
    suspend fun updatePreferences(preferences: List<NotificationPreference>): AppResult<List<NotificationPreference>>
    suspend fun reminders(): AppResult<List<InterviewReminder>>
    suspend fun createReminder(title: String, company: String?, scheduledAtIso: String, notes: String?): AppResult<InterviewReminder>
    suspend fun deleteReminder(id: String): AppResult<Unit>
}

interface SubscriptionRepository {
    suspend fun plans(): AppResult<List<SubscriptionPlan>>
    suspend fun status(): AppResult<SubscriptionStatus>
    suspend fun history(): AppResult<List<PurchaseHistoryItem>>
    /** Sends a Google Play purchase token to the backend, which verifies it with Google before granting access. */
    suspend fun verifyGooglePlayPurchase(productId: String, purchaseToken: String): AppResult<SubscriptionStatus>
}

interface SyncRepository {
    val state: Flow<SyncState>
    /** Pushes pending local changes and pulls server changes. */
    suspend fun sync(): AppResult<Unit>
    fun scheduleSync()
}

interface SettingsRepository {
    val themeMode: Flow<String>
    val onboardingCompleted: Flow<Boolean>
    val notificationsEnabled: Flow<Boolean>
    suspend fun setThemeMode(mode: String)
    suspend fun setOnboardingCompleted()
    suspend fun setNotificationsEnabled(enabled: Boolean)
}
