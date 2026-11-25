package com.devinterview.app

import android.content.Context
import android.content.Intent
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.viewModels
import androidx.activity.enableEdgeToEdge
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.runtime.getValue
import androidx.core.splashscreen.SplashScreen.Companion.installSplashScreen
import androidx.lifecycle.ViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewModelScope
import com.devinterview.app.notifications.Notifications
import com.devinterview.app.ui.DevInterviewAppShell
import com.devinterview.core.designsystem.DevInterviewTheme
import com.devinterview.core.designsystem.LocalApiBaseUrl
import com.devinterview.core.navigation.DeepLinks
import com.devinterview.core.network.SessionEvents
import com.devinterview.domain.model.SyncState
import com.devinterview.domain.repository.AuthRepository
import com.devinterview.domain.repository.ProfileRepository
import com.devinterview.domain.repository.SettingsRepository
import com.devinterview.domain.repository.SyncRepository
import dagger.hilt.android.AndroidEntryPoint
import dagger.hilt.android.lifecycle.HiltViewModel
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.flow.*
import kotlinx.coroutines.launch
import javax.inject.Inject

@HiltViewModel
class MainViewModel @Inject constructor(
    settings: SettingsRepository,
    sync: SyncRepository,
    auth: AuthRepository,
    sessionEvents: SessionEvents,
    private val profiles: ProfileRepository,
    @ApplicationContext private val context: Context,
) : ViewModel() {
    val themeMode: StateFlow<String> = settings.themeMode.stateIn(viewModelScope, SharingStarted.Eagerly, "system")
    val syncState: StateFlow<SyncState?> = sync.state.stateIn(viewModelScope, SharingStarted.WhileSubscribed(5000), null)
    val sessionExpired: Flow<Unit> = sessionEvents.expired
    private val _deepLinks = Channel<Any>(Channel.BUFFERED)
    val deepLinks = _deepLinks.receiveAsFlow()

    init {
        // When someone signs in: register this device for push and push any offline changes.
        viewModelScope.launch {
            auth.currentUser.map { it?.id }.distinctUntilChanged().filterNotNull().collect {
                sync.scheduleSync()
                Notifications.fcmToken(context)?.let { token -> profiles.registerDevice(token) }
            }
        }
        // Coming back online flushes the offline queue.
        viewModelScope.launch {
            sync.state.map { it.isOnline to (it.pendingChanges > 0) }.distinctUntilChanged().collect { (online, pending) -> if (online && pending) sync.scheduleSync() }
        }
    }

    fun handleIntent(intent: Intent?) {
        val link = intent?.dataString ?: intent?.getStringExtra("deepLink") ?: return
        DeepLinks.toRoute(link)?.let { viewModelScope.launch { _deepLinks.send(it) } }
    }
}

@AndroidEntryPoint
class MainActivity : ComponentActivity() {
    private val viewModel: MainViewModel by viewModels()

    override fun onCreate(savedInstanceState: Bundle?) {
        installSplashScreen()
        enableEdgeToEdge()
        super.onCreate(savedInstanceState)
        if (savedInstanceState == null) viewModel.handleIntent(intent)
        setContent {
            val theme by viewModel.themeMode.collectAsStateWithLifecycle()
            CompositionLocalProvider(LocalApiBaseUrl provides BuildConfig.API_BASE_URL) {
                DevInterviewTheme(themeMode = theme) { DevInterviewAppShell(viewModel) }
            }
        }
    }

    override fun onNewIntent(intent: Intent) {
        super.onNewIntent(intent)
        viewModel.handleIntent(intent)
    }
}
