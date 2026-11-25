package com.devinterview.core.testing

import com.devinterview.domain.model.*
import com.devinterview.domain.repository.*
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.flowOf
import kotlinx.coroutines.flow.map

/** Test data builders shared by ViewModel and UI tests. */
object TestData {
    val tech = Ref("t1", "C#", "csharp")
    val category = Ref("c1", "Language Basics", "basics")

    fun user(id: String = "u1", email: String = "dev@example.com") = UserSummary(id, email, "Ada", "Lovelace", emailConfirmed = true)

    fun question(id: String = "q1", multiple: Boolean = false, objective: Boolean = true, next: String? = "q2") = QuestionDetail(
        id = id, title = "What does the 'sealed' keyword do?", body = "Explain sealed classes.", technology = tech, category = category,
        difficulty = "Beginner", type = if (objective) "SingleChoice" else "Descriptive", isObjective = objective, allowsMultipleAnswers = multiple,
        options = if (objective) listOf(QuestionOption("o1", "Prevents inheritance", 0), QuestionOption("o2", "Makes it static", 1), QuestionOption("o3", "Makes it abstract", 2)) else emptyList(),
        nextId = next, shareText = "Share me",
    )

    fun answer(questionId: String = "q1") = QuestionAnswer(questionId, "Prevents inheritance.", "A sealed class cannot be derived from.", listOf("o1"))

    fun quizAttempt(expiresAt: String? = null, serverNow: String = "2026-01-01T10:00:00Z", questions: Int = 3) = QuizAttempt(
        attemptId = "a1", quizId = "quiz1", title = "C# Fundamentals", status = "InProgress", startedAt = "2026-01-01T10:00:00Z",
        expiresAt = expiresAt, serverNow = serverNow, timeLimitSeconds = expiresAt?.let { 600 },
        questions = (0 until questions).map { i ->
            QuizQuestion("q$i", i, "Question $i", "Body $i", type = "SingleChoice", options = listOf(QuestionOption("q$i-a", "A", 0), QuestionOption("q$i-b", "B", 1)))
        },
    )

    fun quizResult(attemptId: String = "a1") = QuizResult(
        attemptId, "quiz1", "C# Fundamentals", "Submitted", 3, 2, 2, 0, 1, 100.0, 2.0, 3.0, 66.7, true, 60, 120, "2026-01-01T10:00:00Z",
    )

    fun challenge(id: String = "ch1") = ChallengeDetail(
        id = id, title = "Two Sum", slug = "two-sum", category = "Arrays", difficulty = "Easy", problemStatement = "Find two numbers.",
        inputDescription = "n", outputDescription = "indices", constraints = "n <= 10^4", hintCount = 2,
        starterCode = mapOf("python" to "def solve():\n    pass\n", "csharp" to "class Solution {}"), allowedLanguages = listOf("python", "csharp"),
    )

    fun submission(status: String = "Accepted", xp: Int = 10) = Submission("s1", "ch1", "Two Sum", "python", "Submit", status, 5, 5, 12, "2026-01-01T10:00:00Z", xpAwarded = xp)

    fun flashcard(id: String) = Flashcard(id, "Front $id", "Back $id", difficulty = "Beginner")
}

class FakeAuthRepository : AuthRepository {
    val user = MutableStateFlow<UserSummary?>(null)
    var loginResult: AppResult<UserSummary> = AppResult.Success(TestData.user())
    var registerResult: AppResult<RegisterResult> = AppResult.Success(RegisterResult(TestData.user(), emailVerificationRequired = true))
    var verifyResult: AppResult<UserSummary> = AppResult.Success(TestData.user())
    var changePasswordResult: AppResult<Unit> = AppResult.Success(Unit)
    val resentTo = mutableListOf<String>()
    val loginCalls = mutableListOf<Pair<String, String>>()
    var registeredForm: RegistrationForm? = null
    var loggedOut = false

    override val currentUser: Flow<UserSummary?> = user
    override suspend fun login(email: String, password: String) = loginResult.also { loginCalls += email to password; if (it is AppResult.Success) user.value = it.data }
    override suspend fun loginWithGoogle(idToken: String) = loginResult
    override suspend fun register(form: RegistrationForm) = registerResult.also { registeredForm = form }
    override suspend fun verifyEmail(email: String, code: String) = verifyResult
    override suspend fun resendVerification(email: String): AppResult<Unit> { resentTo += email; return AppResult.Success(Unit) }
    override suspend fun forgotPassword(email: String): AppResult<Unit> = AppResult.Success(Unit)
    override suspend fun resetPassword(email: String, code: String, newPassword: String): AppResult<Unit> = AppResult.Success(Unit)
    override suspend fun changePassword(current: String, newPassword: String) = changePasswordResult
    override suspend fun logout(allDevices: Boolean) { loggedOut = true; user.value = null }
    override suspend fun refreshUser(): AppResult<UserSummary> = user.value?.let { AppResult.Success(it) } ?: AppResult.Failure(AppError.Unauthorized())
}

class FakeSettingsRepository(onboarded: Boolean = true) : SettingsRepository {
    val theme = MutableStateFlow("system")
    val onboarded = MutableStateFlow(onboarded)
    val notifications = MutableStateFlow(true)
    override val themeMode: Flow<String> = theme
    override val onboardingCompleted: Flow<Boolean> = this.onboarded
    override val notificationsEnabled: Flow<Boolean> = notifications
    override suspend fun setThemeMode(mode: String) { theme.value = mode }
    override suspend fun setOnboardingCompleted() { onboarded.value = true }
    override suspend fun setNotificationsEnabled(enabled: Boolean) { notifications.value = enabled }
}

class FakeQuestionRepository : QuestionRepository {
    var technologiesResult: AppResult<List<Technology>> = AppResult.Success(listOf(Technology("t1", "C#", "csharp", questionCount = 10)))
    var questionResults: MutableMap<String, AppResult<QuestionDetail>> = mutableMapOf("q1" to AppResult.Success(TestData.question()))
    var attemptResult: (List<String>) -> AppResult<AttemptResult> = { sel ->
        AppResult.Success(AttemptResult(isCorrect = sel == listOf("o1"), correctOptionIds = listOf("o1"), xpAwarded = if (sel == listOf("o1")) 5 else 0, answer = TestData.answer()))
    }
    val attempts = mutableListOf<Triple<String, List<String>, Boolean?>>()
    var pages: MutableMap<Int, AppResult<Page<QuestionSummary>>> = mutableMapOf()
    val requestedFilters = mutableListOf<QuestionFilter>()

    override suspend fun technologies() = technologiesResult
    override suspend fun categories(technologyId: String): AppResult<List<Category>> = AppResult.Success(emptyList())
    override suspend fun questions(filter: QuestionFilter, page: Int, pageSize: Int): AppResult<Page<QuestionSummary>> {
        requestedFilters += filter
        return pages[page] ?: AppResult.Success(Page())
    }
    override suspend fun question(id: String) = questionResults[id] ?: AppResult.Failure(AppError.NotFound())
    override suspend fun random(technologyId: String?, difficulty: String?) = question("q1")
    override suspend fun answer(id: String): AppResult<QuestionAnswer> = AppResult.Success(TestData.answer(id))
    override suspend fun attempt(id: String, selectedOptionIds: List<String>, timeSpentSeconds: Int, selfAssessedCorrect: Boolean?): AppResult<AttemptResult> {
        attempts += Triple(id, selectedOptionIds, selfAssessedCorrect)
        return attemptResult(selectedOptionIds)
    }
    override suspend fun report(id: String, reason: String, details: String?): AppResult<Unit> = AppResult.Success(Unit)
    override suspend fun reportReasons(): AppResult<List<String>> = AppResult.Success(listOf("IncorrectAnswer", "Typo"))
}

/** In-memory offline library mirroring the Room-backed implementation's observable behaviour. */
class FakeLibraryRepository : LibraryRepository {
    val bookmarks = MutableStateFlow<Map<String, QuestionSummary>>(emptyMap())
    val notes = MutableStateFlow<Map<String, Note>>(emptyMap())
    override fun observeBookmarks(): Flow<List<QuestionSummary>> = bookmarks.map { it.values.toList() }
    override fun observeNotes(): Flow<List<Note>> = notes.map { it.values.toList() }
    override fun observeIsBookmarked(questionId: String): Flow<Boolean> = bookmarks.map { questionId in it }
    override fun observeNote(questionId: String): Flow<String?> = notes.map { it[questionId]?.content }
    override suspend fun setBookmark(question: QuestionSummary, bookmarked: Boolean) {
        bookmarks.value = if (bookmarked) bookmarks.value + (question.id to question) else bookmarks.value - question.id
    }
    override suspend fun saveNote(questionId: String, questionTitle: String, technology: String, content: String) {
        notes.value = notes.value + (questionId to Note(questionId, questionTitle, technology, content, "2026-01-01T00:00:00Z"))
    }
    override suspend fun deleteNote(questionId: String) { notes.value = notes.value - questionId }
    override suspend fun exportNotes(format: String): AppResult<String> = AppResult.Success("# Notes")
    override suspend fun collections(): AppResult<List<SavedCollection>> = AppResult.Success(emptyList())
    override suspend fun collection(id: String): AppResult<SavedCollectionDetail> = AppResult.Failure(AppError.NotFound())
    override suspend fun createCollection(name: String, description: String?): AppResult<SavedCollection> = AppResult.Success(SavedCollection("col1", name))
    override suspend fun deleteCollection(id: String): AppResult<Unit> = AppResult.Success(Unit)
    override suspend fun addToCollection(collectionId: String, itemType: String, itemId: String): AppResult<Unit> = AppResult.Success(Unit)
    override suspend fun removeFromCollection(collectionId: String, itemId: String): AppResult<Unit> = AppResult.Success(Unit)
}

class FakeQuizRepository : QuizRepository {
    var startResult: AppResult<QuizAttempt> = AppResult.Success(TestData.quizAttempt())
    var submitResult: AppResult<QuizResult> = AppResult.Success(TestData.quizResult())
    val saved = mutableListOf<QuizAnswer>()
    val submitted = mutableListOf<List<QuizAnswer>>()
    override suspend fun quizzes(mode: String?, technologyId: String?, page: Int): AppResult<Page<QuizSummary>> = AppResult.Success(Page())
    override suspend fun daily(): AppResult<QuizSummary> = AppResult.Failure(AppError.NotFound())
    override suspend fun create(request: CreateQuiz): AppResult<QuizSummary> = AppResult.Success(QuizSummary("quiz1", "Custom", mode = request.mode, questionCount = request.questionCount))
    override suspend fun start(quizId: String) = startResult
    override suspend fun saveAnswer(attemptId: String, answer: QuizAnswer): AppResult<Unit> { saved += answer; return AppResult.Success(Unit) }
    override suspend fun submit(quizId: String, attemptId: String, answers: List<QuizAnswer>) = submitResult.also { submitted += answers }
    override suspend fun result(attemptId: String): AppResult<QuizResult> = AppResult.Success(TestData.quizResult(attemptId))
    override suspend fun history(page: Int): AppResult<Page<QuizAttemptSummary>> = AppResult.Success(Page())
}

class FakeCodingRepository : CodingRepository {
    var submitResults = ArrayDeque<AppResult<Submission>>()
    val submitKeys = mutableListOf<String>()
    val drafts = mutableMapOf<String, String>()
    var runResult: AppResult<RunResult> = AppResult.Success(RunResult("Accepted", passedCount = 2, totalCount = 2))
    override suspend fun languages(): AppResult<List<Language>> = AppResult.Success(listOf(Language("python", "Python", "3.12"), Language("csharp", "C#", "12")))
    override suspend fun challenges(category: String?, difficulty: String?, status: String?, search: String?, page: Int): AppResult<Page<ChallengeSummary>> = AppResult.Success(Page())
    override suspend fun challenge(id: String): AppResult<ChallengeDetail> = AppResult.Success(TestData.challenge(id))
    override suspend fun hint(id: String, index: Int): AppResult<Hint> = AppResult.Success(Hint(index, 2, "Hint ${index + 1}"))
    override suspend fun solution(id: String): AppResult<Solution> = AppResult.Success(Solution(id, "Use a hash map."))
    override suspend fun run(challengeId: String, language: String, source: String, customInput: String?) = runResult
    override suspend fun submit(challengeId: String, language: String, source: String, idempotencyKey: String): AppResult<Submission> {
        submitKeys += idempotencyKey
        return submitResults.removeFirstOrNull() ?: AppResult.Success(TestData.submission())
    }
    override suspend fun submissions(challengeId: String?, page: Int): AppResult<Page<Submission>> = AppResult.Success(Page())
    override suspend fun submission(id: String): AppResult<Submission> = AppResult.Success(TestData.submission())
    override suspend fun draft(challengeId: String, language: String): String? = drafts["$challengeId/$language"]
    override suspend fun saveDraft(challengeId: String, language: String, code: String) { drafts["$challengeId/$language"] = code }
}

class FakeFlashcardRepository(private val cards: List<Flashcard>) : FlashcardRepository {
    val reviews = mutableListOf<Pair<String, FlashcardGrade>>()
    val bookmarks = mutableMapOf<String, Boolean>()
    override suspend fun decks(): AppResult<List<FlashcardDeck>> = AppResult.Success(listOf(FlashcardDeck("t1", "C#", cards.size, cards.size, 0, 0)))
    override suspend fun session(technologyId: String?, dueOnly: Boolean, bookmarkedOnly: Boolean): AppResult<List<Flashcard>> = AppResult.Success(if (dueOnly) cards else cards + TestData.flashcard("extra"))
    override suspend fun review(card: Flashcard, grade: FlashcardGrade) { reviews += card.id to grade }
    override suspend fun setBookmark(cardId: String, bookmarked: Boolean): AppResult<Unit> { bookmarks[cardId] = bookmarked; return AppResult.Success(Unit) }
}

class FakeSyncRepository(initial: SyncState = SyncState(isOnline = true, pendingChanges = 0, lastSyncedAt = null)) : SyncRepository {
    val stateFlow = MutableStateFlow(initial)
    var syncCalls = 0
    var scheduled = 0
    override val state: Flow<SyncState> = stateFlow
    override suspend fun sync(): AppResult<Unit> { syncCalls++; return AppResult.Success(Unit) }
    override fun scheduleSync() { scheduled++ }
}

class FakeDashboardRepository : DashboardRepository {
    var searchResult: (String, String?) -> AppResult<SearchResults> = { q, _ -> AppResult.Success(SearchResults(q, Page(listOf(SearchHit("question", "q1", "Result for $q"))))) }
    val searches = mutableListOf<String>()
    override suspend fun summary(): AppResult<DashboardSummary> = AppResult.Failure(AppError.Network())
    override suspend fun activity(days: Int): AppResult<List<ActivityDay>> = AppResult.Success(emptyList())
    override suspend fun skills(): AppResult<List<SkillProgress>> = AppResult.Success(emptyList())
    override suspend fun search(query: String, type: String?, page: Int): AppResult<SearchResults> { searches += query; return searchResult(query, type) }
}

fun <T> constantFlow(value: T): Flow<T> = flowOf(value)
