package com.devinterview.feature.questions

import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import androidx.navigation.toRoute
import androidx.paging.*
import com.devinterview.core.designsystem.Loadable
import com.devinterview.core.designsystem.toLoadable
import com.devinterview.core.navigation.Routes
import com.devinterview.domain.model.*
import com.devinterview.domain.repository.LibraryRepository
import com.devinterview.domain.repository.QuestionRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.flow.*
import kotlinx.coroutines.launch
import javax.inject.Inject

sealed interface QuestionEvent {
    data class Message(val text: String) : QuestionEvent
    data class Share(val text: String) : QuestionEvent
    data class Navigate(val route: Any) : QuestionEvent
}

@HiltViewModel
class TechnologyExplorerViewModel @Inject constructor(private val repo: QuestionRepository) : ViewModel() {
    private val _state = MutableStateFlow<Loadable<List<Technology>>>(Loadable.Loading)
    val state: StateFlow<Loadable<List<Technology>>> = _state
    val query = MutableStateFlow("")

    init { load() }
    fun load() = viewModelScope.launch { _state.value = Loadable.Loading; _state.value = repo.technologies().toLoadable() }
}

@HiltViewModel
class CategoriesViewModel @Inject constructor(private val repo: QuestionRepository, handle: SavedStateHandle) : ViewModel() {
    val route = handle.toRoute<Routes.Categories>()
    private val _state = MutableStateFlow<Loadable<List<Category>>>(Loadable.Loading)
    val state: StateFlow<Loadable<List<Category>>> = _state

    init { load() }
    fun load() = viewModelScope.launch { _state.value = Loadable.Loading; _state.value = repo.categories(route.technologyId).toLoadable() }
}

/** Pages through the question API; errors surface through Paging's LoadState. */
class QuestionPagingSource(private val repo: QuestionRepository, private val filter: QuestionFilter) : PagingSource<Int, QuestionSummary>() {
    override fun getRefreshKey(state: PagingState<Int, QuestionSummary>): Int? = state.anchorPosition?.let { pos ->
        state.closestPageToPosition(pos)?.let { it.prevKey?.plus(1) ?: it.nextKey?.minus(1) }
    }

    override suspend fun load(params: LoadParams<Int>): LoadResult<Int, QuestionSummary> {
        val page = params.key ?: 1
        return when (val r = repo.questions(filter, page, params.loadSize.coerceAtMost(50))) {
            is AppResult.Success -> LoadResult.Page(r.data.items, if (page == 1) null else page - 1, if (r.data.hasNext) page + 1 else null)
            is AppResult.Failure -> LoadResult.Error(AppErrorException(r.error))
        }
    }
}

class AppErrorException(val error: AppError) : Exception(error.message)

@OptIn(ExperimentalCoroutinesApi::class)
@HiltViewModel
class QuestionListViewModel @Inject constructor(private val repo: QuestionRepository, handle: SavedStateHandle) : ViewModel() {
    val route = handle.toRoute<Routes.QuestionList>()
    private val _filter = MutableStateFlow(
        QuestionFilter(technologyId = route.technologyId, categoryId = route.categoryId, difficulty = route.difficulty, bookmarkedOnly = route.bookmarkedOnly)
    )
    val filter: StateFlow<QuestionFilter> = _filter

    val questions: Flow<PagingData<QuestionSummary>> = _filter.debounce(250).distinctUntilChanged().flatMapLatest { f ->
        Pager(PagingConfig(pageSize = 20, enablePlaceholders = false)) { QuestionPagingSource(repo, f) }.flow
    }.cachedIn(viewModelScope)

    fun update(transform: (QuestionFilter) -> QuestionFilter) = _filter.update(transform)
}

data class QuestionDetailUiState(
    val question: Loadable<QuestionDetail> = Loadable.Loading,
    val selected: Set<String> = emptySet(),
    val answer: QuestionAnswer? = null,
    val result: AttemptResult? = null,
    val revealing: Boolean = false,
    val submitting: Boolean = false,
    val reportReasons: List<String> = emptyList(),
    val collections: List<SavedCollection> = emptyList(),
)

@OptIn(ExperimentalCoroutinesApi::class)
@HiltViewModel
class QuestionDetailViewModel @Inject constructor(
    private val repo: QuestionRepository,
    private val library: LibraryRepository,
    handle: SavedStateHandle,
) : ViewModel() {
    private val questionId = MutableStateFlow(handle.toRoute<Routes.QuestionDetail>().questionId)
    private val _state = MutableStateFlow(QuestionDetailUiState())
    val state: StateFlow<QuestionDetailUiState> = _state
    private val _events = Channel<QuestionEvent>(Channel.BUFFERED)
    val events = _events.receiveAsFlow()
    private var openedAt = System.currentTimeMillis()

    val isBookmarked: StateFlow<Boolean> = questionId.flatMapLatest { library.observeIsBookmarked(it) }.stateIn(viewModelScope, SharingStarted.Eagerly, false)
    val note: StateFlow<String?> = questionId.flatMapLatest { library.observeNote(it) }.stateIn(viewModelScope, SharingStarted.Eagerly, null)

    init { load() }

    fun load() = viewModelScope.launch {
        _state.value = QuestionDetailUiState()
        openedAt = System.currentTimeMillis()
        _state.update { it.copy(question = repo.question(questionId.value).toLoadable()) }
    }

    /** Moves to the previous/next question in the same category without growing the back stack. */
    fun goTo(id: String) { questionId.value = id; load() }

    private val current get() = _state.value.question.dataOrNull

    fun toggleOption(optionId: String) {
        val q = current ?: return
        if (_state.value.result != null) return
        _state.update { s ->
            val sel = if (q.allowsMultipleAnswers) { if (optionId in s.selected) s.selected - optionId else s.selected + optionId } else setOf(optionId)
            s.copy(selected = sel)
        }
    }

    private fun elapsed() = ((System.currentTimeMillis() - openedAt) / 1000).toInt().coerceIn(0, 3600)

    fun submitAnswer() {
        val q = current ?: return
        val s = _state.value
        if (s.selected.isEmpty() || s.submitting) return
        _state.update { it.copy(submitting = true) }
        viewModelScope.launch {
            when (val r = repo.attempt(q.id, s.selected.toList(), elapsed(), null)) {
                is AppResult.Success -> {
                    _state.update { it.copy(result = r.data, answer = r.data.answer, submitting = false) }
                    if (r.data.xpAwarded > 0) _events.send(QuestionEvent.Message("+${r.data.xpAwarded} XP"))
                }
                is AppResult.Failure -> { _state.update { it.copy(submitting = false) }; _events.send(QuestionEvent.Message(r.error.message)) }
            }
        }
    }

    fun revealAnswer() {
        val q = current ?: return
        _state.update { it.copy(revealing = true) }
        viewModelScope.launch {
            when (val r = repo.answer(q.id)) {
                is AppResult.Success -> _state.update { it.copy(answer = r.data, revealing = false) }
                is AppResult.Failure -> { _state.update { it.copy(revealing = false) }; _events.send(QuestionEvent.Message(r.error.message)) }
            }
        }
    }

    /** For open-ended questions the learner grades themselves after reading the model answer. */
    fun selfAssess(knewIt: Boolean) {
        val q = current ?: return
        viewModelScope.launch {
            when (val r = repo.attempt(q.id, emptyList(), elapsed(), knewIt)) {
                is AppResult.Success -> {
                    _state.update { it.copy(result = r.data) }
                    _events.send(QuestionEvent.Message(if (r.data.xpAwarded > 0) "Progress saved · +${r.data.xpAwarded} XP" else "Progress saved"))
                }
                is AppResult.Failure -> _events.send(QuestionEvent.Message(r.error.message))
            }
        }
    }

    fun toggleBookmark() {
        val q = current ?: return
        viewModelScope.launch {
            library.setBookmark(q.toSummary(), !isBookmarked.value)
        }
    }

    fun saveNote(text: String) {
        val q = current ?: return
        viewModelScope.launch {
            if (text.isBlank()) library.deleteNote(q.id) else library.saveNote(q.id, q.title, q.technology.name, text.trim())
            _events.send(QuestionEvent.Message(if (text.isBlank()) "Note deleted" else "Note saved"))
        }
    }

    fun loadReportReasons() = viewModelScope.launch {
        if (_state.value.reportReasons.isEmpty()) repo.reportReasons().getOrNull()?.let { r -> _state.update { it.copy(reportReasons = r) } }
    }

    fun report(reason: String, details: String?) {
        val q = current ?: return
        viewModelScope.launch {
            val msg = when (val r = repo.report(q.id, reason, details?.ifBlank { null })) {
                is AppResult.Success -> "Thanks — our content team will review this question."
                is AppResult.Failure -> r.error.message
            }
            _events.send(QuestionEvent.Message(msg))
        }
    }

    fun loadCollections() = viewModelScope.launch { library.collections().getOrNull()?.let { c -> _state.update { it.copy(collections = c) } } }

    fun addToCollection(collectionId: String?, newName: String?) {
        val q = current ?: return
        viewModelScope.launch {
            val id = collectionId ?: when (val c = library.createCollection(newName!!.trim(), null)) {
                is AppResult.Success -> c.data.id
                is AppResult.Failure -> { _events.send(QuestionEvent.Message(c.error.message)); return@launch }
            }
            val msg = when (val r = library.addToCollection(id, "Question", q.id)) {
                is AppResult.Success -> "Added to collection"
                is AppResult.Failure -> r.error.message
            }
            _events.send(QuestionEvent.Message(msg))
        }
    }

    fun share() { current?.let { viewModelScope.launch { _events.send(QuestionEvent.Share(it.shareText.ifBlank { it.title })) } } }
}

fun QuestionDetail.toSummary() = QuestionSummary(id, title, technology, category, difficulty, type, estimatedSeconds, tags, isPremium, isBookmarked)

@HiltViewModel
class RandomQuestionViewModel @Inject constructor(private val repo: QuestionRepository, handle: SavedStateHandle) : ViewModel() {
    private val route = handle.toRoute<Routes.RandomQuestion>()
    private val _state = MutableStateFlow<Loadable<QuestionDetail>>(Loadable.Loading)
    val state: StateFlow<Loadable<QuestionDetail>> = _state

    init { load() }
    fun load() = viewModelScope.launch { _state.value = Loadable.Loading; _state.value = repo.random(route.technologyId, null).toLoadable() }
}

@HiltViewModel
class BookmarksViewModel @Inject constructor(private val library: LibraryRepository) : ViewModel() {
    val bookmarks: StateFlow<List<QuestionSummary>?> = library.observeBookmarks().stateIn(viewModelScope, SharingStarted.WhileSubscribed(5000), null)
    fun remove(q: QuestionSummary) = viewModelScope.launch { library.setBookmark(q, false) }
}

@HiltViewModel
class NotesViewModel @Inject constructor(private val library: LibraryRepository) : ViewModel() {
    val notes: StateFlow<List<Note>?> = library.observeNotes().stateIn(viewModelScope, SharingStarted.WhileSubscribed(5000), null)
    private val _events = Channel<QuestionEvent>(Channel.BUFFERED)
    val events = _events.receiveAsFlow()

    fun delete(note: Note) = viewModelScope.launch { library.deleteNote(note.questionId) }

    fun export() = viewModelScope.launch {
        when (val r = library.exportNotes("markdown")) {
            is AppResult.Success -> _events.send(QuestionEvent.Share(r.data))
            is AppResult.Failure -> _events.send(QuestionEvent.Message(r.error.message))
        }
    }
}

@HiltViewModel
class CollectionsViewModel @Inject constructor(private val library: LibraryRepository) : ViewModel() {
    private val _state = MutableStateFlow<Loadable<List<SavedCollection>>>(Loadable.Loading)
    val state: StateFlow<Loadable<List<SavedCollection>>> = _state
    private val _events = Channel<QuestionEvent>(Channel.BUFFERED)
    val events = _events.receiveAsFlow()

    init { load() }
    fun load() = viewModelScope.launch { _state.value = library.collections().toLoadable() }

    fun create(name: String, description: String?) = viewModelScope.launch {
        when (val r = library.createCollection(name.trim(), description?.trim()?.ifBlank { null })) {
            is AppResult.Success -> load()
            is AppResult.Failure -> _events.send(QuestionEvent.Message(r.error.message))
        }
    }

    fun delete(id: String) = viewModelScope.launch {
        when (val r = library.deleteCollection(id)) {
            is AppResult.Success -> load()
            is AppResult.Failure -> _events.send(QuestionEvent.Message(r.error.message))
        }
    }
}

@HiltViewModel
class CollectionDetailViewModel @Inject constructor(private val library: LibraryRepository, handle: SavedStateHandle) : ViewModel() {
    val route = handle.toRoute<Routes.CollectionDetail>()
    private val _state = MutableStateFlow<Loadable<SavedCollectionDetail>>(Loadable.Loading)
    val state: StateFlow<Loadable<SavedCollectionDetail>> = _state

    init { load() }
    fun load() = viewModelScope.launch { _state.value = library.collection(route.collectionId).toLoadable() }

    fun remove(item: SavedCollectionItem) = viewModelScope.launch {
        if (library.removeFromCollection(route.collectionId, item.id) is AppResult.Success) {
            _state.update { s -> (s as? Loadable.Ready)?.let { Loadable.Ready(it.data.copy(items = it.data.items - item)) } ?: s }
        }
    }
}
