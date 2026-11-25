package com.devinterview.feature.learning

import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import androidx.navigation.toRoute
import com.devinterview.core.designsystem.Loadable
import com.devinterview.core.designsystem.toLoadable
import com.devinterview.core.navigation.Routes
import com.devinterview.domain.model.*
import com.devinterview.domain.repository.LearningRepository
import com.devinterview.core.common.ApplicationScope
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.flow.*
import kotlinx.coroutines.launch
import javax.inject.Inject

@HiltViewModel
class LearnViewModel @Inject constructor(private val repo: LearningRepository) : ViewModel() {
    private val _state = MutableStateFlow<Loadable<List<LearningPathSummary>>>(Loadable.Loading)
    val state: StateFlow<Loadable<List<LearningPathSummary>>> = _state
    val downloaded: StateFlow<Set<String>> = repo.observeDownloadedPathIds().stateIn(viewModelScope, SharingStarted.WhileSubscribed(5000), emptySet())
    val levelFilter = MutableStateFlow<String?>(null)

    init { load() }
    fun load() = viewModelScope.launch { if (_state.value !is Loadable.Ready) _state.value = Loadable.Loading; _state.value = repo.paths().toLoadable() }
}

data class PathDetailUiState(val path: Loadable<LearningPathDetail> = Loadable.Loading, val downloading: Boolean = false, val message: String? = null)

@HiltViewModel
class LearningPathViewModel @Inject constructor(private val repo: LearningRepository, handle: SavedStateHandle) : ViewModel() {
    private val pathId = handle.toRoute<Routes.LearningPathDetail>().pathId
    private val _state = MutableStateFlow(PathDetailUiState())
    val state: StateFlow<PathDetailUiState> = _state
    val isDownloaded: StateFlow<Boolean> = repo.observeDownloadedPathIds().map { pathId in it }.stateIn(viewModelScope, SharingStarted.WhileSubscribed(5000), false)

    init { load() }
    fun load() = viewModelScope.launch { _state.update { it.copy(path = repo.path(pathId).toLoadable()) } }

    fun download() {
        if (_state.value.downloading) return
        _state.update { it.copy(downloading = true) }
        viewModelScope.launch {
            val msg = when (val r = repo.downloadPath(pathId)) {
                is AppResult.Success -> "${r.data} lessons saved for offline reading"
                is AppResult.Failure -> r.error.message
            }
            _state.update { it.copy(downloading = false, message = msg) }
        }
    }

    fun clearMessage() = _state.update { it.copy(message = null) }
}

data class LessonUiState(val lesson: Loadable<Lesson> = Loadable.Loading, val completing: Boolean = false, val pathProgress: Int? = null)

@HiltViewModel
class LessonViewModel @Inject constructor(
    private val repo: LearningRepository,
    @ApplicationScope private val appScope: CoroutineScope,
    handle: SavedStateHandle,
) : ViewModel() {
    private val lessonId = MutableStateFlow(handle.toRoute<Routes.LessonDetail>().lessonId)
    private val _state = MutableStateFlow(LessonUiState())
    val state: StateFlow<LessonUiState> = _state
    private val _events = Channel<String>(Channel.BUFFERED)
    val events = _events.receiveAsFlow()
    private var openedAt = System.currentTimeMillis()

    init { load() }

    fun load() = viewModelScope.launch {
        _state.value = LessonUiState()
        openedAt = System.currentTimeMillis()
        val r = repo.lesson(lessonId.value)
        _state.update { it.copy(lesson = r.toLoadable()) }
        (r as? AppResult.Success)?.data?.takeIf { it.status == "NotStarted" }?.let { repo.updateProgress(it.id, "InProgress", 0) }
    }

    fun open(id: String) { recordTime(); lessonId.value = id; load() }

    private fun elapsed() = ((System.currentTimeMillis() - openedAt) / 1000).toInt().coerceIn(0, 4 * 3600)

    private fun recordTime() {
        val lesson = _state.value.lesson.dataOrNull ?: return
        if (lesson.status == "Completed") return
        val seconds = elapsed()
        // Application scope: this also runs from onCleared, after viewModelScope is cancelled.
        if (seconds >= 10) appScope.launch { repo.updateProgress(lesson.id, "InProgress", seconds) }
    }

    fun complete() {
        val lesson = _state.value.lesson.dataOrNull ?: return
        _state.update { it.copy(completing = true) }
        viewModelScope.launch {
            when (val r = repo.updateProgress(lesson.id, "Completed", elapsed())) {
                is AppResult.Success -> {
                    _state.update { it.copy(completing = false, pathProgress = r.data.pathProgressPercent, lesson = Loadable.Ready(lesson.copy(status = "Completed"))) }
                    _events.send(if (r.data.xpAwarded > 0) "Lesson complete · +${r.data.xpAwarded} XP" else "Lesson complete")
                }
                is AppResult.Failure -> { _state.update { it.copy(completing = false) }; _events.send(r.error.message) }
            }
        }
    }

    override fun onCleared() { recordTime() }
}

@HiltViewModel
class CompaniesViewModel @Inject constructor(private val repo: LearningRepository) : ViewModel() {
    private val _state = MutableStateFlow<Loadable<List<CompanySummary>>>(Loadable.Loading)
    val state: StateFlow<Loadable<List<CompanySummary>>> = _state
    init { load() }
    fun load() = viewModelScope.launch { _state.value = Loadable.Loading; _state.value = repo.companies().toLoadable() }
}

@HiltViewModel
class CompanyViewModel @Inject constructor(private val repo: LearningRepository, handle: SavedStateHandle) : ViewModel() {
    private val id = handle.toRoute<Routes.CompanyDetail>().companyId
    private val _state = MutableStateFlow<Loadable<CompanyDetail>>(Loadable.Loading)
    val state: StateFlow<Loadable<CompanyDetail>> = _state
    init { load() }
    fun load() = viewModelScope.launch { _state.value = Loadable.Loading; _state.value = repo.company(id).toLoadable() }
}

@HiltViewModel
class CompanyCollectionViewModel @Inject constructor(private val repo: LearningRepository, handle: SavedStateHandle) : ViewModel() {
    private val id = handle.toRoute<Routes.CompanyCollection>().collectionId
    private val _state = MutableStateFlow<Loadable<CompanyCollectionDetail>>(Loadable.Loading)
    val state: StateFlow<Loadable<CompanyCollectionDetail>> = _state
    init { load() }
    fun load() = viewModelScope.launch { _state.value = Loadable.Loading; _state.value = repo.companyCollection(id).toLoadable() }
}

/** Human-readable label for ContentSource values, used to make content provenance explicit. */
fun sourceLabel(source: String) = when (source) {
    "Verified" -> "Verified from public source"
    "CommunityContributed" -> "Community contributed"
    else -> "Illustrative practice"
}
