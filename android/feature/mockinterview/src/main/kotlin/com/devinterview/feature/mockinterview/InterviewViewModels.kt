package com.devinterview.feature.mockinterview

import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import androidx.navigation.toRoute
import com.devinterview.core.designsystem.Loadable
import com.devinterview.core.designsystem.toLoadable
import com.devinterview.core.navigation.Routes
import com.devinterview.domain.model.*
import com.devinterview.domain.repository.AiRepository
import com.devinterview.domain.repository.InterviewRepository
import com.devinterview.domain.repository.LibraryRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.async
import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.flow.*
import kotlinx.coroutines.launch
import javax.inject.Inject

val InterviewTypes = listOf(
    "Technical" to "Technical", "Hr" to "HR / behavioural", "Coding" to "Coding", "Sql" to "SQL", "DotNetDeveloper" to ".NET developer",
    "ReactDeveloper" to "React developer", "FullStackDeveloper" to "Full-stack developer", "SystemDesign" to "System design", "Managerial" to "Managerial",
)
val Levels = listOf("Beginner", "Intermediate", "Advanced", "Expert")
val ReviewLanguages = listOf("csharp" to "C#", "javascript" to "JavaScript", "typescript" to "TypeScript", "python" to "Python", "sql" to "SQL",
    "java" to "Java", "kotlin" to "Kotlin", "cpp" to "C++")

fun interviewTypeLabel(type: String) = InterviewTypes.firstOrNull { it.first == type }?.second ?: type

sealed interface AiEvent {
    data class Open(val route: Any) : AiEvent
    data class Message(val text: String) : AiEvent
}

data class InterviewHomeData(val status: AiStatus?, val interviews: List<InterviewSummary>, val evaluations: List<EvaluationSummary>)

@HiltViewModel
class InterviewHomeViewModel @Inject constructor(private val interviews: InterviewRepository, private val ai: AiRepository) : ViewModel() {
    private val _state = MutableStateFlow<Loadable<InterviewHomeData>>(Loadable.Loading)
    val state: StateFlow<Loadable<InterviewHomeData>> = _state

    init { load() }

    fun load() = viewModelScope.launch {
        if (_state.value !is Loadable.Ready) _state.value = Loadable.Loading
        val status = async { ai.status() }
        val evals = async { ai.evaluations(1) }
        _state.value = when (val list = interviews.list(1)) {
            is AppResult.Success -> Loadable.Ready(InterviewHomeData(status.await().getOrNull(), list.data.items, evals.await().getOrNull()?.items.orEmpty()))
            is AppResult.Failure -> Loadable.Failed(list.error)
        }
    }
}

data class InterviewSetupUiState(
    val type: String = "Technical", val technology: String = "C#", val level: String = "Intermediate", val difficulty: String = "Intermediate",
    val durationMinutes: Int = 30, val questionCount: Int = 6, val targetRole: String = "Software Engineer",
    val starting: Boolean = false, val error: AppError? = null,
)

@HiltViewModel
class InterviewSetupViewModel @Inject constructor(private val interviews: InterviewRepository, handle: SavedStateHandle) : ViewModel() {
    private val _state = MutableStateFlow(InterviewSetupUiState(type = handle.toRoute<Routes.InterviewSetup>().type ?: "Technical"))
    val state: StateFlow<InterviewSetupUiState> = _state
    private val _events = Channel<AiEvent>(Channel.BUFFERED)
    val events = _events.receiveAsFlow()

    fun update(transform: (InterviewSetupUiState) -> InterviewSetupUiState) = _state.update { transform(it).copy(error = null) }

    fun start() {
        val s = _state.value
        if (s.technology.isBlank() || s.targetRole.isBlank()) { _state.update { it.copy(error = AppError.Validation("Technology and target role are required.")) }; return }
        _state.update { it.copy(starting = true) }
        viewModelScope.launch {
            when (val r = interviews.start(StartInterview(s.type, s.technology.trim(), s.level, s.difficulty, s.durationMinutes, s.questionCount, s.targetRole.trim()))) {
                is AppResult.Success -> _events.send(AiEvent.Open(Routes.InterviewChat(r.data.id)))
                is AppResult.Failure -> _state.update { it.copy(error = r.error) }
            }
            _state.update { it.copy(starting = false) }
        }
    }
}

data class InterviewChatUiState(
    val interview: Loadable<MockInterview> = Loadable.Loading, val draft: String = "", val usedVoice: Boolean = false,
    val sending: Boolean = false, val finishing: Boolean = false, val error: String? = null,
)

/** Chat with the AI interviewer. The backend decides the next question, follow-ups, difficulty and when the interview ends. */
@HiltViewModel
class InterviewChatViewModel @Inject constructor(private val interviews: InterviewRepository, handle: SavedStateHandle) : ViewModel() {
    val interviewId = handle.toRoute<Routes.InterviewChat>().interviewId
    private val _state = MutableStateFlow(InterviewChatUiState())
    val state: StateFlow<InterviewChatUiState> = _state
    private val _events = Channel<AiEvent>(Channel.BUFFERED)
    val events = _events.receiveAsFlow()
    private var questionShownAt = System.currentTimeMillis()

    init { load() }

    fun load() = viewModelScope.launch {
        _state.update { it.copy(interview = Loadable.Loading) }
        val r = interviews.get(interviewId)
        _state.update { it.copy(interview = r.toLoadable()) }
        questionShownAt = System.currentTimeMillis()
        (r as? AppResult.Success)?.data?.takeIf { it.status != "InProgress" }?.let { _events.send(AiEvent.Open(Routes.InterviewReport(it.id))) }
    }

    fun onDraft(text: String) = _state.update { it.copy(draft = text.take(6000)) }
    fun onVoiceText(text: String) = _state.update { it.copy(draft = (it.draft + " " + text).trim().take(6000), usedVoice = true) }

    fun send() {
        val s = _state.value
        val interview = s.interview.dataOrNull ?: return
        val question = interview.currentQuestion ?: return
        if (s.draft.isBlank() || s.sending) return
        val seconds = ((System.currentTimeMillis() - questionShownAt) / 1000).toInt()
        // Show the answer immediately; the server response replaces the transcript.
        val optimistic = interview.copy(messages = interview.messages + InterviewMessage("candidate", s.draft.trim()))
        _state.update { it.copy(interview = Loadable.Ready(optimistic), sending = true, error = null) }
        viewModelScope.launch {
            when (val r = interviews.answer(interviewId, question.id, s.draft.trim(), seconds, s.usedVoice)) {
                is AppResult.Success -> {
                    questionShownAt = System.currentTimeMillis()
                    _state.update { it.copy(interview = Loadable.Ready(r.data), draft = "", usedVoice = false, sending = false) }
                    if (r.data.status != "InProgress" || r.data.currentQuestion == null) finish()
                }
                is AppResult.Failure -> _state.update { it.copy(interview = Loadable.Ready(interview), sending = false, error = r.error.message) }
            }
        }
    }

    fun skip() {
        if (_state.value.sending) return
        _state.update { it.copy(sending = true, error = null) }
        viewModelScope.launch {
            when (val r = interviews.skip(interviewId)) {
                is AppResult.Success -> {
                    questionShownAt = System.currentTimeMillis()
                    _state.update { it.copy(interview = Loadable.Ready(r.data), sending = false) }
                    if (r.data.status != "InProgress" || r.data.currentQuestion == null) finish()
                }
                is AppResult.Failure -> _state.update { it.copy(sending = false, error = r.error.message) }
            }
        }
    }

    fun finish() {
        if (_state.value.finishing) return
        _state.update { it.copy(finishing = true, error = null) }
        viewModelScope.launch {
            when (val r = interviews.complete(interviewId)) {
                is AppResult.Success -> _events.send(AiEvent.Open(Routes.InterviewReport(interviewId)))
                is AppResult.Failure -> _state.update { it.copy(finishing = false, error = r.error.message) }
            }
        }
    }

    fun clearError() = _state.update { it.copy(error = null) }
}

@HiltViewModel
class InterviewReportViewModel @Inject constructor(private val interviews: InterviewRepository, handle: SavedStateHandle) : ViewModel() {
    private val id = handle.toRoute<Routes.InterviewReport>().interviewId
    private val _state = MutableStateFlow<Loadable<InterviewReport>>(Loadable.Loading)
    val state: StateFlow<Loadable<InterviewReport>> = _state

    init { load() }
    fun load() = viewModelScope.launch { _state.value = Loadable.Loading; _state.value = interviews.report(id).toLoadable() }
}

data class EvaluatorUiState(
    val questionId: String? = null, val questionText: String = "", val answer: String = "", val evaluating: Boolean = false,
    val result: AnswerEvaluation? = null, val error: AppError? = null,
)

@HiltViewModel
class AnswerEvaluatorViewModel @Inject constructor(private val ai: AiRepository, private val library: LibraryRepository, handle: SavedStateHandle) : ViewModel() {
    private val route = handle.toRoute<Routes.AnswerEvaluator>()
    private val _state = MutableStateFlow(EvaluatorUiState(questionId = route.questionId, questionText = route.questionText.orEmpty()))
    val state: StateFlow<EvaluatorUiState> = _state
    private val _events = Channel<AiEvent>(Channel.BUFFERED)
    val events = _events.receiveAsFlow()

    fun onQuestion(v: String) = _state.update { it.copy(questionText = v.take(4000), questionId = null, error = null) }
    fun onAnswer(v: String) = _state.update { it.copy(answer = v.take(12000), error = null) }
    fun reset() = _state.update { it.copy(result = null, answer = "") }

    fun evaluate() {
        val s = _state.value
        if (s.answer.isBlank() || (s.questionId == null && s.questionText.isBlank())) {
            _state.update { it.copy(error = AppError.Validation("Enter the question and your answer.")) }; return
        }
        _state.update { it.copy(evaluating = true, error = null) }
        viewModelScope.launch {
            when (val r = ai.evaluateAnswer(s.questionId, s.questionText.ifBlank { null }, s.answer.trim())) {
                is AppResult.Success -> _state.update { it.copy(result = r.data, evaluating = false) }
                is AppResult.Failure -> _state.update { it.copy(error = r.error, evaluating = false) }
            }
        }
    }

    suspend fun collections() = library.collections().getOrNull().orEmpty()

    fun saveToCollection(collectionId: String) = viewModelScope.launch {
        val id = _state.value.result?.id ?: return@launch
        val msg = when (val r = library.addToCollection(collectionId, "AnswerEvaluation", id)) { is AppResult.Success -> "Saved to collection"; is AppResult.Failure -> r.error.message }
        _events.send(AiEvent.Message(msg))
    }
}

@HiltViewModel
class EvaluationDetailViewModel @Inject constructor(private val ai: AiRepository, handle: SavedStateHandle) : ViewModel() {
    private val id = handle.toRoute<Routes.EvaluationDetail>().evaluationId
    private val _state = MutableStateFlow<Loadable<AnswerEvaluation>>(Loadable.Loading)
    val state: StateFlow<Loadable<AnswerEvaluation>> = _state

    init { load() }
    fun load() = viewModelScope.launch { _state.value = Loadable.Loading; _state.value = ai.evaluation(id).toLoadable() }
}

data class CodeReviewUiState(
    val language: String = "csharp", val code: String = "", val context: String = "", val reviewing: Boolean = false,
    val result: CodeReview? = null, val error: AppError? = null,
)

@HiltViewModel
class CodeReviewerViewModel @Inject constructor(private val ai: AiRepository) : ViewModel() {
    private val _state = MutableStateFlow(CodeReviewUiState())
    val state: StateFlow<CodeReviewUiState> = _state

    fun update(transform: (CodeReviewUiState) -> CodeReviewUiState) = _state.update { transform(it).copy(error = null) }

    fun review() {
        val s = _state.value
        if (s.code.isBlank()) { _state.update { it.copy(error = AppError.Validation("Paste the code you want reviewed.")) }; return }
        _state.update { it.copy(reviewing = true, error = null) }
        viewModelScope.launch {
            when (val r = ai.reviewCode(s.language, s.code, s.context.ifBlank { null })) {
                is AppResult.Success -> _state.update { it.copy(result = r.data, reviewing = false) }
                is AppResult.Failure -> _state.update { it.copy(error = r.error, reviewing = false) }
            }
        }
    }
}
