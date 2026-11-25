package com.devinterview.core.database

import android.content.Context
import androidx.room.*
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.android.qualifiers.ApplicationContext
import dagger.hilt.components.SingletonComponent
import kotlinx.coroutines.flow.Flow
import javax.inject.Singleton

/** Cached question detail (JSON payload of QuestionDetail + answer when revealed) for offline reading. */
@Entity(tableName = "questions")
data class CachedQuestionEntity(
    @PrimaryKey val id: String,
    val title: String,
    val technology: String,
    val difficulty: String,
    val detailJson: String,
    val answerJson: String?,
    val cachedAt: Long,
)

/** Local bookmark state; [pendingSync] marks changes not yet pushed to the server. */
@Entity(tableName = "bookmarks")
data class BookmarkEntity(
    @PrimaryKey val questionId: String,
    val summaryJson: String,
    val isBookmarked: Boolean,
    val updatedAt: Long,
    val pendingSync: Boolean,
)

@Entity(tableName = "notes")
data class NoteEntity(
    @PrimaryKey val questionId: String,
    val questionTitle: String,
    val technology: String,
    val content: String,
    val isDeleted: Boolean,
    val updatedAt: Long,
    val pendingSync: Boolean,
)

@Entity(tableName = "flashcards", indices = [Index("technologyId")])
data class FlashcardEntity(
    @PrimaryKey val id: String,
    val technologyId: String?,
    val json: String,
    val cachedAt: Long,
    /** Set when the card was reviewed locally; reviewed cards are hidden from offline sessions until the server reschedules them. */
    val reviewedLocallyAt: Long?,
)

/** Append-only queue of reviews done while offline (idempotent by clientReviewId on the server). */
@Entity(tableName = "pending_reviews")
data class PendingReviewEntity(
    @PrimaryKey val clientReviewId: String,
    val flashcardId: String,
    val grade: String,
    val reviewedAt: String,
)

@Entity(tableName = "lessons", indices = [Index("pathId")])
data class LessonEntity(@PrimaryKey val id: String, val pathId: String, val json: String, val cachedAt: Long)

@Entity(tableName = "paths")
data class PathEntity(@PrimaryKey val id: String, val json: String, val downloadedAt: Long)

@Entity(tableName = "challenges")
data class ChallengeEntity(@PrimaryKey val id: String, val title: String, val json: String, val cachedAt: Long)

@Entity(tableName = "code_drafts", primaryKeys = ["challengeId", "language"])
data class CodeDraftEntity(val challengeId: String, val language: String, val code: String, val updatedAt: Long)

@Dao
interface QuestionDao {
    @Upsert suspend fun upsert(entity: CachedQuestionEntity)
    @Query("SELECT * FROM questions WHERE id = :id") suspend fun get(id: String): CachedQuestionEntity?
    @Query("UPDATE questions SET answerJson = :answerJson WHERE id = :id") suspend fun saveAnswer(id: String, answerJson: String)
    @Query("DELETE FROM questions WHERE cachedAt < :before AND id NOT IN (SELECT questionId FROM bookmarks WHERE isBookmarked = 1)")
    suspend fun prune(before: Long)
}

@Dao
interface BookmarkDao {
    @Query("SELECT * FROM bookmarks WHERE isBookmarked = 1 ORDER BY updatedAt DESC") fun observeActive(): Flow<List<BookmarkEntity>>
    @Query("SELECT isBookmarked FROM bookmarks WHERE questionId = :id") fun observe(id: String): Flow<Boolean?>
    @Query("SELECT * FROM bookmarks WHERE questionId = :id") suspend fun get(id: String): BookmarkEntity?
    @Upsert suspend fun upsert(entity: BookmarkEntity)
    @Query("SELECT * FROM bookmarks WHERE pendingSync = 1") suspend fun pending(): List<BookmarkEntity>
    @Query("UPDATE bookmarks SET pendingSync = 0 WHERE questionId = :id AND updatedAt = :updatedAt") suspend fun markSynced(id: String, updatedAt: Long)
    @Query("SELECT COUNT(*) FROM bookmarks WHERE pendingSync = 1") fun observePendingCount(): Flow<Int>
}

@Dao
interface NoteDao {
    @Query("SELECT * FROM notes WHERE isDeleted = 0 ORDER BY updatedAt DESC") fun observeActive(): Flow<List<NoteEntity>>
    @Query("SELECT * FROM notes WHERE questionId = :id") fun observe(id: String): Flow<NoteEntity?>
    @Query("SELECT * FROM notes WHERE questionId = :id") suspend fun get(id: String): NoteEntity?
    @Upsert suspend fun upsert(entity: NoteEntity)
    @Query("SELECT * FROM notes WHERE pendingSync = 1") suspend fun pending(): List<NoteEntity>
    @Query("UPDATE notes SET pendingSync = 0 WHERE questionId = :id AND updatedAt = :updatedAt") suspend fun markSynced(id: String, updatedAt: Long)
    @Query("SELECT COUNT(*) FROM notes WHERE pendingSync = 1") fun observePendingCount(): Flow<Int>
}

@Dao
interface FlashcardDao {
    @Upsert suspend fun upsertAll(cards: List<FlashcardEntity>)
    @Query("SELECT * FROM flashcards WHERE (:technologyId IS NULL OR technologyId = :technologyId) AND reviewedLocallyAt IS NULL ORDER BY cachedAt LIMIT :limit")
    suspend fun unreviewed(technologyId: String?, limit: Int): List<FlashcardEntity>
    @Query("UPDATE flashcards SET reviewedLocallyAt = :at WHERE id = :id") suspend fun markReviewed(id: String, at: Long)
    @Query("UPDATE flashcards SET reviewedLocallyAt = NULL WHERE id IN (:ids)") suspend fun clearReviewed(ids: List<String>)
    @Insert(onConflict = OnConflictStrategy.IGNORE) suspend fun enqueue(review: PendingReviewEntity)
    @Query("SELECT * FROM pending_reviews ORDER BY reviewedAt") suspend fun pendingReviews(): List<PendingReviewEntity>
    @Query("DELETE FROM pending_reviews WHERE clientReviewId IN (:ids)") suspend fun removeReviews(ids: List<String>)
    @Query("SELECT COUNT(*) FROM pending_reviews") fun observePendingCount(): Flow<Int>
}

@Dao
interface LearningDao {
    @Upsert suspend fun upsertLesson(lesson: LessonEntity)
    @Upsert suspend fun upsertLessons(lessons: List<LessonEntity>)
    @Query("SELECT * FROM lessons WHERE id = :id") suspend fun lesson(id: String): LessonEntity?
    @Upsert suspend fun upsertPath(path: PathEntity)
    @Query("SELECT * FROM paths WHERE id = :id") suspend fun path(id: String): PathEntity?
    @Query("SELECT id FROM paths") fun observeDownloadedIds(): Flow<List<String>>
}

@Dao
interface ChallengeDao {
    @Upsert suspend fun upsert(entity: ChallengeEntity)
    @Query("SELECT * FROM challenges WHERE id = :id") suspend fun get(id: String): ChallengeEntity?
    @Upsert suspend fun saveDraft(draft: CodeDraftEntity)
    @Query("SELECT * FROM code_drafts WHERE challengeId = :challengeId AND language = :language") suspend fun draft(challengeId: String, language: String): CodeDraftEntity?
}

@Database(
    entities = [CachedQuestionEntity::class, BookmarkEntity::class, NoteEntity::class, FlashcardEntity::class, PendingReviewEntity::class,
        LessonEntity::class, PathEntity::class, ChallengeEntity::class, CodeDraftEntity::class],
    version = 1,
    exportSchema = true,
)
abstract class DevInterviewDatabase : RoomDatabase() {
    abstract fun questions(): QuestionDao
    abstract fun bookmarks(): BookmarkDao
    abstract fun notes(): NoteDao
    abstract fun flashcards(): FlashcardDao
    abstract fun learning(): LearningDao
    abstract fun challenges(): ChallengeDao
}

@Module
@InstallIn(SingletonComponent::class)
object DatabaseModule {
    @Provides @Singleton
    fun database(@ApplicationContext context: Context): DevInterviewDatabase =
        Room.databaseBuilder(context, DevInterviewDatabase::class.java, "devinterview.db").build()

    @Provides fun questionDao(db: DevInterviewDatabase) = db.questions()
    @Provides fun bookmarkDao(db: DevInterviewDatabase) = db.bookmarks()
    @Provides fun noteDao(db: DevInterviewDatabase) = db.notes()
    @Provides fun flashcardDao(db: DevInterviewDatabase) = db.flashcards()
    @Provides fun learningDao(db: DevInterviewDatabase) = db.learning()
    @Provides fun challengeDao(db: DevInterviewDatabase) = db.challenges()
}
