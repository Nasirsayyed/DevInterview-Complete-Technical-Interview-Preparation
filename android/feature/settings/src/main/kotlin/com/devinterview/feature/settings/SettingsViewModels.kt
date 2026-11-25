package com.devinterview.feature.settings

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.devinterview.core.designsystem.Loadable
import com.devinterview.core.designsystem.toLoadable
import com.devinterview.domain.model.*
import com.devinterview.domain.repository.AuthRepository
import com.devinterview.domain.repository.NotificationRepository
import com.devinterview.domain.repository.ProfileRepository
import com.devinterview.domain.repository.SettingsRepository
import com.devinterview.domain.repository.SyncRepository
import com.devinterview.domain.usecase.FormValidator
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.flow.*
import kotlinx.coroutines.launch
import java.time.LocalDateTime
import java.time.ZoneId
import javax.inject.Inject

sealed interface SettingsEvent {
    data class Message(val text: String) : SettingsEvent
    data class Export(val json: String) : SettingsEvent
    data object SignedOut : SettingsEvent
}

data class SettingsUiState(val hasPassword: Boolean = true, val email: String = "", val busy: Boolean = false, val deleteError: String? = null)

@HiltViewModel
class SettingsViewModel @Inject constructor(
    private val settings: SettingsRepository,
    private val auth: AuthRepository,
    private val profiles: ProfileRepository,
    private val sync: SyncRepository,
) : ViewModel() {
    val themeMode = settings.themeMode.stateIn(viewModelScope, SharingStarted.Eagerly, "system")
    val notificationsEnabled = settings.notificationsEnabled.stateIn(viewModelScope, SharingStarted.Eagerly, true)
    val syncState = sync.state.stateIn(viewModelScope, SharingStarted.WhileSubscribed(5000), null)
    private val _state = MutableStateFlow(SettingsUiState())
    val state: StateFlow<SettingsUiState> = _state
    private val _events = Channel<SettingsEvent>(Channel.BUFFERED)
    val events = _events.receiveAsFlow()

    init {
        viewModelScope.launch { profiles.getProfile().getOrNull()?.let { p -> _state.update { it.copy(hasPassword = p.hasPassword, email = p.email) } } }
    }

    fun setTheme(mode: String) = viewModelScope.launch { settings.setThemeMode(mode) }
    fun setNotifications(enabled: Boolean) = viewModelScope.launch { settings.setNotificationsEnabled(enabled) }

    fun syncNow() = viewModelScope.launch {
        val msg = when (val r = sync.sync()) { is AppResult.Success -> "Everything is up to date"; is AppResult.Failure -> r.error.message }
        _events.send(SettingsEvent.Message(msg))
    }

    fun export() = viewModelScope.launch {
        _state.update { it.copy(busy = true) }
        when (val r = profiles.exportData()) {
            is AppResult.Success -> _events.send(SettingsEvent.Export(r.data))
            is AppResult.Failure -> _events.send(SettingsEvent.Message(r.error.message))
        }
        _state.update { it.copy(busy = false) }
    }

    fun logout(allDevices: Boolean) = viewModelScope.launch {
        // Push offline changes first so nothing is lost; signing out clears local data.
        sync.sync()
        auth.logout(allDevices)
        _events.send(SettingsEvent.SignedOut)
    }

    fun deleteAccount(password: String?) {
        _state.update { it.copy(busy = true, deleteError = null) }
        viewModelScope.launch {
            when (val r = profiles.deleteAccount(password?.ifBlank { null })) {
                is AppResult.Success -> { auth.logout(false); _events.send(SettingsEvent.SignedOut) }
                is AppResult.Failure -> _state.update { it.copy(deleteError = r.error.message) }
            }
            _state.update { it.copy(busy = false) }
        }
    }

    fun clearDeleteError() = _state.update { it.copy(deleteError = null) }
}

data class ChangePasswordUiState(val current: String = "", val new: String = "", val confirm: String = "", val errors: Map<String, String> = emptyMap(), val saving: Boolean = false, val error: String? = null)

@HiltViewModel
class ChangePasswordViewModel @Inject constructor(private val auth: AuthRepository) : ViewModel() {
    private val _state = MutableStateFlow(ChangePasswordUiState())
    val state: StateFlow<ChangePasswordUiState> = _state
    private val _done = Channel<Unit>(Channel.CONFLATED)
    val done = _done.receiveAsFlow()

    fun update(transform: (ChangePasswordUiState) -> ChangePasswordUiState) = _state.update { transform(it).copy(errors = emptyMap(), error = null) }

    fun save() {
        val s = _state.value
        val errors = buildMap {
            if (s.current.isBlank()) put("current", "Enter your current password.")
            FormValidator.password(s.new)?.let { put("new", it) }
            if (s.new == s.current && s.new.isNotEmpty()) put("new", "Choose a password you haven't used here.")
            if (s.confirm != s.new) put("confirm", "Passwords do not match.")
        }
        if (errors.isNotEmpty()) { _state.update { it.copy(errors = errors) }; return }
        _state.update { it.copy(saving = true) }
        viewModelScope.launch {
            when (val r = auth.changePassword(s.current, s.new)) {
                is AppResult.Success -> _done.send(Unit)
                is AppResult.Failure -> _state.update { it.copy(error = r.error.message) }
            }
            _state.update { it.copy(saving = false) }
        }
    }
}

@HiltViewModel
class NotificationSettingsViewModel @Inject constructor(private val repo: NotificationRepository) : ViewModel() {
    private val _state = MutableStateFlow<Loadable<List<NotificationPreference>>>(Loadable.Loading)
    val state: StateFlow<Loadable<List<NotificationPreference>>> = _state
    private val _events = Channel<String>(Channel.BUFFERED)
    val events = _events.receiveAsFlow()

    init { load() }
    fun load() = viewModelScope.launch { _state.value = Loadable.Loading; _state.value = repo.preferences().toLoadable() }

    fun update(pref: NotificationPreference) {
        val current = _state.value.dataOrNull ?: return
        val next = current.map { if (it.category == pref.category) pref else it }
        _state.value = Loadable.Ready(next)
        viewModelScope.launch {
            when (val r = repo.updatePreferences(next)) {
                is AppResult.Success -> _state.value = Loadable.Ready(r.data)
                is AppResult.Failure -> { _state.value = Loadable.Ready(current); _events.send(r.error.message) }
            }
        }
    }
}

data class ReminderForm(val title: String = "", val company: String = "", val notes: String = "", val dateTime: LocalDateTime = LocalDateTime.now().plusDays(1).withHour(10).withMinute(0))

@HiltViewModel
class RemindersViewModel @Inject constructor(private val repo: NotificationRepository) : ViewModel() {
    private val _state = MutableStateFlow<Loadable<List<InterviewReminder>>>(Loadable.Loading)
    val state: StateFlow<Loadable<List<InterviewReminder>>> = _state
    private val _events = Channel<String>(Channel.BUFFERED)
    val events = _events.receiveAsFlow()

    init { load() }
    fun load() = viewModelScope.launch { _state.value = repo.reminders().toLoadable() }

    fun create(form: ReminderForm) = viewModelScope.launch {
        if (form.title.isBlank()) { _events.send("Give the interview a title."); return@launch }
        val instant = form.dateTime.atZone(ZoneId.systemDefault()).toInstant()
        if (instant.isBefore(java.time.Instant.now())) { _events.send("Choose a time in the future."); return@launch }
        when (val r = repo.createReminder(form.title.trim(), form.company.trim().ifBlank { null }, instant.toString(), form.notes.trim().ifBlank { null })) {
            is AppResult.Success -> { load(); _events.send("Reminder scheduled") }
            is AppResult.Failure -> _events.send(r.error.message)
        }
    }

    fun delete(id: String) = viewModelScope.launch {
        when (val r = repo.deleteReminder(id)) { is AppResult.Success -> load(); is AppResult.Failure -> _events.send(r.error.message) }
    }
}
