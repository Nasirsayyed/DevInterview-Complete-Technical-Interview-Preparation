package com.devinterview.feature.profile

import android.app.Activity
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.devinterview.core.designsystem.Loadable
import com.devinterview.core.designsystem.toLoadable
import com.devinterview.data.billing.BillingManager
import com.devinterview.data.billing.PlayOffer
import com.devinterview.data.billing.PurchaseState
import com.devinterview.domain.model.*
import com.devinterview.domain.repository.*
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.async
import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.flow.*
import kotlinx.coroutines.launch
import javax.inject.Inject

data class ProfileData(val profile: UserProfile, val dashboard: DashboardSummary?, val subscription: SubscriptionStatus?)

@HiltViewModel
class ProfileViewModel @Inject constructor(
    private val profiles: ProfileRepository,
    private val dashboard: DashboardRepository,
    private val subscriptions: SubscriptionRepository,
) : ViewModel() {
    private val _state = MutableStateFlow<Loadable<ProfileData>>(Loadable.Loading)
    val state: StateFlow<Loadable<ProfileData>> = _state

    init { load() }

    fun load() = viewModelScope.launch {
        if (_state.value !is Loadable.Ready) _state.value = Loadable.Loading
        val d = async { dashboard.summary() }
        val sub = async { subscriptions.status() }
        _state.value = when (val p = profiles.getProfile()) {
            is AppResult.Success -> Loadable.Ready(ProfileData(p.data, d.await().getOrNull(), sub.await().getOrNull()))
            is AppResult.Failure -> Loadable.Failed(p.error)
        }
    }
}

data class EditProfileUiState(
    val loaded: Boolean = false, val loadError: AppError? = null,
    val firstName: String = "", val lastName: String = "", val experienceLevel: String = "Intermediate", val currentRole: String = "",
    val targetRole: String = "", val bio: String = "", val photoUrl: String? = null, val technologies: List<Technology> = emptyList(),
    val selected: Set<String> = emptySet(), val dailyGoal: Int = 10, val weeklyGoalMinutes: Int = 180, val showOnLeaderboard: Boolean = true,
    val timeZoneId: String = "UTC", val saving: Boolean = false, val uploading: Boolean = false, val errors: Map<String, String> = emptyMap(),
)

@HiltViewModel
class EditProfileViewModel @Inject constructor(private val profiles: ProfileRepository, private val questions: QuestionRepository) : ViewModel() {
    private val _state = MutableStateFlow(EditProfileUiState())
    val state: StateFlow<EditProfileUiState> = _state
    private val _events = Channel<String>(Channel.BUFFERED)
    val events = _events.receiveAsFlow()
    private val _saved = Channel<Unit>(Channel.CONFLATED)
    val saved = _saved.receiveAsFlow()

    init { load() }

    fun load() = viewModelScope.launch {
        _state.update { it.copy(loadError = null) }
        val techs = async { questions.technologies().getOrNull().orEmpty() }
        when (val r = profiles.getProfile()) {
            is AppResult.Success -> r.data.let { p ->
                _state.value = EditProfileUiState(
                    loaded = true, firstName = p.firstName, lastName = p.lastName, experienceLevel = p.experienceLevel,
                    currentRole = p.currentJobRole.orEmpty(), targetRole = p.targetJobRole.orEmpty(), bio = p.bio.orEmpty(), photoUrl = p.profilePhotoUrl,
                    technologies = techs.await(), selected = p.preferredTechnologies.map { it.id }.toSet(), dailyGoal = p.dailyQuestionGoal,
                    weeklyGoalMinutes = p.weeklyGoalMinutes, showOnLeaderboard = p.showOnLeaderboard,
                    // The device zone drives streak/day boundaries on the server.
                    timeZoneId = java.util.TimeZone.getDefault().id,
                )
            }
            is AppResult.Failure -> _state.update { it.copy(loadError = r.error) }
        }
    }

    fun update(transform: (EditProfileUiState) -> EditProfileUiState) = _state.update { transform(it).copy(errors = emptyMap()) }
    fun toggle(id: String) = _state.update { s -> s.copy(selected = if (id in s.selected) s.selected - id else if (s.selected.size < 20) s.selected + id else s.selected) }

    fun save() {
        val s = _state.value
        val errors = buildMap {
            if (s.firstName.isBlank()) put("firstName", "First name is required.")
            if (s.lastName.isBlank()) put("lastName", "Last name is required.")
            if (s.bio.length > 1000) put("bio", "Bio must be at most 1000 characters.")
        }
        if (errors.isNotEmpty()) { _state.update { it.copy(errors = errors) }; return }
        _state.update { it.copy(saving = true) }
        viewModelScope.launch {
            val profile = profiles.updateProfile(s.firstName.trim(), s.lastName.trim(), s.experienceLevel, s.currentRole.trim().ifBlank { null },
                s.targetRole.trim().ifBlank { null }, s.bio.trim().ifBlank { null }, s.selected.toList())
            val prefs = if (profile is AppResult.Success) profiles.updatePreferences(s.timeZoneId, s.dailyGoal, s.weeklyGoalMinutes, s.showOnLeaderboard) else profile
            _state.update { it.copy(saving = false) }
            when (prefs) {
                is AppResult.Success -> { _events.send("Profile updated"); _saved.send(Unit) }
                is AppResult.Failure -> {
                    val fields = (prefs.error as? AppError.Validation)?.fields?.mapValues { it.value.first() }.orEmpty()
                    _state.update { it.copy(errors = fields) }
                    _events.send(prefs.error.message)
                }
            }
        }
    }

    fun uploadPhoto(bytes: ByteArray, mimeType: String) {
        if (bytes.size > 5 * 1024 * 1024) { viewModelScope.launch { _events.send("Choose an image smaller than 5 MB.") }; return }
        _state.update { it.copy(uploading = true) }
        viewModelScope.launch {
            when (val r = profiles.uploadPhoto(bytes, mimeType)) {
                is AppResult.Success -> _state.update { it.copy(photoUrl = r.data, uploading = false) }
                is AppResult.Failure -> { _state.update { it.copy(uploading = false) }; _events.send(r.error.message) }
            }
        }
    }
}

data class SubscriptionUiState(
    val status: Loadable<SubscriptionStatus> = Loadable.Loading,
    val plans: List<SubscriptionPlan> = emptyList(),
    val offers: List<PlayOffer> = emptyList(),
    val billingError: String? = null,
    val history: List<PurchaseHistoryItem> = emptyList(),
    val restoring: Boolean = false,
)

/**
 * Plans come from the backend; prices come from Google Play. Purchases are completed in Google Play and then verified
 * server-side — the app only displays the entitlement the backend returns.
 */
@HiltViewModel
class SubscriptionViewModel @Inject constructor(
    private val subscriptions: SubscriptionRepository,
    private val billing: BillingManager,
    private val auth: AuthRepository,
) : ViewModel() {
    private val _state = MutableStateFlow(SubscriptionUiState())
    val state: StateFlow<SubscriptionUiState> = _state
    val purchase: StateFlow<PurchaseState> = billing.state
    private val _events = Channel<String>(Channel.BUFFERED)
    val events = _events.receiveAsFlow()

    init {
        load()
        viewModelScope.launch {
            billing.state.collect { p ->
                when (p) {
                    is PurchaseState.Completed -> { _state.update { it.copy(status = Loadable.Ready(p.status)) }; _events.send("Premium is active. Thank you!"); billing.reset(); loadHistory() }
                    is PurchaseState.Failed -> { _events.send(p.message); billing.reset() }
                    else -> Unit
                }
            }
        }
    }

    fun load() = viewModelScope.launch {
        _state.update { it.copy(status = Loadable.Loading) }
        val plans = subscriptions.plans().getOrNull().orEmpty()
        _state.update { it.copy(status = subscriptions.status().toLoadable(), plans = plans) }
        loadHistory()
        when (val o = billing.offers(plans.filter { it.tier == "Premium" })) {
            is AppResult.Success -> _state.update { it.copy(offers = o.data, billingError = if (o.data.isEmpty() && plans.any { p -> p.googlePlayProductId != null }) "Plans are not available from Google Play on this device." else null) }
            is AppResult.Failure -> _state.update { it.copy(billingError = o.error.message) }
        }
    }

    private suspend fun loadHistory() { subscriptions.history().getOrNull()?.let { h -> _state.update { it.copy(history = h) } } }

    fun buy(activity: Activity, offer: PlayOffer) = viewModelScope.launch {
        val userId = auth.currentUser.first()?.id ?: return@launch
        if (!billing.launch(activity, offer, userId)) _events.send("Could not open Google Play checkout.")
    }

    fun restore() {
        _state.update { it.copy(restoring = true) }
        viewModelScope.launch {
            val msg = when (val r = billing.restore()) {
                is AppResult.Success -> if (r.data == 0) "No active Google Play subscriptions found." else "Purchases restored."
                is AppResult.Failure -> r.error.message
            }
            _state.update { it.copy(restoring = false, status = subscriptions.status().toLoadable()) }
            _events.send(msg)
        }
    }
}
