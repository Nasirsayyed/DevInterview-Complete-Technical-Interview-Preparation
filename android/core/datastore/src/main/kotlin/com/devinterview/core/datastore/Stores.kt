package com.devinterview.core.datastore

import android.content.Context
import android.security.keystore.KeyGenParameterSpec
import android.security.keystore.KeyProperties
import android.util.Base64
import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.core.booleanPreferencesKey
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.longPreferencesKey
import androidx.datastore.preferences.core.stringPreferencesKey
import androidx.datastore.preferences.preferencesDataStore
import com.devinterview.domain.model.AuthSession
import com.devinterview.domain.model.UserSummary
import dagger.Binds
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.android.qualifiers.ApplicationContext
import dagger.hilt.components.SingletonComponent
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.map
import kotlinx.serialization.json.Json
import java.security.KeyStore
import javax.crypto.Cipher
import javax.crypto.KeyGenerator
import javax.crypto.SecretKey
import javax.crypto.spec.GCMParameterSpec
import javax.inject.Inject
import javax.inject.Named
import javax.inject.Singleton

/** Encrypts secrets before they are written to disk. */
interface TokenCipher {
    fun encrypt(plain: String): String
    fun decrypt(encoded: String): String?
}

/** AES-256-GCM with a non-exportable key held in the Android Keystore. */
class KeystoreTokenCipher @Inject constructor() : TokenCipher {
    private val alias = "devinterview_session_key"

    private fun key(): SecretKey {
        val store = KeyStore.getInstance("AndroidKeyStore").apply { load(null) }
        (store.getEntry(alias, null) as? KeyStore.SecretKeyEntry)?.let { return it.secretKey }
        val generator = KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES, "AndroidKeyStore")
        generator.init(
            KeyGenParameterSpec.Builder(alias, KeyProperties.PURPOSE_ENCRYPT or KeyProperties.PURPOSE_DECRYPT)
                .setBlockModes(KeyProperties.BLOCK_MODE_GCM).setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE)
                .setKeySize(256).build()
        )
        return generator.generateKey()
    }

    override fun encrypt(plain: String): String {
        val cipher = Cipher.getInstance("AES/GCM/NoPadding").apply { init(Cipher.ENCRYPT_MODE, key()) }
        val data = cipher.doFinal(plain.toByteArray(Charsets.UTF_8))
        return Base64.encodeToString(cipher.iv + data, Base64.NO_WRAP)
    }

    override fun decrypt(encoded: String): String? = runCatching {
        val bytes = Base64.decode(encoded, Base64.NO_WRAP)
        val cipher = Cipher.getInstance("AES/GCM/NoPadding").apply { init(Cipher.DECRYPT_MODE, key(), GCMParameterSpec(128, bytes, 0, 12)) }
        String(cipher.doFinal(bytes, 12, bytes.size - 12), Charsets.UTF_8)
    }.getOrNull()
}

private val Context.sessionDataStore by preferencesDataStore(name = "session")
private val Context.settingsDataStore by preferencesDataStore(name = "settings")

data class StoredSession(val accessToken: String, val refreshToken: String, val accessTokenExpiresAt: String, val user: UserSummary)

/** Holds the authenticated session. Tokens are encrypted with [TokenCipher]; nothing sensitive is stored in plain text. */
@Singleton
class SessionStore @Inject constructor(
    @Named("session") private val dataStore: DataStore<Preferences>,
    private val cipher: TokenCipher,
) {
    private val json = Json { ignoreUnknownKeys = true }
    @Volatile private var cached: StoredSession? = null

    val session: Flow<StoredSession?> = dataStore.data.map { read(it) }
    val user: Flow<UserSummary?> = session.map { it?.user }

    private fun read(p: Preferences): StoredSession? {
        val access = p[ACCESS]?.let(cipher::decrypt) ?: return null
        val refresh = p[REFRESH]?.let(cipher::decrypt) ?: return null
        val user = p[USER]?.let { runCatching { json.decodeFromString(UserSummary.serializer(), it) }.getOrNull() } ?: return null
        return StoredSession(access, refresh, p[ACCESS_EXP].orEmpty(), user).also { cached = it }
    }

    suspend fun current(): StoredSession? = cached ?: session.first()

    suspend fun save(auth: AuthSession) {
        dataStore.edit {
            it[ACCESS] = cipher.encrypt(auth.accessToken)
            it[REFRESH] = cipher.encrypt(auth.refreshToken)
            it[ACCESS_EXP] = auth.accessTokenExpiresAt
            it[USER] = json.encodeToString(UserSummary.serializer(), auth.user)
        }
        cached = StoredSession(auth.accessToken, auth.refreshToken, auth.accessTokenExpiresAt, auth.user)
    }

    suspend fun updateUser(user: UserSummary) {
        dataStore.edit { it[USER] = json.encodeToString(UserSummary.serializer(), user) }
        cached = cached?.copy(user = user)
    }

    suspend fun clear() {
        cached = null
        dataStore.edit { it.clear() }
    }

    private companion object {
        val ACCESS = stringPreferencesKey("access_token")
        val REFRESH = stringPreferencesKey("refresh_token")
        val ACCESS_EXP = stringPreferencesKey("access_expires")
        val USER = stringPreferencesKey("user")
    }
}

@Singleton
class SettingsStore @Inject constructor(@Named("settings") private val dataStore: DataStore<Preferences>) {
    val themeMode: Flow<String> = dataStore.data.map { it[THEME] ?: "system" }
    val onboardingCompleted: Flow<Boolean> = dataStore.data.map { it[ONBOARDING] ?: false }
    val notificationsEnabled: Flow<Boolean> = dataStore.data.map { it[NOTIFICATIONS] ?: true }
    val lastSyncedAt: Flow<Long?> = dataStore.data.map { it[LAST_SYNC] }
    val lastPullCursor: Flow<String?> = dataStore.data.map { it[PULL_CURSOR] }
    /** Id of the account whose data is cached locally; used to wipe the cache when a different account signs in. */
    val cachedUserId: Flow<String?> = dataStore.data.map { it[CACHED_USER] }

    suspend fun setTheme(mode: String) = dataStore.edit { it[THEME] = mode }
    suspend fun setOnboardingCompleted() = dataStore.edit { it[ONBOARDING] = true }
    suspend fun setNotificationsEnabled(enabled: Boolean) = dataStore.edit { it[NOTIFICATIONS] = enabled }
    suspend fun setSynced(at: Long, cursor: String) = dataStore.edit { it[LAST_SYNC] = at; it[PULL_CURSOR] = cursor }
    suspend fun clearSync() = dataStore.edit { it.remove(LAST_SYNC); it.remove(PULL_CURSOR) }
    suspend fun setCachedUserId(id: String?) = dataStore.edit { if (id == null) it.remove(CACHED_USER) else it[CACHED_USER] = id }

    private companion object {
        val THEME = stringPreferencesKey("theme")
        val ONBOARDING = booleanPreferencesKey("onboarding_completed")
        val NOTIFICATIONS = booleanPreferencesKey("notifications_enabled")
        val LAST_SYNC = longPreferencesKey("last_sync")
        val PULL_CURSOR = stringPreferencesKey("pull_cursor")
        val CACHED_USER = stringPreferencesKey("cached_user_id")
    }
}

@Module
@InstallIn(SingletonComponent::class)
abstract class DataStoreBindings {
    @Binds abstract fun cipher(impl: KeystoreTokenCipher): TokenCipher
}

@Module
@InstallIn(SingletonComponent::class)
object DataStoreModule {
    @Provides @Singleton @Named("session")
    fun session(@ApplicationContext context: Context): DataStore<Preferences> = context.sessionDataStore

    @Provides @Singleton @Named("settings")
    fun settings(@ApplicationContext context: Context): DataStore<Preferences> = context.settingsDataStore
}
