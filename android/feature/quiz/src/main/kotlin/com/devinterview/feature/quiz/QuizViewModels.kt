package com.devinterview.feature.quiz

import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import androidx.navigation.toRoute
import com.devinterview.core.designsystem.Loadable
import com.devinterview.core.designsystem.toLoadable
import com.devinterview.core.navigation.Routes
import com.devinterview.domain.model.*
import com.devinterview.domain.repository.QuestionRepository
import com.devinterview.domain.repository.QuizRepository
import com.devinterview.domain.usecase.QuizProgress
import com.devinterview.domain.usecase.QuizTimer
import com.devinterview.domain.usecase.SubmitQuizUseCase
import com.devinterview.domain.usecase.quizProgress
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.Job
import kotlinx.coroutines.async
import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.*
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import java.time.Instant
import javax.inject.Inject

sealed interface QuizEvent {
    data class OpenPlayer(val quizId: String) : QuizEvent
    data class OpenResult(val attemptId: String) : QuizEvent
    data class Message(val text: String) : QuizEvent
}

data class QuizHomeData(val daily: QuizSummary?, val quizzes: List<QuizSummary>, val history: List<QuizAttemptSummary>)

@HiltViewModel
class QuizHomeViewModel @Inject constructor(private val quizzes: QuizRepository) : ViewModel() {
    private val _state = MutableStateFlow<Loadable<QuizHomeData>>(Loadable.Loading)
    val state: StateFlow<Loadable<QuizHomeData>> = _state

    init { load() }

    fun load() = viewModelScope.launch {
        if (_state.value !is Loadable.Ready) _state.value = Loadable.Loading
        val daily = async { quizzes.daily() }
        val history = async { quizzes.history(1) }
        _state.value = when (val list = quizzes.quizzes(null, null, 1)) {
            is AppResult.Success -> Loadable.Ready(QuizHomeData(daily.await().getOrNull(), list.data.items, history.await().getOrNull()?.items.orEmpty()))
            is AppResult.Failure -> Loadable.Failed(list.error)
        }
    }
}

data class QuizConfigUiState(
    val technologies: List<Technology> = emptyList(), val categories: List<Category> = emptyList(),
    val technologyId: String? = null, val categoryIds: Set<String> = emptySet(), val difficulty: String? = null,
    val questionCount: Int = 10, val timed: Boolean = true, val minutes: Int = 10, val negativeMarking: Boolean = false,
    val creating: Boolean = false, val error: String? = null,
)

@HiltViewModel
class QuizConfigViewModel @Inject constructor(
    private val quizzes: QuizRepository,
    private val questions: QuestionRepository,
    handle: SavedStateHandle,
) : ViewModel() {
    private val _state = MutableStateFlow(QuizConfigUiState(technologyId = handle.toRoute<Routes.QuizConfig>().technologyId))
    val state: StateFlow<QuizConfigUiState> = _state
    private val _events = Channel<QuizEvent>(Channel.BUFFERED)
    val events = _events.receiveAsFlow()

    init {
        viewModelScope.launch {
            questions.technologies().getOrNull()?.let { t -> _state.update { it.copy(technologies = t) } }
            _state.value.technologyId?.let(::loadCategories)
        }
    }

    private fun loadCategories(technologyId: String) = viewModelScope.launch {
        questions.categories(technologyId).getOrNull()?.let { c -> _state.update { it.copy(categories = c) } }
    }

    fun selectTechnology(id: String?) {
        _state.update { it.copy(technologyId = id, categoryIds = emptySet(), categories = emptyList()) }
        id?.let(::loadCategories)
    }

    fun update(transform: (QuizConfigUiState) -> QuizConfigUiState) = _state.update { transform(it).copy(error = null) }

    fun toggleCategory(id: String) = _state.update { s -> s.copy(categoryIds = if (id in s.categoryIds) s.categoryIds - id else s.categoryIds + id) }

    fun create() {
        val s = _state.value
        if (s.technologyId == null) { _state.update { it.copy(error = "Choose a technology.") }; return }
        _state.update { it.copy(creating = true) }
        viewModelScope.launch {
            val request = CreateQuiz(
                mode = if (s.categoryIds.isNotEmpty()) "Topic" else "Custom",
                technologyIds = listOf(s.technologyId), categoryIds = s.categoryIds.toList(), difficulty = s.difficulty,
                questionCount = s.questionCount, timeLimitSeconds = if (s.timed) s.minutes * 60 else null,
                negativeMarkingFraction = if (s.negativeMarking) 0.25 else 0.0,
            )
            when (val r = quizzes.create(request)) {
                is AppResult.Success -> _events.send(QuizEvent.OpenPlayer(r.data.id))
                is AppResult.Failure -> _state.update { it.copy(error = r.error.message) }
            }
            _state.update { it.copy(creating = false) }
        }
    }
}

data class QuizPlayerUiState(
    val attempt: Loadable<QuizAttempt> = Loadable.Loading,
    val index: Int = 0,
    val answers: Map<String, QuizAnswer> = emptyMap(),
    val remainingSeconds: Long? = null,
    val submitting: Boolean = false,
    val showSubmitDialog: Boolean = false,
    val error: String? = null,
) {
    val progress: QuizProgress? get() = attempt.dataOrNull?.let { quizProgress(it, answers) }
}

/**
 * Runs a quiz attempt. The deadline comes from the server and the countdown compensates for device clock skew; when
 * time runs out the attempt is submitted automatically (the server also ignores late answers and expires attempts).
 */
@HiltViewModel
class QuizPlayerViewModel @Inject constructor(
    private val quizzes: QuizRepository,
    private val submitQuiz: SubmitQuizUseCase,
    handle: SavedStateHandle,
) : ViewModel() {
    private val quizId = handle.toRoute<Routes.QuizPlayer>().quizId
    private val _state = MutableStateFlow(QuizPlayerUiState())
    val state: StateFlow<QuizPlayerUiState> = _state
    private val _events = Channel<QuizEvent>(Channel.BUFFERED)
    val events = _events.receiveAsFlow()
    private var timerJob: Job? = null
    private var questionShownAt = System.currentTimeMillis()

    init { start() }

    fun start() = viewModelScope.launch {
        _state.value = QuizPlayerUiState()
        when (val r = quizzes.start(quizId)) {
            is AppResult.Success -> {
                val attempt = r.data
                val answers = attempt.questions.associate { it.questionId to QuizAnswer(it.questionId, it.selectedOptionIds, it.isMarkedForReview, 0) }
                _state.value = QuizPlayerUiState(Loadable.Ready(attempt), answers = answers)
                questionShownAt = System.currentTimeMillis()
                startTimer(QuizTimer.from(attempt))
            }
            is AppResult.Failure -> _state.value = QuizPlayerUiState(Loadable.Failed(r.error))
        }
    }

    private fun startTimer(timer: QuizTimer) {
        if (!timer.isTimed) return
        timerJob?.cancel()
        timerJob = viewModelScope.launch {
            while (isActive) {
                val remaining = timer.remainingSeconds(Instant.now())
                _state.update { it.copy(remainingSeconds = remaining) }
                if (remaining == 0L) { submit(auto = true); break }
                delay(1000)
            }
        }
    }

    private fun recordTime() {
        val attempt = _state.value.attempt.dataOrNull ?: return
        val q = attempt.questions.getOrNull(_state.value.index) ?: return
        val spent = ((System.currentTimeMillis() - questionShownAt) / 1000).toInt()
        questionShownAt = System.currentTimeMillis()
        _state.update { s -> s.answers[q.questionId]?.let { a -> s.copy(answers = s.answers + (q.questionId to a.copy(timeSpentSeconds = a.timeSpentSeconds + spent))) } ?: s }
    }

    fun goTo(index: Int) {
        val count = _state.value.attempt.dataOrNull?.questions?.size ?: return
        if (index !in 0 until count) return
        recordTime()
        _state.update { it.copy(index = index) }
    }

    fun toggleOption(question: QuizQuestion, optionId: String) {
        if (_state.value.submitting) return
        _state.update { s ->
            val current = s.answers[question.questionId] ?: QuizAnswer(question.questionId, emptyList(), false, 0)
            val selected = if (question.allowsMultipleAnswers) {
                if (optionId in current.selectedOptionIds) current.selectedOptionIds - optionId else current.selectedOptionIds + optionId
            } else listOf(optionId)
            s.copy(answers = s.answers + (question.questionId to current.copy(selectedOptionIds = selected)))
        }
        persist(question.questionId)
    }

    fun toggleMark(question: QuizQuestion) {
        _state.update { s ->
            val current = s.answers[question.questionId] ?: QuizAnswer(question.questionId, emptyList(), false, 0)
            s.copy(answers = s.answers + (question.questionId to current.copy(isMarkedForReview = !current.isMarkedForReview)))
        }
        persist(question.questionId)
    }

    /** Saves progress so the attempt can resume after the app is closed; failures are harmless (submit sends everything). */
    private fun persist(questionId: String) {
        val attempt = _state.value.attempt.dataOrNull ?: return
        val answer = _state.value.answers[questionId] ?: return
        viewModelScope.launch { quizzes.saveAnswer(attempt.attemptId, answer) }
    }

    fun requestSubmit() = _state.update { it.copy(showSubmitDialog = true) }
    fun dismissSubmit() = _state.update { it.copy(showSubmitDialog = false) }

    fun submit(auto: Boolean = false) {
        val attempt = _state.value.attempt.dataOrNull ?: return
        if (_state.value.submitting) return
        recordTime()
        _state.update { it.copy(submitting = true, showSubmitDialog = false) }
        viewModelScope.launch {
            when (val r = submitQuiz(attempt, _state.value.answers)) {
                is AppResult.Success -> { timerJob?.cancel(); _events.send(QuizEvent.OpenResult(r.data.attemptId)) }
                is AppResult.Failure -> {
                    // When time ran out the server finalises the attempt itself, so the result screen can load (and retry) it.
                    if (auto) _events.send(QuizEvent.OpenResult(attempt.attemptId))
                    else _state.update { it.copy(submitting = false, error = r.error.message) }
                }
            }
        }
    }

    fun clearError() = _state.update { it.copy(error = null) }
}

@HiltViewModel
class QuizResultViewModel @Inject constructor(private val quizzes: QuizRepository, handle: SavedStateHandle) : ViewModel() {
    private val attemptId = handle.toRoute<Routes.QuizResult>().attemptId
    private val _state = MutableStateFlow<Loadable<QuizResult>>(Loadable.Loading)
    val state: StateFlow<Loadable<QuizResult>> = _state
    val reviewFilter = MutableStateFlow("all")

    init { load() }
    fun load() = viewModelScope.launch { _state.value = Loadable.Loading; _state.value = quizzes.result(attemptId).toLoadable() }
}
