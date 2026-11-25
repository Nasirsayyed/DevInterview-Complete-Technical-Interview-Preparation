package com.devinterview.feature.coding

import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import androidx.navigation.toRoute
import com.devinterview.core.designsystem.Loadable
import com.devinterview.core.designsystem.toLoadable
import com.devinterview.core.navigation.Routes
import com.devinterview.domain.model.*
import com.devinterview.domain.repository.CodingRepository
import com.devinterview.domain.repository.LibraryRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.FlowPreview
import kotlinx.coroutines.Job
import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.flow.*
import kotlinx.coroutines.launch
import java.util.UUID
import javax.inject.Inject

val ChallengeCategories = listOf(
    "Arrays", "Strings", "LinkedLists", "Stacks", "Queues", "Trees", "Graphs", "Sorting", "Searching", "Recursion",
    "DynamicProgramming", "Hashing", "Mathematics", "Sql",
)

fun String.humanize() = replace(Regex("([a-z])([A-Z])"), "$1 $2")

data class ChallengeListUiState(
    val items: List<ChallengeSummary> = emptyList(), val category: String? = null, val difficulty: String? = null, val status: String? = null,
    val search: String = "", val page: Int = 0, val hasNext: Boolean = true, val loading: Boolean = false, val error: AppError? = null,
)

@OptIn(FlowPreview::class)
@HiltViewModel
class ChallengeListViewModel @Inject constructor(private val repo: CodingRepository, handle: SavedStateHandle) : ViewModel() {
    private val _state = MutableStateFlow(ChallengeListUiState(category = handle.toRoute<Routes.ChallengeList>().category))
    val state: StateFlow<ChallengeListUiState> = _state
    private var loadJob: Job? = null

    init {
        viewModelScope.launch {
            _state.map { listOf(it.category, it.difficulty, it.status, it.search.trim()) }.distinctUntilChanged().debounce(300).collect { reload() }
        }
    }

    fun update(transform: (ChallengeListUiState) -> ChallengeListUiState) = _state.update(transform)

    private fun reload() {
        loadJob?.cancel()
        _state.update { it.copy(items = emptyList(), page = 0, hasNext = true, loading = false, error = null) }
        loadMore()
    }

    fun retry() = reload()

    fun loadMore() {
        val s = _state.value
        if (s.loading || !s.hasNext) return
        _state.update { it.copy(loading = true, error = null) }
        loadJob = viewModelScope.launch {
            when (val r = repo.challenges(s.category, s.difficulty, s.status, s.search.trim().ifBlank { null }, s.page + 1)) {
                is AppResult.Success -> _state.update { it.copy(items = it.items + r.data.items, page = r.data.page, hasNext = r.data.hasNext, loading = false) }
                is AppResult.Failure -> _state.update { it.copy(loading = false, error = r.error) }
            }
        }
    }
}

data class ChallengeDetailUiState(
    val challenge: Loadable<ChallengeDetail> = Loadable.Loading, val hints: List<Hint> = emptyList(), val solution: Solution? = null,
    val loadingHint: Boolean = false, val loadingSolution: Boolean = false, val message: String? = null,
)

@HiltViewModel
class ChallengeDetailViewModel @Inject constructor(
    private val repo: CodingRepository, private val library: LibraryRepository, handle: SavedStateHandle,
) : ViewModel() {
    val challengeId = handle.toRoute<Routes.ChallengeDetail>().challengeId
    private val _state = MutableStateFlow(ChallengeDetailUiState())
    val state: StateFlow<ChallengeDetailUiState> = _state

    init { load() }
    fun load() = viewModelScope.launch { _state.update { it.copy(challenge = Loadable.Loading) }; _state.update { it.copy(challenge = repo.challenge(challengeId).toLoadable()) } }

    fun nextHint() {
        val c = _state.value.challenge.dataOrNull ?: return
        val index = _state.value.hints.size
        if (index >= c.hintCount) return
        _state.update { it.copy(loadingHint = true) }
        viewModelScope.launch {
            when (val r = repo.hint(challengeId, index)) {
                is AppResult.Success -> _state.update { it.copy(hints = it.hints + r.data, loadingHint = false) }
                is AppResult.Failure -> _state.update { it.copy(loadingHint = false, message = r.error.message) }
            }
        }
    }

    fun showSolution() {
        _state.update { it.copy(loadingSolution = true) }
        viewModelScope.launch {
            when (val r = repo.solution(challengeId)) {
                is AppResult.Success -> _state.update { it.copy(solution = r.data, loadingSolution = false) }
                is AppResult.Failure -> _state.update { it.copy(loadingSolution = false, message = r.error.message) }
            }
        }
    }

    fun saveToCollection(collectionId: String) = viewModelScope.launch {
        val msg = when (val r = library.addToCollection(collectionId, "CodingChallenge", challengeId)) { is AppResult.Success -> "Saved to collection"; is AppResult.Failure -> r.error.message }
        _state.update { it.copy(message = msg) }
    }

    suspend fun collections() = library.collections().getOrNull().orEmpty()
    fun clearMessage() = _state.update { it.copy(message = null) }
}

data class EditorUiState(
    val challenge: Loadable<ChallengeDetail> = Loadable.Loading,
    val languages: List<Language> = emptyList(),
    val language: String = "",
    val code: String = "",
    val customInput: String = "",
    val useCustomInput: Boolean = false,
    val running: Boolean = false,
    val submitting: Boolean = false,
    val runResult: RunResult? = null,
    val submission: Submission? = null,
    val error: AppError? = null,
)

/**
 * Code editor. Code is drafted locally per language (autosaved). Run and Submit send code to the backend, which executes
 * it in an isolated sandbox; the app never executes or grades code itself.
 */
@OptIn(FlowPreview::class)
@HiltViewModel
class CodeEditorViewModel @Inject constructor(private val repo: CodingRepository, handle: SavedStateHandle) : ViewModel() {
    private val challengeId = handle.toRoute<Routes.CodeEditor>().challengeId
    private val _state = MutableStateFlow(EditorUiState())
    val state: StateFlow<EditorUiState> = _state
    private val _events = Channel<String>(Channel.BUFFERED)
    val events = _events.receiveAsFlow()
    /** One idempotency key per distinct submit, so a retried request after a timeout is not double-counted. */
    private var pendingSubmitKey: Pair<String, String>? = null

    init {
        load()
        viewModelScope.launch {
            _state.map { it.language to it.code }.distinctUntilChanged().debounce(800).collect { (lang, code) ->
                if (lang.isNotBlank() && _state.value.challenge is Loadable.Ready) repo.saveDraft(challengeId, lang, code)
            }
        }
    }

    fun load() = viewModelScope.launch {
        _state.update { it.copy(challenge = Loadable.Loading) }
        val langs = repo.languages().getOrNull().orEmpty()
        when (val r = repo.challenge(challengeId)) {
            is AppResult.Success -> {
                val c = r.data
                val available = langs.filter { it.code in c.allowedLanguages }.ifEmpty { c.allowedLanguages.map { Language(it, it, "") } }
                val lang = available.firstOrNull()?.code.orEmpty()
                _state.update { it.copy(challenge = Loadable.Ready(c), languages = available, language = lang, code = repo.draft(challengeId, lang) ?: c.starterCode[lang].orEmpty()) }
            }
            is AppResult.Failure -> _state.update { it.copy(challenge = Loadable.Failed(r.error)) }
        }
    }

    fun selectLanguage(code: String) = viewModelScope.launch {
        val c = _state.value.challenge.dataOrNull ?: return@launch
        repo.saveDraft(challengeId, _state.value.language, _state.value.code)
        _state.update { it.copy(language = code, code = repo.draft(challengeId, code) ?: c.starterCode[code].orEmpty(), runResult = null, submission = null) }
    }

    fun onCode(code: String) { if (code.length <= 64_000) _state.update { it.copy(code = code) } }
    fun onCustomInput(v: String) = _state.update { it.copy(customInput = v.take(10_000)) }
    fun toggleCustomInput(v: Boolean) = _state.update { it.copy(useCustomInput = v) }

    fun resetCode() {
        val c = _state.value.challenge.dataOrNull ?: return
        _state.update { it.copy(code = c.starterCode[it.language].orEmpty()) }
    }

    fun run() {
        val s = _state.value
        if (s.running || s.submitting || s.code.isBlank()) return
        _state.update { it.copy(running = true, error = null, submission = null) }
        viewModelScope.launch {
            when (val r = repo.run(challengeId, s.language, s.code, s.customInput.takeIf { s.useCustomInput })) {
                is AppResult.Success -> _state.update { it.copy(runResult = r.data, running = false) }
                is AppResult.Failure -> _state.update { it.copy(error = r.error, running = false) }
            }
        }
    }

    fun submit() {
        val s = _state.value
        if (s.running || s.submitting || s.code.isBlank()) return
        val fingerprint = s.language + ":" + s.code.hashCode()
        val key = pendingSubmitKey?.takeIf { it.first == fingerprint }?.second ?: UUID.randomUUID().toString()
        pendingSubmitKey = fingerprint to key
        _state.update { it.copy(submitting = true, error = null, runResult = null) }
        viewModelScope.launch {
            when (val r = repo.submit(challengeId, s.language, s.code, key)) {
                is AppResult.Success -> {
                    pendingSubmitKey = null
                    _state.update { it.copy(submission = r.data, submitting = false) }
                    if (r.data.status == "Accepted") _events.send(if (r.data.xpAwarded > 0) "Accepted! +${r.data.xpAwarded} XP" else "Accepted!")
                }
                is AppResult.Failure -> _state.update { it.copy(error = r.error, submitting = false) }
            }
        }
    }
}

data class SubmissionsUiState(val items: List<Submission> = emptyList(), val page: Int = 0, val hasNext: Boolean = true, val loading: Boolean = false, val error: AppError? = null)

@HiltViewModel
class SubmissionsViewModel @Inject constructor(private val repo: CodingRepository, handle: SavedStateHandle) : ViewModel() {
    private val challengeId = handle.toRoute<Routes.Submissions>().challengeId
    private val _state = MutableStateFlow(SubmissionsUiState())
    val state: StateFlow<SubmissionsUiState> = _state

    init { loadMore() }
    fun refresh() { _state.value = SubmissionsUiState(); loadMore() }

    fun loadMore() {
        val s = _state.value
        if (s.loading || !s.hasNext) return
        _state.update { it.copy(loading = true, error = null) }
        viewModelScope.launch {
            when (val r = repo.submissions(challengeId, s.page + 1)) {
                is AppResult.Success -> _state.update { it.copy(items = it.items + r.data.items, page = r.data.page, hasNext = r.data.hasNext, loading = false) }
                is AppResult.Failure -> _state.update { it.copy(loading = false, error = r.error) }
            }
        }
    }
}

@HiltViewModel
class SubmissionDetailViewModel @Inject constructor(private val repo: CodingRepository, handle: SavedStateHandle) : ViewModel() {
    private val id = handle.toRoute<Routes.SubmissionDetail>().submissionId
    private val _state = MutableStateFlow<Loadable<Submission>>(Loadable.Loading)
    val state: StateFlow<Loadable<Submission>> = _state

    init { load() }
    fun load() = viewModelScope.launch { _state.value = Loadable.Loading; _state.value = repo.submission(id).toLoadable() }
}

/** Shared by the console and submission views. */
fun submissionStatusLabel(status: String) = when (status) {
    "Accepted" -> "Accepted"; "WrongAnswer" -> "Wrong answer"; "RuntimeError" -> "Runtime error"; "CompilationError" -> "Compilation error"
    "TimeLimitExceeded" -> "Time limit exceeded"; "MemoryLimitExceeded" -> "Memory limit exceeded"; "InternalError" -> "Execution unavailable"
    "Pending" -> "Pending"; else -> status.humanize()
}

