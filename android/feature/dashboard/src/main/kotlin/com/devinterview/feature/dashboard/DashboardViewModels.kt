package com.devinterview.feature.dashboard

import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import androidx.navigation.toRoute
import com.devinterview.core.designsystem.Loadable
import com.devinterview.core.designsystem.toLoadable
import com.devinterview.core.navigation.Routes
import com.devinterview.domain.model.*
import com.devinterview.domain.repository.DashboardRepository
import com.devinterview.domain.repository.NotificationRepository
import com.devinterview.domain.repository.SyncRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.FlowPreview
import kotlinx.coroutines.async
import kotlinx.coroutines.flow.*
import kotlinx.coroutines.launch
import javax.inject.Inject

data class HomeData(val summary: DashboardSummary, val activity: List<ActivityDay>, val skills: List<SkillProgress>)

@HiltViewModel
class HomeViewModel @Inject constructor(private val dashboard: DashboardRepository, private val sync: SyncRepository) : ViewModel() {
    private val _state = MutableStateFlow<Loadable<HomeData>>(Loadable.Loading)
    val state: StateFlow<Loadable<HomeData>> = _state
    private val _refreshing = MutableStateFlow(false)
    val refreshing: StateFlow<Boolean> = _refreshing

    init { load() }

    fun load(pullToRefresh: Boolean = false) = viewModelScope.launch {
        if (pullToRefresh) _refreshing.value = true else if (_state.value !is Loadable.Ready) _state.value = Loadable.Loading
        if (pullToRefresh) sync.scheduleSync()
        val summary = async { dashboard.summary() }
        val activity = async { dashboard.activity(14) }
        val skills = async { dashboard.skills() }
        _state.value = when (val s = summary.await()) {
            is AppResult.Success -> Loadable.Ready(HomeData(s.data, activity.await().getOrNull().orEmpty(), skills.await().getOrNull().orEmpty()))
            is AppResult.Failure -> if (_state.value is Loadable.Ready && pullToRefresh) _state.value else Loadable.Failed(s.error)
        }
        _refreshing.value = false
    }
}

data class NotificationsUiState(
    val items: List<AppNotification> = emptyList(), val page: Int = 0, val hasNext: Boolean = true, val loading: Boolean = false,
    val error: AppError? = null,
)

@HiltViewModel
class NotificationsViewModel @Inject constructor(private val repo: NotificationRepository) : ViewModel() {
    private val _state = MutableStateFlow(NotificationsUiState())
    val state: StateFlow<NotificationsUiState> = _state

    init { loadMore() }

    fun refresh() { _state.value = NotificationsUiState(); loadMore() }

    fun loadMore() {
        val s = _state.value
        if (s.loading || !s.hasNext) return
        _state.update { it.copy(loading = true, error = null) }
        viewModelScope.launch {
            when (val r = repo.notifications(s.page + 1)) {
                is AppResult.Success -> _state.update { it.copy(items = it.items + r.data.items, page = r.data.page, hasNext = r.data.hasNext, loading = false) }
                is AppResult.Failure -> _state.update { it.copy(error = r.error, loading = false) }
            }
        }
    }

    fun open(n: AppNotification) {
        if (n.readAt != null) return
        _state.update { s -> s.copy(items = s.items.map { if (it.id == n.id) it.copy(readAt = "now") else it }) }
        viewModelScope.launch { repo.markRead(n.id) }
    }

    fun markAllRead() = viewModelScope.launch {
        if (repo.markAllRead() is AppResult.Success) _state.update { s -> s.copy(items = s.items.map { it.copy(readAt = it.readAt ?: "now") }) }
    }
}

data class SearchUiState(
    val query: String = "", val type: String? = null, val results: SearchResults? = null, val loading: Boolean = false, val error: AppError? = null,
)

@OptIn(FlowPreview::class)
@HiltViewModel
class SearchViewModel @Inject constructor(private val dashboard: DashboardRepository, handle: SavedStateHandle) : ViewModel() {
    private val _state = MutableStateFlow(SearchUiState(query = handle.toRoute<Routes.Search>().query))
    val state: StateFlow<SearchUiState> = _state

    init {
        viewModelScope.launch {
            _state.map { it.query.trim() to it.type }.distinctUntilChanged().debounce(350).collectLatest { (q, type) ->
                if (q.length < 2) { _state.update { it.copy(results = null, loading = false, error = null) }; return@collectLatest }
                _state.update { it.copy(loading = true, error = null) }
                when (val r = dashboard.search(q, type, 1)) {
                    is AppResult.Success -> _state.update { it.copy(results = r.data, loading = false) }
                    is AppResult.Failure -> _state.update { it.copy(error = r.error, loading = false) }
                }
            }
        }
    }

    fun onQuery(q: String) = _state.update { it.copy(query = q) }
    fun onType(type: String?) = _state.update { it.copy(type = type) }
}
