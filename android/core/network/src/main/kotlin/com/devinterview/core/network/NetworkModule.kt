package com.devinterview.core.network

import android.os.Build
import com.devinterview.core.datastore.SessionStore
import com.devinterview.domain.model.AppError
import com.devinterview.domain.model.AppResult
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.SharedFlow
import kotlinx.coroutines.runBlocking
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import kotlinx.serialization.json.Json
import okhttp3.Authenticator
import okhttp3.Interceptor
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.Response
import okhttp3.Route
import okhttp3.logging.HttpLoggingInterceptor
import retrofit2.HttpException
import retrofit2.Retrofit
import retrofit2.converter.kotlinx.serialization.asConverterFactory
import java.io.IOException
import java.net.SocketTimeoutException
import java.util.concurrent.TimeUnit
import javax.inject.Inject
import javax.inject.Named
import javax.inject.Singleton

val NetworkJson = Json {
    ignoreUnknownKeys = true
    explicitNulls = false
    coerceInputValues = true
    encodeDefaults = true
}

/** Emits when the session can no longer be refreshed so the app can navigate to sign-in. */
@Singleton
class SessionEvents @Inject constructor() {
    private val _expired = MutableSharedFlow<Unit>(extraBufferCapacity = 1)
    val expired: SharedFlow<Unit> = _expired
    fun notifyExpired() { _expired.tryEmit(Unit) }
}

class AuthInterceptor @Inject constructor(private val session: SessionStore) : Interceptor {
    override fun intercept(chain: Interceptor.Chain): Response {
        val request = chain.request()
        if (request.header(NO_AUTH) != null) return chain.proceed(request.newBuilder().removeHeader(NO_AUTH).build())
        val token = runBlocking { session.current()?.accessToken }
        val authed = if (token != null) request.newBuilder().header("Authorization", "Bearer $token").build() else request
        return chain.proceed(authed)
    }

    companion object { const val NO_AUTH = "X-No-Auth" }
}

/**
 * Refreshes the access token on 401 responses. A mutex makes the refresh single-flight: concurrent requests wait and
 * reuse the new token instead of each presenting the (now rotated) refresh token, which the server would treat as reuse.
 */
class TokenAuthenticator @Inject constructor(
    private val session: SessionStore,
    private val refreshApi: dagger.Lazy<RefreshApi>,
    private val events: SessionEvents,
) : Authenticator {
    private val mutex = Mutex()

    override fun authenticate(route: Route?, response: Response): Request? {
        if (response.request.header("Authorization") == null) return null
        if (responseCount(response) >= 2) return null
        val failedToken = response.request.header("Authorization")?.removePrefix("Bearer ")
        return runBlocking {
            mutex.withLock {
                val current = session.current() ?: return@withLock null
                if (current.accessToken != failedToken) {
                    // Another request already refreshed while we waited.
                    return@withLock response.request.newBuilder().header("Authorization", "Bearer ${current.accessToken}").build()
                }
                val refreshed = runCatching { refreshApi.get().refresh(RefreshBody(current.refreshToken)) }.getOrNull()
                val data = refreshed?.data
                if (refreshed?.success == true && data != null) {
                    session.save(data)
                    response.request.newBuilder().header("Authorization", "Bearer ${data.accessToken}").build()
                } else {
                    session.clear()
                    events.notifyExpired()
                    null
                }
            }
        }
    }

    private fun responseCount(response: Response): Int {
        var count = 1
        var prior = response.priorResponse
        while (prior != null) { count++; prior = prior.priorResponse }
        return count
    }
}

/** Executes an API call and maps the envelope, HTTP errors and I/O failures to typed [AppError]s. */
suspend fun <T> apiCall(block: suspend () -> Envelope<T>): AppResult<T> = try {
    val envelope = block()
    val data = envelope.data
    when {
        envelope.success && data != null -> AppResult.Success(data)
        envelope.success -> @Suppress("UNCHECKED_CAST") (AppResult.Success(Unit as T))
        else -> AppResult.Failure(envelope.error.toAppError(200))
    }
} catch (e: CancellationException) {
    throw e
} catch (e: HttpException) {
    val body = e.response()?.errorBody()?.string()
    val error = body?.let { runCatching { NetworkJson.decodeFromString(Envelope.serializer(kotlinx.serialization.json.JsonElement.serializer()), it).error }.getOrNull() }
    AppResult.Failure(error.toAppError(e.code()))
} catch (e: SocketTimeoutException) {
    AppResult.Failure(AppError.Timeout())
} catch (e: IOException) {
    AppResult.Failure(AppError.Network())
} catch (e: kotlinx.serialization.SerializationException) {
    AppResult.Failure(AppError.Unknown("The server returned an unexpected response."))
}

/** Like [apiCall] for endpoints whose payload is irrelevant. */
suspend fun apiUnit(block: suspend () -> Envelope<*>): AppResult<Unit> = when (val r = apiCall { block().let { Envelope(it.success, Unit, it.error) } }) {
    is AppResult.Success -> AppResult.Success(Unit)
    is AppResult.Failure -> r
}

fun ApiErrorBody?.toAppError(status: Int): AppError {
    val code = this?.code ?: "unknown"
    val message = this?.message?.takeIf { it.isNotBlank() } ?: "Request failed ($status)."
    return when {
        code == "validation_error" -> AppError.Validation(message, this?.details.orEmpty())
        code == "premium_required" || status == 402 -> AppError.PremiumRequired(message)
        code == "quota_exceeded" -> AppError.QuotaExceeded(message)
        code == "not_found" || status == 404 -> AppError.NotFound(message)
        code == "unauthorized" || code == "invalid_token" -> AppError.Unauthorized(message)
        status == 503 || code in setOf("ai_unavailable", "execution_unavailable", "billing_unavailable", "ai_invalid_response") -> AppError.Unavailable(code, message)
        status == 429 -> AppError.QuotaExceeded(message)
        else -> AppError.Server(code, message, status)
    }
}

@Module
@InstallIn(SingletonComponent::class)
object NetworkModule {
    @Provides @Singleton @Named("baseUrl") fun baseUrl(): String = BuildConfig.API_BASE_URL

    @Provides @Singleton
    fun okHttp(auth: AuthInterceptor, authenticator: TokenAuthenticator): OkHttpClient = OkHttpClient.Builder()
        .connectTimeout(15, TimeUnit.SECONDS)
        .readTimeout(120, TimeUnit.SECONDS) // AI evaluation and code execution can take a while
        .writeTimeout(30, TimeUnit.SECONDS)
        .addInterceptor(auth)
        .addInterceptor { chain -> chain.proceed(chain.request().newBuilder().header("User-Agent", "DevInterview-Android/${Build.VERSION.SDK_INT}").build()) }
        .addInterceptor(HttpLoggingInterceptor().apply {
            level = if (BuildConfig.DEBUG) HttpLoggingInterceptor.Level.BASIC else HttpLoggingInterceptor.Level.NONE
            redactHeader("Authorization")
        })
        .authenticator(authenticator)
        .build()

    @Provides @Singleton
    fun retrofit(client: OkHttpClient, @Named("baseUrl") baseUrl: String): Retrofit = Retrofit.Builder()
        .baseUrl(baseUrl)
        .client(client)
        .addConverterFactory(NetworkJson.asConverterFactory("application/json".toMediaType()))
        .build()

    @Provides @Singleton
    fun refreshApi(@Named("baseUrl") baseUrl: String): RefreshApi = Retrofit.Builder()
        .baseUrl(baseUrl)
        .client(OkHttpClient.Builder().connectTimeout(15, TimeUnit.SECONDS).build())
        .addConverterFactory(NetworkJson.asConverterFactory("application/json".toMediaType()))
        .build().create(RefreshApi::class.java)

    @Provides @Singleton fun authApi(r: Retrofit): AuthApi = r.create(AuthApi::class.java)
    @Provides @Singleton fun userApi(r: Retrofit): UserApi = r.create(UserApi::class.java)
    @Provides @Singleton fun contentApi(r: Retrofit): ContentApi = r.create(ContentApi::class.java)
    @Provides @Singleton fun practiceApi(r: Retrofit): PracticeApi = r.create(PracticeApi::class.java)
    @Provides @Singleton fun aiApi(r: Retrofit): AiApi = r.create(AiApi::class.java)
    @Provides @Singleton fun engagementApi(r: Retrofit): EngagementApi = r.create(EngagementApi::class.java)
}
