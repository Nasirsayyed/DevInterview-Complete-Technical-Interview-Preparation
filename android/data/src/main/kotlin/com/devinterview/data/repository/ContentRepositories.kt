package com.devinterview.data.repository

import com.devinterview.core.common.IoDispatcher
import com.devinterview.core.database.*
import com.devinterview.core.network.*
import com.devinterview.domain.model.*
import com.devinterview.domain.repository.*
import kotlinx.coroutines.CoroutineDispatcher
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.withContext
import java.time.Instant
import java.util.UUID
import javax.inject.Inject
import javax.inject.Singleton

private val json = NetworkJson

/** Falls back to a cached copy when the network call fails for connectivity reasons. */
internal suspend fun <T> networkWithCache(network: suspend () -> AppResult<T>, cache: suspend () -> T?, save: suspend (T) -> Unit): AppResult<T> {
    return when (val result = network()) {
        is AppResult.Success -> { save(result.data); result }
        is AppResult.Failure -> {
            if (result.error is AppError.Network || result.error is AppError.Timeout) cache()?.let { AppResult.Success(it) } ?: AppResult.Failure(AppError.Offline("You're offline and this item hasn't been downloaded yet."))
            else result
        }
    }
}

@Singleton
class QuestionRepositoryImpl @Inject constructor(
    private val api: ContentApi,
    private val questions: QuestionDao,
    @IoDispatcher private val io: CoroutineDispatcher,
) : QuestionRepository {
    override suspend fun technologies() = apiCall { api.technologies() }
    override suspend fun categories(technologyId: String) = apiCall { api.categories(technologyId) }

    override suspend fun questions(filter: QuestionFilter, page: Int, pageSize: Int): AppResult<Page<QuestionSummary>> {
        val query = buildMap {
            put("page", page.toString()); put("pageSize", pageSize.toString())
            filter.technologyId?.let { put("technologyId", it) }
            filter.categoryId?.let { put("categoryId", it) }
            filter.difficulty?.let { put("difficulty", it) }
            filter.type?.let { put("type", it) }
            filter.search?.takeIf { it.isNotBlank() }?.let { put("search", it) }
            filter.attemptFilter?.let { put("attemptFilter", it) }
            filter.sort?.let { put("sort", it) }
            if (filter.bookmarkedOnly) put("bookmarkedOnly", "true")
        }
        return apiCall { api.questions(query) }
    }

    override suspend fun question(id: String): AppResult<QuestionDetail> = withContext(io) {
        networkWithCache(
            network = { apiCall { api.question(id) } },
            cache = { questions.get(id)?.let { json.decodeFromString(QuestionDetail.serializer(), it.detailJson) } },
            save = { d ->
                val existing = questions.get(id)
                questions.upsert(CachedQuestionEntity(d.id, d.title, d.technology.name, d.difficulty, json.encodeToString(QuestionDetail.serializer(), d), existing?.answerJson, System.currentTimeMillis()))
            },
        )
    }

    override suspend fun random(technologyId: String?, difficulty: String?) = apiCall { api.random(technologyId, difficulty) }

    override suspend fun answer(id: String): AppResult<QuestionAnswer> = withContext(io) {
        networkWithCache(
            network = { apiCall { api.answer(id) } },
            cache = { questions.get(id)?.answerJson?.let { json.decodeFromString(QuestionAnswer.serializer(), it) } },
            save = { a -> questions.saveAnswer(id, json.encodeToString(QuestionAnswer.serializer(), a)) },
        )
    }

    override suspend fun attempt(id: String, selectedOptionIds: List<String>, timeSpentSeconds: Int, selfAssessedCorrect: Boolean?): AppResult<AttemptResult> {
        val result = apiCall { api.attempt(id, AttemptBody(selectedOptionIds, timeSpentSeconds, selfAssessedCorrect)) }
        if (result is AppResult.Success) withContext(io) { questions.saveAnswer(id, json.encodeToString(QuestionAnswer.serializer(), result.data.answer)) }
        return result
    }

    override suspend fun report(id: String, reason: String, details: String?) = apiUnit { api.report(id, ReportBody(reason, details)) }
    override suspend fun reportReasons() = apiCall { api.reportReasons() }
}

/**
 * Bookmarks and notes are written to Room first (instant, offline-capable) and flagged pendingSync.
 * [SyncRepositoryImpl] pushes them; the server resolves conflicts with last-writer-wins on client timestamps.
 */
@Singleton
class LibraryRepositoryImpl @Inject constructor(
    private val bookmarks: BookmarkDao,
    private val notes: NoteDao,
    private val api: ContentApi,
    private val sync: dagger.Lazy<SyncRepository>,
    @IoDispatcher private val io: CoroutineDispatcher,
) : LibraryRepository {
    override fun observeBookmarks(): Flow<List<QuestionSummary>> = bookmarks.observeActive().map { list ->
        list.mapNotNull { runCatching { json.decodeFromString(QuestionSummary.serializer(), it.summaryJson).copy(isBookmarked = true) }.getOrNull() }
    }

    override fun observeNotes(): Flow<List<Note>> = notes.observeActive().map { list ->
        list.map { Note(it.questionId, it.questionTitle, it.technology, it.content, Instant.ofEpochMilli(it.updatedAt).toString()) }
    }

    override fun observeIsBookmarked(questionId: String): Flow<Boolean> = bookmarks.observe(questionId).map { it == true }
    override fun observeNote(questionId: String): Flow<String?> = notes.observe(questionId).map { if (it == null || it.isDeleted) null else it.content }

    override suspend fun setBookmark(question: QuestionSummary, bookmarked: Boolean) = withContext(io) {
        bookmarks.upsert(BookmarkEntity(question.id, json.encodeToString(QuestionSummary.serializer(), question), bookmarked, System.currentTimeMillis(), pendingSync = true))
        sync.get().scheduleSync()
    }

    override suspend fun saveNote(questionId: String, questionTitle: String, technology: String, content: String) = withContext(io) {
        notes.upsert(NoteEntity(questionId, questionTitle, technology, content.trim(), isDeleted = content.isBlank(), updatedAt = System.currentTimeMillis(), pendingSync = true))
        sync.get().scheduleSync()
    }

    override suspend fun deleteNote(questionId: String) = withContext(io) {
        val existing = notes.get(questionId) ?: return@withContext
        notes.upsert(existing.copy(content = "", isDeleted = true, updatedAt = System.currentTimeMillis(), pendingSync = true))
        sync.get().scheduleSync()
    }

    override suspend fun exportNotes(format: String): AppResult<String> = try {
        AppResult.Success(api.exportNotes(format).string())
    } catch (e: java.io.IOException) {
        AppResult.Failure(AppError.Network())
    } catch (e: retrofit2.HttpException) {
        AppResult.Failure(AppError.Server("export_failed", "Could not export notes.", e.code()))
    }

    override suspend fun collections() = apiCall { api.collections() }
    override suspend fun collection(id: String) = apiCall { api.collection(id) }
    override suspend fun createCollection(name: String, description: String?) = apiCall { api.createCollection(CollectionBody(name, description)) }
    override suspend fun deleteCollection(id: String) = apiUnit { api.deleteCollection(id) }
    override suspend fun addToCollection(collectionId: String, itemType: String, itemId: String) = apiUnit { api.addItem(collectionId, CollectionItemBody(itemType, itemId)) }
    override suspend fun removeFromCollection(collectionId: String, itemId: String) = apiUnit { api.removeItem(collectionId, itemId) }
}

@Singleton
class CodingRepositoryImpl @Inject constructor(
    private val api: PracticeApi,
    private val dao: ChallengeDao,
    @IoDispatcher private val io: CoroutineDispatcher,
) : CodingRepository {
    override suspend fun languages() = apiCall { api.languages() }

    override suspend fun challenges(category: String?, difficulty: String?, status: String?, search: String?, page: Int): AppResult<Page<ChallengeSummary>> {
        val query = buildMap {
            put("page", page.toString()); put("pageSize", "30")
            category?.let { put("category", it) }; difficulty?.let { put("difficulty", it) }
            status?.let { put("status", it) }; search?.takeIf { it.isNotBlank() }?.let { put("search", it) }
        }
        return apiCall { api.challenges(query) }
    }

    override suspend fun challenge(id: String): AppResult<ChallengeDetail> = withContext(io) {
        networkWithCache(
            network = { apiCall { api.challenge(id) } },
            cache = { dao.get(id)?.let { json.decodeFromString(ChallengeDetail.serializer(), it.json) } },
            save = { dao.upsert(ChallengeEntity(it.id, it.title, json.encodeToString(ChallengeDetail.serializer(), it), System.currentTimeMillis())) },
        )
    }

    override suspend fun hint(id: String, index: Int) = apiCall { api.hint(id, index) }
    override suspend fun solution(id: String) = apiCall { api.solution(id) }
    override suspend fun run(challengeId: String, language: String, source: String, customInput: String?) = apiCall { api.run(RunBody(challengeId, language, source, customInput)) }
    override suspend fun submit(challengeId: String, language: String, source: String, idempotencyKey: String) =
        apiCall { api.submit(SubmitCodeBody(challengeId, language, source, idempotencyKey)) }
    override suspend fun submissions(challengeId: String?, page: Int) = apiCall { api.submissions(challengeId, page) }
    override suspend fun submission(id: String) = apiCall { api.submission(id) }
    override suspend fun draft(challengeId: String, language: String): String? = withContext(io) { dao.draft(challengeId, language)?.code }
    override suspend fun saveDraft(challengeId: String, language: String, code: String) = withContext(io) {
        dao.saveDraft(CodeDraftEntity(challengeId, language, code, System.currentTimeMillis()))
    }
}

@Singleton
class LearningRepositoryImpl @Inject constructor(
    private val api: EngagementApi,
    private val content: ContentApi,
    private val dao: LearningDao,
    @IoDispatcher private val io: CoroutineDispatcher,
) : LearningRepository {
    override suspend fun paths() = apiCall { api.paths() }

    override suspend fun path(id: String): AppResult<LearningPathDetail> = withContext(io) {
        networkWithCache(
            network = { apiCall { api.path(id) } },
            cache = { dao.path(id)?.let { json.decodeFromString(LearningPathDetail.serializer(), it.json) } },
            save = { d -> if (dao.path(id) != null) dao.upsertPath(PathEntity(id, json.encodeToString(LearningPathDetail.serializer(), d), System.currentTimeMillis())) },
        )
    }

    override suspend fun lesson(id: String): AppResult<Lesson> = withContext(io) {
        networkWithCache(
            network = { apiCall { api.lesson(id) } },
            cache = { dao.lesson(id)?.let { json.decodeFromString(Lesson.serializer(), it.json) } },
            save = { l -> dao.upsertLesson(LessonEntity(l.id, l.pathId, json.encodeToString(Lesson.serializer(), l), System.currentTimeMillis())) },
        )
    }

    override suspend fun updateProgress(lessonId: String, status: String, timeSpentSeconds: Int) = apiCall { api.progress(ProgressBody(lessonId, status, timeSpentSeconds)) }

    override suspend fun downloadPath(id: String): AppResult<Int> = withContext(io) {
        val detail = when (val r = apiCall { api.path(id) }) { is AppResult.Success -> r.data; is AppResult.Failure -> return@withContext r }
        val lessons = mutableListOf<LessonEntity>()
        for (summary in detail.modules.flatMap { it.lessons }) {
            when (val l = apiCall { api.lesson(summary.id) }) {
                is AppResult.Success -> lessons += LessonEntity(l.data.id, id, json.encodeToString(Lesson.serializer(), l.data), System.currentTimeMillis())
                is AppResult.Failure -> return@withContext l
            }
        }
        dao.upsertLessons(lessons)
        dao.upsertPath(PathEntity(id, json.encodeToString(LearningPathDetail.serializer(), detail), System.currentTimeMillis()))
        AppResult.Success(lessons.size)
    }

    override fun observeDownloadedPathIds(): Flow<Set<String>> = dao.observeDownloadedIds().map { it.toSet() }
    override suspend fun companies() = apiCall { content.companies() }
    override suspend fun company(id: String) = apiCall { content.company(id) }
    override suspend fun companyCollection(id: String) = apiCall { content.companyCollection(id) }
}

/**
 * Flashcards work offline: sessions are cached, reviews are queued locally (idempotent client ids) and replayed by the
 * sync worker. Scheduling (SM-2) is computed only by the server; offline-reviewed cards are hidden until it reschedules them.
 */
@Singleton
class FlashcardRepositoryImpl @Inject constructor(
    private val api: PracticeApi,
    private val dao: FlashcardDao,
    private val sync: dagger.Lazy<SyncRepository>,
    @IoDispatcher private val io: CoroutineDispatcher,
) : FlashcardRepository {
    override suspend fun decks() = apiCall { api.decks() }

    override suspend fun session(technologyId: String?, dueOnly: Boolean, bookmarkedOnly: Boolean): AppResult<List<Flashcard>> = withContext(io) {
        when (val r = apiCall { api.flashcards(technologyId, dueOnly, bookmarkedOnly) }) {
            is AppResult.Success -> {
                dao.upsertAll(r.data.map { FlashcardEntity(it.id, it.technology?.id, json.encodeToString(Flashcard.serializer(), it), System.currentTimeMillis(), null) })
                r
            }
            is AppResult.Failure -> if (r.error is AppError.Network || r.error is AppError.Timeout) {
                AppResult.Success(dao.unreviewed(technologyId, 30).map { json.decodeFromString(Flashcard.serializer(), it.json) })
            } else r
        }
    }

    override suspend fun review(card: Flashcard, grade: FlashcardGrade) = withContext(io) {
        dao.enqueue(PendingReviewEntity(UUID.randomUUID().toString(), card.id, grade.name, Instant.now().toString()))
        dao.markReviewed(card.id, System.currentTimeMillis())
        sync.get().scheduleSync()
    }

    override suspend fun setBookmark(cardId: String, bookmarked: Boolean) = apiUnit { api.bookmarkCard(cardId, BookmarkCardBody(bookmarked)) }
}
