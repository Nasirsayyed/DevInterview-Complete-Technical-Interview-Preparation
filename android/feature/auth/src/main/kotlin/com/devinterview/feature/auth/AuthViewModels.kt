package com.devinterview.feature.auth

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.devinterview.domain.model.AppError
import com.devinterview.domain.model.AppResult
import com.devinterview.domain.model.RegistrationForm
import com.devinterview.domain.model.Technology
import com.devinterview.domain.repository.AuthRepository
import com.devinterview.domain.repository.QuestionRepository
import com.devinterview.domain.repository.SettingsRepository
import com.devinterview.domain.usecase.FormValidator
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.flow.*
import kotlinx.coroutines.launch
import javax.inject.Inject

sealed interface AuthEvent {
    data object SignedIn : AuthEvent
    data class VerifyEmail(val email: String) : AuthEvent
    data class Message(val text: String) : AuthEvent
}

/** Decides where the app starts: onboarding, sign-in or home. */
@HiltViewModel
class SplashViewModel @Inject constructor(auth: AuthRepository, settings: SettingsRepository) : ViewModel() {
    sealed interface Destination { data object Onboarding : Destination; data object Login : Destination; data object Home : Destination }

    val destination: StateFlow<Destination?> = combine(settings.onboardingCompleted, auth.currentUser) { onboarded, user ->
        when { !onboarded -> Destination.Onboarding; user == null -> Destination.Login; else -> Destination.Home }
    }.stateIn(viewModelScope, SharingStarted.Eagerly, null)
}

@HiltViewModel
class OnboardingViewModel @Inject constructor(private val settings: SettingsRepository) : ViewModel() {
    fun complete(then: () -> Unit) = viewModelScope.launch { settings.setOnboardingCompleted(); then() }
}

data class LoginUiState(
    val email: String = "", val password: String = "", val emailError: String? = null, val passwordError: String? = null,
    val loading: Boolean = false, val error: String? = null,
)

@HiltViewModel
class LoginViewModel @Inject constructor(private val auth: AuthRepository) : ViewModel() {
    private val _state = MutableStateFlow(LoginUiState())
    val state: StateFlow<LoginUiState> = _state
    private val _events = Channel<AuthEvent>(Channel.BUFFERED)
    val events = _events.receiveAsFlow()

    fun onEmail(v: String) = _state.update { it.copy(email = v, emailError = null, error = null) }
    fun onPassword(v: String) = _state.update { it.copy(password = v, passwordError = null, error = null) }

    fun login() {
        val s = _state.value
        val emailError = FormValidator.email(s.email)
        val passwordError = if (s.password.isBlank()) "Password is required." else null
        if (emailError != null || passwordError != null) {
            _state.update { it.copy(emailError = emailError, passwordError = passwordError) }; return
        }
        _state.update { it.copy(loading = true, error = null) }
        viewModelScope.launch {
            when (val r = auth.login(s.email, s.password)) {
                is AppResult.Success -> _events.send(AuthEvent.SignedIn)
                is AppResult.Failure -> {
                    val err = r.error
                    if (err is AppError.Server && err.code == "email_not_verified") {
                        auth.resendVerification(s.email)
                        _events.send(AuthEvent.VerifyEmail(s.email.trim()))
                    } else _state.update { it.copy(error = err.message) }
                }
            }
            _state.update { it.copy(loading = false) }
        }
    }

    fun googleSignIn(idToken: String) {
        _state.update { it.copy(loading = true, error = null) }
        viewModelScope.launch {
            when (val r = auth.loginWithGoogle(idToken)) {
                is AppResult.Success -> _events.send(AuthEvent.SignedIn)
                is AppResult.Failure -> _state.update { it.copy(error = r.error.message) }
            }
            _state.update { it.copy(loading = false) }
        }
    }

    fun googleFailed(message: String) = _state.update { it.copy(error = message) }
}

data class RegisterUiState(
    val step: Int = 0, val firstName: String = "", val lastName: String = "", val email: String = "", val password: String = "",
    val confirmPassword: String = "", val experienceLevel: String = "Intermediate", val currentRole: String = "", val targetRole: String = "",
    val technologies: List<Technology> = emptyList(), val selectedTechnologies: Set<String> = emptySet(),
    val errors: Map<String, String> = emptyMap(), val loading: Boolean = false, val error: String? = null,
)

@HiltViewModel
class RegisterViewModel @Inject constructor(private val auth: AuthRepository, private val questions: QuestionRepository) : ViewModel() {
    private val _state = MutableStateFlow(RegisterUiState())
    val state: StateFlow<RegisterUiState> = _state
    private val _events = Channel<AuthEvent>(Channel.BUFFERED)
    val events = _events.receiveAsFlow()

    init {
        // Technologies are public catalogue data, but the endpoint requires auth; registration works without them.
        viewModelScope.launch { (questions.technologies() as? AppResult.Success)?.let { r -> _state.update { it.copy(technologies = r.data) } } }
    }

    fun update(transform: (RegisterUiState) -> RegisterUiState) = _state.update { transform(it).copy(error = null) }
    fun toggleTechnology(id: String) = _state.update { s -> s.copy(selectedTechnologies = if (id in s.selectedTechnologies) s.selectedTechnologies - id else s.selectedTechnologies + id) }

    private fun form(s: RegisterUiState) = RegistrationForm(
        s.firstName.trim(), s.lastName.trim(), s.email.trim(), s.password, s.confirmPassword, s.experienceLevel,
        s.currentRole.trim().ifBlank { null }, s.targetRole.trim().ifBlank { null }, s.selectedTechnologies.toList(),
    )

    fun next() {
        val s = _state.value
        val errors = FormValidator.registration(form(s))
        val stepFields = setOf("firstName", "lastName", "email", "password", "confirmPassword")
        val relevant = if (s.step == 0) errors.filterKeys { it in stepFields } else errors
        if (relevant.isNotEmpty()) { _state.update { it.copy(errors = relevant) }; return }
        if (s.step == 0) _state.update { it.copy(step = 1, errors = emptyMap()) } else submit()
    }

    fun back() = _state.update { it.copy(step = 0) }

    private fun submit() {
        val s = _state.value
        _state.update { it.copy(loading = true) }
        viewModelScope.launch {
            when (val r = auth.register(form(s))) {
                is AppResult.Success -> if (r.data.emailVerificationRequired) _events.send(AuthEvent.VerifyEmail(s.email.trim())) else _events.send(AuthEvent.SignedIn)
                is AppResult.Failure -> {
                    val err = r.error
                    val fieldErrors = (err as? AppError.Validation)?.fields?.mapValues { it.value.first() }.orEmpty()
                    _state.update { it.copy(error = err.message, errors = fieldErrors, step = if (fieldErrors.keys.any { k -> k in setOf("email", "password") }) 0 else it.step) }
                }
            }
            _state.update { it.copy(loading = false) }
        }
    }
}

data class CodeUiState(val code: String = "", val loading: Boolean = false, val error: String? = null, val info: String? = null)

@HiltViewModel
class VerifyEmailViewModel @Inject constructor(private val auth: AuthRepository) : ViewModel() {
    private val _state = MutableStateFlow(CodeUiState())
    val state: StateFlow<CodeUiState> = _state
    private val _events = Channel<AuthEvent>(Channel.BUFFERED)
    val events = _events.receiveAsFlow()

    fun onCode(v: String) = _state.update { it.copy(code = v.filter(Char::isDigit).take(6), error = null) }

    fun verify(email: String) {
        FormValidator.code(_state.value.code)?.let { e -> _state.update { it.copy(error = e) }; return }
        _state.update { it.copy(loading = true) }
        viewModelScope.launch {
            when (val r = auth.verifyEmail(email, _state.value.code)) {
                is AppResult.Success -> _events.send(AuthEvent.SignedIn)
                is AppResult.Failure -> _state.update { it.copy(error = r.error.message) }
            }
            _state.update { it.copy(loading = false) }
        }
    }

    fun resend(email: String) = viewModelScope.launch {
        auth.resendVerification(email)
        _state.update { it.copy(info = "A new code has been sent to $email.") }
    }
}

data class ForgotUiState(
    val email: String = "", val code: String = "", val password: String = "", val confirm: String = "", val codeSent: Boolean = false,
    val loading: Boolean = false, val errors: Map<String, String> = emptyMap(), val error: String? = null,
)

@HiltViewModel
class ForgotPasswordViewModel @Inject constructor(private val auth: AuthRepository) : ViewModel() {
    private val _state = MutableStateFlow(ForgotUiState())
    val state: StateFlow<ForgotUiState> = _state
    private val _events = Channel<AuthEvent>(Channel.BUFFERED)
    val events = _events.receiveAsFlow()

    fun update(transform: (ForgotUiState) -> ForgotUiState) = _state.update { transform(it).copy(error = null, errors = emptyMap()) }

    fun sendCode() {
        val s = _state.value
        FormValidator.email(s.email)?.let { e -> _state.update { it.copy(errors = mapOf("email" to e)) }; return }
        _state.update { it.copy(loading = true) }
        viewModelScope.launch {
            when (val r = auth.forgotPassword(s.email)) {
                is AppResult.Success -> _state.update { it.copy(codeSent = true) }
                is AppResult.Failure -> _state.update { it.copy(error = r.error.message) }
            }
            _state.update { it.copy(loading = false) }
        }
    }

    fun reset() {
        val s = _state.value
        val errors = buildMap {
            FormValidator.code(s.code)?.let { put("code", it) }
            FormValidator.password(s.password)?.let { put("password", it) }
            if (s.confirm != s.password) put("confirm", "Passwords do not match.")
        }
        if (errors.isNotEmpty()) { _state.update { it.copy(errors = errors) }; return }
        _state.update { it.copy(loading = true) }
        viewModelScope.launch {
            when (val r = auth.resetPassword(s.email, s.code, s.password)) {
                is AppResult.Success -> _events.send(AuthEvent.Message("Password updated. Please sign in."))
                is AppResult.Failure -> _state.update { it.copy(error = r.error.message) }
            }
            _state.update { it.copy(loading = false) }
        }
    }
}
