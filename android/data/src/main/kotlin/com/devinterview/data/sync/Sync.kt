package com.devinterview.data.sync

import android.content.Context
import androidx.hilt.work.HiltWorker
import androidx.work.*
import com.devinterview.core.common.NetworkMonitor
import com.devinterview.core.database.*
import com.devinterview.core.datastore.SessionStore
import com.devinterview.core.datastore.SettingsStore
import com.devinterview.core.network.*
import com.devinterview.domain.model.AppResult
import com.devinterview.domain.model.QuestionSummary
import com.devinterview.domain.model.SyncState
import com.devinterview.domain.repository.SyncRepository
import dagger.assisted.Assisted
import dagger.assisted.AssistedInject
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.combine
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import java.time.Instant
import java.util.concurrent.TimeUnit
import javax.inject.Inject
import javax.inject.Singleton

/**
 * Offline synchronisation.
 *
 * Push: pending bookmarks/notes (last-writer-wins by client timestamp on the server) and queued flashcard reviews
 * (append-only, idempotent by clientReviewId). Items edited again while a push is in flight keep their pending flag,
 * because rows are only marked synced if their updatedAt still matches what was sent.
 *
 * Pull: server changes since the last cursor. Local rows that are still pending win locally until they are pushed;
 * the server makes the final decision on the next push.
 */
@Singleton
class SyncRepositoryImpl @Inject constructor(
    @ApplicationContext private val context: Context,
    private val api: EngagementApi,
    private val content: ContentApi,
    private val bookmarks: BookmarkDao,
    private val notes: NoteDao,
    private val flashcards: FlashcardDao,
    private val session: SessionStore,
    private val settings: SettingsStore,
    network: NetworkMonitor,
) : SyncRepository {
    private val mutex = Mutex()

    override val state: Flow<SyncState> = combine(
        network.isOnline, bookmarks.observePendingCount(), notes.observePendingCount(), flashcards.observePendingCount(), settings.lastSyncedAt,
    ) { online, b, n, r, last -> SyncState(online, b + n + r, last) }

    override suspend fun sync(): AppResult<Unit> = mutex.withLock {
        if (session.current() == null) return AppResult.Success(Unit)
        val pendingBookmarks = bookmarks.pending()
        val pendingNotes = notes.pending()
        val pendingReviews = flashcards.pendingReviews()

        if (pendingBookmarks.isNotEmpty() || pendingNotes.isNotEmpty() || pendingReviews.isNotEmpty()) {
            val body = SyncPushBody(
                bookmarks = pendingBookmarks.map { BookmarkChange(it.questionId, it.isBookmarked, Instant.ofEpochMilli(it.updatedAt).toString()) },
                notes = pendingNotes.map { NoteChange(it.questionId, it.content, it.isDeleted, Instant.ofEpochMilli(it.updatedAt).toString()) },
                flashcardReviews = pendingReviews.map { ReviewCardBody(it.flashcardId, it.grade, it.clientReviewId, it.reviewedAt) },
            )
            when (val pushed = apiCall { api.push(body) }) {
                is AppResult.Failure -> return pushed
                is AppResult.Success -> {
                    pendingBookmarks.forEach { bookmarks.markSynced(it.questionId, it.updatedAt) }
                    pendingNotes.forEach { notes.markSynced(it.questionId, it.updatedAt) }
                    flashcards.removeReviews(pendingReviews.map { it.clientReviewId })
                }
            }
        }

        val cursor = settings.lastPullCursor.first()
        val pulled = when (val r = apiCall { api.pull(cursor) }) { is AppResult.Success -> r.data; is AppResult.Failure -> return r }
        applyPull(pulled)
        settings.setSynced(System.currentTimeMillis(), pulled.serverTime)
        AppResult.Success(Unit)
    }

    private suspend fun applyPull(pulled: SyncPullResult) {
        // Removed bookmarks/notes can be applied directly; added ones need display data, fetched from list endpoints.
        val newBookmarks = pulled.bookmarks.filter { it.isBookmarked && bookmarks.get(it.questionId)?.pendingSync != true }
        if (newBookmarks.isNotEmpty()) {
            val page = apiCall { content.questions(mapOf("bookmarkedOnly" to "true", "pageSize" to "100")) }
            if (page is AppResult.Success) page.data.items.forEach { summary ->
                if (bookmarks.get(summary.id)?.pendingSync != true)
                    bookmarks.upsert(BookmarkEntity(summary.id, NetworkJson.encodeToString(QuestionSummary.serializer(), summary), true, System.currentTimeMillis(), false))
            }
        }
        pulled.bookmarks.filter { !it.isBookmarked }.forEach { state ->
            val local = bookmarks.get(state.questionId)
            if (local != null && !local.pendingSync) bookmarks.upsert(local.copy(isBookmarked = false, pendingSync = false))
        }
        if (pulled.notes.any { !it.isDeleted }) {
            val page = apiCall { content.notes(1) }
            if (page is AppResult.Success) page.data.items.forEach { n ->
                if (notes.get(n.questionId)?.pendingSync != true)
                    notes.upsert(NoteEntity(n.questionId, n.questionTitle, n.technology, n.content, false, System.currentTimeMillis(), false))
            }
        }
        pulled.notes.filter { it.isDeleted }.forEach { state ->
            val local = notes.get(state.questionId)
            if (local != null && !local.pendingSync) notes.upsert(local.copy(isDeleted = true, content = "", pendingSync = false))
        }
    }

    override fun scheduleSync() {
        val request = OneTimeWorkRequestBuilder<SyncWorker>()
            .setConstraints(Constraints.Builder().setRequiredNetworkType(NetworkType.CONNECTED).build())
            .setBackoffCriteria(BackoffPolicy.EXPONENTIAL, 30, TimeUnit.SECONDS)
            .build()
        WorkManager.getInstance(context).enqueueUniqueWork(SyncWorker.ONE_TIME, ExistingWorkPolicy.REPLACE, request)
    }

    fun schedulePeriodicSync() {
        val request = PeriodicWorkRequestBuilder<SyncWorker>(6, TimeUnit.HOURS)
            .setConstraints(Constraints.Builder().setRequiredNetworkType(NetworkType.CONNECTED).build())
            .build()
        WorkManager.getInstance(context).enqueueUniquePeriodicWork(SyncWorker.PERIODIC, ExistingPeriodicWorkPolicy.KEEP, request)
    }
}

@HiltWorker
class SyncWorker @AssistedInject constructor(
    @Assisted context: Context,
    @Assisted params: WorkerParameters,
    private val sync: SyncRepository,
) : CoroutineWorker(context, params) {
    override suspend fun doWork(): Result = when (sync.sync()) {
        is AppResult.Success -> Result.success()
        is AppResult.Failure -> if (runAttemptCount < 5) Result.retry() else Result.failure()
    }

    companion object {
        const val ONE_TIME = "devinterview-sync"
        const val PERIODIC = "devinterview-sync-periodic"
    }
}
