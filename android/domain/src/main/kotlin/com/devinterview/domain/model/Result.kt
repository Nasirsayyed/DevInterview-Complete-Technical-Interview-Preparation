package com.devinterview.domain.model

/** Result of a repository call. Errors are typed so the UI can react (e.g. show a paywall for PremiumRequired). */
sealed interface AppResult<out T> {
    data class Success<T>(val data: T) : AppResult<T>
    data class Failure(val error: AppError) : AppResult<Nothing>
}

sealed class AppError(open val message: String) {
    data class Network(override val message: String = "No internet connection. Check your network and try again.") : AppError(message)
    data class Timeout(override val message: String = "The server took too long to respond.") : AppError(message)
    data class Unauthorized(override val message: String = "Your session has expired. Please sign in again.") : AppError(message)
    data class Validation(override val message: String, val fields: Map<String, List<String>> = emptyMap()) : AppError(message)
    data class NotFound(override val message: String = "Not found.") : AppError(message)
    data class PremiumRequired(override val message: String) : AppError(message)
    data class QuotaExceeded(override val message: String) : AppError(message)
    data class Unavailable(override val code: String, override val message: String) : AppError(message), HasCode
    data class Server(override val code: String, override val message: String, val status: Int) : AppError(message), HasCode
    data class Offline(override val message: String = "This feature needs an internet connection.") : AppError(message)
    data class Unknown(override val message: String = "Something went wrong. Please try again.") : AppError(message)
}

interface HasCode { val code: String }

inline fun <T, R> AppResult<T>.map(transform: (T) -> R): AppResult<R> = when (this) {
    is AppResult.Success -> AppResult.Success(transform(data))
    is AppResult.Failure -> this
}

inline fun <T> AppResult<T>.onSuccess(block: (T) -> Unit): AppResult<T> = also { if (it is AppResult.Success) block(it.data) }
inline fun <T> AppResult<T>.onFailure(block: (AppError) -> Unit): AppResult<T> = also { if (it is AppResult.Failure) block(it.error) }
fun <T> AppResult<T>.getOrNull(): T? = (this as? AppResult.Success)?.data

@kotlinx.serialization.Serializable
data class Page<T>(
    val items: List<T> = emptyList(),
    val page: Int = 1,
    val pageSize: Int = 20,
    val totalCount: Int = 0,
    val totalPages: Int = 0,
    val hasNext: Boolean = false,
)

@kotlinx.serialization.Serializable
data class Ref(val id: String, val name: String, val slug: String? = null)
