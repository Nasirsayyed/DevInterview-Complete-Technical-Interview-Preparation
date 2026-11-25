package com.devinterview.data

import com.devinterview.core.network.ContentApi
import com.devinterview.core.network.NetworkJson
import com.devinterview.core.network.apiCall
import com.devinterview.domain.model.AppError
import com.devinterview.domain.model.AppResult
import com.google.common.truth.Truth.assertThat
import kotlinx.coroutines.runBlocking
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import okhttp3.mockwebserver.SocketPolicy
import org.junit.After
import org.junit.Test
import retrofit2.Retrofit
import retrofit2.converter.kotlinx.serialization.asConverterFactory

/** The ApiResponse<T> envelope from the backend is mapped to typed errors the UI reacts to (paywall, quota, validation). */
class ApiErrorMappingTest {
    private val server = MockWebServer().apply { start() }
    private val api = Retrofit.Builder().baseUrl(server.url("/"))
        .addConverterFactory(NetworkJson.asConverterFactory("application/json".toMediaType())).build().create(ContentApi::class.java)

    @After fun tearDown() = server.shutdown()

    private fun error(status: Int, code: String, details: String = "null") = MockResponse().setResponseCode(status)
        .setHeader("Content-Type", "application/json")
        .setBody("""{"success":false,"data":null,"error":{"code":"$code","message":"msg $code","details":$details}}""")

    private fun call() = runBlocking { apiCall { api.technologies() } }

    @Test fun `success envelope returns data`() {
        server.enqueue(MockResponse().setHeader("Content-Type", "application/json")
            .setBody("""{"success":true,"data":[{"id":"t1","name":"C#","slug":"csharp","unknownField":1}]}"""))
        val r = call() as AppResult.Success
        assertThat(r.data.single().name).isEqualTo("C#")
    }

    @Test fun `premium required maps to paywall error`() {
        server.enqueue(error(402, "premium_required"))
        assertThat((call() as AppResult.Failure).error).isInstanceOf(AppError.PremiumRequired::class.java)
    }

    @Test fun `quota and rate limits map to QuotaExceeded`() {
        server.enqueue(error(429, "quota_exceeded"))
        assertThat((call() as AppResult.Failure).error).isInstanceOf(AppError.QuotaExceeded::class.java)
        server.enqueue(error(429, "rate_limited"))
        assertThat((call() as AppResult.Failure).error).isInstanceOf(AppError.QuotaExceeded::class.java)
    }

    @Test fun `validation errors keep field details`() {
        server.enqueue(error(400, "validation_error", """{"email":["Email is invalid."]}"""))
        val e = (call() as AppResult.Failure).error as AppError.Validation
        assertThat(e.fields["email"]).containsExactly("Email is invalid.")
    }

    @Test fun `ai unavailable maps to Unavailable with code`() {
        server.enqueue(error(503, "ai_unavailable"))
        val e = (call() as AppResult.Failure).error as AppError.Unavailable
        assertThat(e.code).isEqualTo("ai_unavailable")
    }

    @Test fun `other server errors keep their code`() {
        server.enqueue(error(403, "email_not_verified"))
        val e = (call() as AppResult.Failure).error as AppError.Server
        assertThat(e.code).isEqualTo("email_not_verified")
        assertThat(e.status).isEqualTo(403)
    }

    @Test fun `dropped connection maps to Network error`() {
        server.enqueue(MockResponse().setSocketPolicy(SocketPolicy.DISCONNECT_AT_START))
        assertThat((call() as AppResult.Failure).error).isInstanceOf(AppError.Network::class.java)
    }

    @Test fun `non-json body maps to Unknown, not a crash`() {
        server.enqueue(MockResponse().setHeader("Content-Type", "application/json").setBody("<html>proxy</html>"))
        assertThat((call() as AppResult.Failure).error).isInstanceOf(AppError.Unknown::class.java)
    }
}
