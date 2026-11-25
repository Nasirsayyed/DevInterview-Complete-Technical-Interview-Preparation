package com.devinterview.data

import android.content.Context
import androidx.datastore.preferences.core.PreferenceDataStoreFactory
import androidx.room.Room
import androidx.test.core.app.ApplicationProvider
import androidx.work.testing.WorkManagerTestInitHelper
import com.devinterview.core.common.NetworkMonitor
import com.devinterview.core.database.DevInterviewDatabase
import com.devinterview.core.datastore.SessionStore
import com.devinterview.core.datastore.SettingsStore
import com.devinterview.core.datastore.TokenCipher
import com.devinterview.core.network.*
import com.devinterview.data.repository.LibraryRepositoryImpl
import com.devinterview.data.sync.SyncRepositoryImpl
import com.devinterview.domain.model.*
import com.google.common.truth.Truth.assertThat
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.runBlocking
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.jsonArray
import kotlinx.serialization.json.jsonObject
import kotlinx.serialization.json.jsonPrimitive
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import org.junit.After
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import retrofit2.Retrofit
import retrofit2.converter.kotlinx.serialization.asConverterFactory
import java.io.File
import java.util.UUID

/** Offline-first behaviour of bookmarks/notes against a real in-memory Room database and a mock server. */
@RunWith(RobolectricTestRunner::class)
class OfflineSyncTest {
    private val context = ApplicationProvider.getApplicationContext<Context>()
    private val server = MockWebServer()
    private lateinit var db: DevInterviewDatabase
    private lateinit var sync: SyncRepositoryImpl
    private lateinit var library: LibraryRepositoryImpl
    private lateinit var session: SessionStore
    private val online = MutableStateFlow(true)

    private object PlainCipher : TokenCipher {
        override fun encrypt(plain: String) = plain
        override fun decrypt(encoded: String) = encoded
    }

    private val question = QuestionSummary("11111111-1111-1111-1111-111111111111", "What is LINQ?", Ref("t", "C#"), Ref("c", "LINQ"), "Beginner", "SingleChoice")

    @Before
    fun setUp() {
        WorkManagerTestInitHelper.initializeTestWorkManager(context)
        server.start()
        val retrofit = Retrofit.Builder().baseUrl(server.url("/"))
            .addConverterFactory(NetworkJson.asConverterFactory("application/json".toMediaType())).build()
        db = Room.inMemoryDatabaseBuilder(context, DevInterviewDatabase::class.java).allowMainThreadQueries().build()
        val dir = File(context.cacheDir, UUID.randomUUID().toString())
        session = SessionStore(PreferenceDataStoreFactory.create { File(dir, "session.preferences_pb") }, PlainCipher)
        val settings = SettingsStore(PreferenceDataStoreFactory.create { File(dir, "settings.preferences_pb") })
        val monitor = object : NetworkMonitor { override val isOnline = online }
        sync = SyncRepositoryImpl(context, retrofit.create(EngagementApi::class.java), retrofit.create(ContentApi::class.java),
            db.bookmarks(), db.notes(), db.flashcards(), session, settings, monitor)
        library = LibraryRepositoryImpl(db.bookmarks(), db.notes(), retrofit.create(ContentApi::class.java), { sync }, Dispatchers.Unconfined)
        runBlocking {
            session.save(AuthSession("access", "2030-01-01T00:00:00Z", "refresh", "2030-01-01T00:00:00Z", UserSummary("u1", "dev@example.com", "Ada", "L")))
        }
    }

    @After
    fun tearDown() { db.close(); server.shutdown() }

    private fun ok(data: String) = MockResponse().setHeader("Content-Type", "application/json").setBody("""{"success":true,"data":$data}""")
    private val emptyPull = """{"bookmarks":[],"notes":[],"serverTime":"2026-01-01T00:00:00Z"}"""
    private val pushResult = """{"appliedBookmarks":1,"appliedNotes":1,"appliedReviews":0,"conflicts":[],"serverTime":"2026-01-01T00:00:00Z"}"""

    @Test
    fun `offline bookmark is visible immediately and counted as pending`() = runBlocking {
        online.value = false
        library.setBookmark(question, true)
        assertThat(library.observeBookmarks().first().map { it.id }).containsExactly(question.id)
        assertThat(library.observeIsBookmarked(question.id).first()).isTrue()
        val state = sync.state.first()
        assertThat(state.isOnline).isFalse()
        assertThat(state.pendingChanges).isEqualTo(1)
    }

    @Test
    fun `sync pushes pending changes and clears the pending flag`() = runBlocking {
        library.setBookmark(question, true)
        library.saveNote(question.id, question.title, "C#", "Deferred execution!")
        server.enqueue(ok(pushResult))
        server.enqueue(ok(emptyPull))

        assertThat(sync.sync()).isInstanceOf(AppResult.Success::class.java)

        val push = server.takeRequest()
        assertThat(push.path).isEqualTo("/api/v1/sync/push")
        val body = Json.parseToJsonElement(push.body.readUtf8()).jsonObject
        assertThat(body["bookmarks"]!!.jsonArray.single().jsonObject["questionId"]!!.jsonPrimitive.content).isEqualTo(question.id)
        assertThat(body["notes"]!!.jsonArray.single().jsonObject["content"]!!.jsonPrimitive.content).isEqualTo("Deferred execution!")
        assertThat(server.takeRequest().path).startsWith("/api/v1/sync/pull")
        assertThat(sync.state.first().pendingChanges).isEqualTo(0)
    }

    @Test
    fun `failed push keeps changes pending for the next attempt`() = runBlocking {
        library.setBookmark(question, true)
        server.enqueue(MockResponse().setResponseCode(500).setBody("""{"success":false,"error":{"code":"internal_error","message":"boom"}}"""))
        val result = sync.sync()
        assertThat(result).isInstanceOf(AppResult.Failure::class.java)
        assertThat(sync.state.first().pendingChanges).isEqualTo(1)
    }

    @Test
    fun `server removal applies locally unless the local copy is still pending`() = runBlocking {
        library.setBookmark(question, true)
        server.enqueue(ok(pushResult)); server.enqueue(ok(emptyPull))
        sync.sync()

        // Removed on another device.
        server.enqueue(ok("""{"bookmarks":[{"questionId":"${question.id}","isBookmarked":false,"updatedAt":"2026-01-02T00:00:00Z"}],"notes":[],"serverTime":"2026-01-02T00:00:00Z"}"""))
        sync.sync()
        assertThat(library.observeIsBookmarked(question.id).first()).isFalse()

        // Re-bookmarked locally while offline; a stale server removal must not overwrite it before it is pushed.
        library.setBookmark(question, true)
        server.enqueue(MockResponse().setResponseCode(503))
        sync.sync()
        assertThat(library.observeIsBookmarked(question.id).first()).isTrue()
    }

    @Test
    fun `deleted note disappears and is synced as a deletion`() = runBlocking {
        library.saveNote(question.id, question.title, "C#", "temp")
        library.deleteNote(question.id)
        assertThat(library.observeNote(question.id).first()).isNull()
        server.enqueue(ok(pushResult)); server.enqueue(ok(emptyPull))
        sync.sync()
        val body = Json.parseToJsonElement(server.takeRequest().body.readUtf8()).jsonObject
        assertThat(body["notes"]!!.jsonArray.single().jsonObject["isDeleted"]!!.jsonPrimitive.content).isEqualTo("true")
    }

    @Test
    fun `nothing is sent when signed out`() = runBlocking {
        session.clear()
        library.setBookmark(question, true)
        assertThat(sync.sync()).isInstanceOf(AppResult.Success::class.java)
        assertThat(server.requestCount).isEqualTo(0)
    }
}
