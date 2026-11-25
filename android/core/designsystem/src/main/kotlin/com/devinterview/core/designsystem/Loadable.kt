package com.devinterview.core.designsystem

import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import com.devinterview.domain.model.AppError
import com.devinterview.domain.model.AppResult

/** Screen-level loading state. */
sealed interface Loadable<out T> {
    data object Loading : Loadable<Nothing>
    data class Ready<T>(val data: T) : Loadable<T>
    data class Failed(val error: AppError) : Loadable<Nothing>

    val dataOrNull: T? get() = (this as? Ready<T>)?.data
}

fun <T> AppResult<T>.toLoadable(): Loadable<T> = when (this) {
    is AppResult.Success -> Loadable.Ready(data)
    is AppResult.Failure -> Loadable.Failed(error)
}

@Composable
fun <T> LoadableContent(
    state: Loadable<T>, onRetry: () -> Unit, modifier: Modifier = Modifier, onUpgrade: (() -> Unit)? = null,
    loading: @Composable () -> Unit = { LoadingState(modifier) },
    content: @Composable (T) -> Unit,
) {
    when (state) {
        Loadable.Loading -> loading()
        is Loadable.Failed -> ErrorState(state.error, onRetry, modifier, onUpgrade)
        is Loadable.Ready -> content(state.data)
    }
}
