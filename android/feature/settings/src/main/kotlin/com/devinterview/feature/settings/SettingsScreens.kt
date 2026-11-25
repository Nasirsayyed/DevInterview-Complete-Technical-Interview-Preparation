package com.devinterview.feature.settings

import android.Manifest
import android.content.pm.PackageManager
import android.os.Build
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.selection.selectable
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.Logout
import androidx.compose.material.icons.filled.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.unit.dp
import androidx.core.content.ContextCompat
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.navigation.NavController
import androidx.navigation.NavGraphBuilder
import androidx.navigation.compose.composable
import com.devinterview.core.common.TimeFormat
import com.devinterview.core.designsystem.*
import com.devinterview.core.navigation.Routes
import com.devinterview.core.navigation.clearAll
import com.devinterview.domain.model.NotificationPreference
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import java.time.LocalDate
import java.time.LocalDateTime
import java.time.LocalTime
import java.time.ZoneId
import java.time.format.DateTimeFormatter

fun NavGraphBuilder.settingsGraph(nav: NavController) {
    composable<Routes.Settings> { SettingsScreen(nav) }
    composable<Routes.ChangePassword> { ChangePasswordScreen(nav) }
    composable<Routes.NotificationSettings> { NotificationSettingsScreen(nav) }
    composable<Routes.InterviewReminders> { RemindersScreen(nav) }
}

@Composable
fun SettingsScreen(nav: NavController, viewModel: SettingsViewModel = hiltViewModel()) {
    val theme by viewModel.themeMode.collectAsStateWithLifecycle()
    val notifications by viewModel.notificationsEnabled.collectAsStateWithLifecycle()
    val sync by viewModel.syncState.collectAsStateWithLifecycle()
    val s by viewModel.state.collectAsStateWithLifecycle()
    val context = LocalContext.current
    val snackbar = remember { SnackbarHostState() }
    val scope = rememberCoroutineScope()
    var pendingExport by remember { mutableStateOf<String?>(null) }
    var confirmLogout by remember { mutableStateOf<Boolean?>(null) }
    var deleteDialog by remember { mutableStateOf(false) }
    val saveExport = rememberLauncherForActivityResult(ActivityResultContracts.CreateDocument("application/json")) { uri ->
        val json = pendingExport
        pendingExport = null
        if (uri != null && json != null) scope.launch {
            val ok = withContext(Dispatchers.IO) { runCatching { context.contentResolver.openOutputStream(uri)?.use { it.write(json.toByteArray()) } }.isSuccess }
            snackbar.showSnackbar(if (ok) "Your data was exported" else "Couldn't save the file")
        }
    }
    val notificationPermission = rememberLauncherForActivityResult(ActivityResultContracts.RequestPermission()) { granted ->
        viewModel.setNotifications(granted)
        if (!granted) scope.launch { snackbar.showSnackbar("Notifications are blocked. You can allow them in system settings.") }
    }
    LaunchedEffect(Unit) {
        viewModel.events.collect { e ->
            when (e) {
                is SettingsEvent.Message -> snackbar.showSnackbar(e.text)
                is SettingsEvent.Export -> { pendingExport = e.json; saveExport.launch("devinterview-export-${LocalDate.now()}.json") }
                SettingsEvent.SignedOut -> nav.navigate(Routes.Login) { clearAll(nav) }
            }
        }
    }
    Scaffold(topBar = { DiTopBar("Settings", onBack = { nav.popBackStack() }) }, snackbarHost = { SnackbarHost(snackbar) }) { padding ->
        Column(Modifier.padding(padding).verticalScroll(rememberScrollState())) {
            Group("Appearance")
            Row(Modifier.padding(horizontal = 16.dp), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                listOf("system" to "System", "light" to "Light", "dark" to "Dark").forEach { (k, l) ->
                    FilterChip(theme == k, onClick = { viewModel.setTheme(k) }, label = { Text(l) }, modifier = Modifier.testTag("theme_$k"))
                }
            }
            Group("Notifications")
            ListItem(headlineContent = { Text("Push notifications") }, supportingContent = { Text("Practice reminders, streaks and interview reminders") },
                leadingContent = { Icon(Icons.Filled.Notifications, null) },
                trailingContent = {
                    Switch(notifications, { enable ->
                        val needsPermission = Build.VERSION.SDK_INT >= 33 &&
                            ContextCompat.checkSelfPermission(context, Manifest.permission.POST_NOTIFICATIONS) != PackageManager.PERMISSION_GRANTED
                        if (enable && needsPermission) notificationPermission.launch(Manifest.permission.POST_NOTIFICATIONS) else viewModel.setNotifications(enable)
                    })
                })
            Row(Icons.Filled.Tune, "Notification preferences") { nav.navigate(Routes.NotificationSettings) }
            Row(Icons.Filled.Event, "Interview reminders") { nav.navigate(Routes.InterviewReminders) }
            Group("Data & sync")
            ListItem(headlineContent = { Text("Sync now") },
                supportingContent = {
                    Text(sync?.let { st -> buildString {
                        append(if (st.isOnline) "Online" else "Offline")
                        if (st.pendingChanges > 0) append(" · ${st.pendingChanges} changes waiting")
                        st.lastSyncedAt?.let { append(" · last synced ${TimeFormat.relative(java.time.Instant.ofEpochMilli(it).toString())}") }
                    } } ?: "")
                },
                leadingContent = { Icon(Icons.Filled.Sync, null) }, modifier = Modifier.clickable { viewModel.syncNow() })
            Row(Icons.Filled.Download, "Export my data", enabled = !s.busy) { viewModel.export() }
            Group("Account")
            if (s.hasPassword) Row(Icons.Filled.Password, "Change password") { nav.navigate(Routes.ChangePassword) }
            Row(Icons.AutoMirrored.Filled.Logout, "Sign out") { confirmLogout = false }
            Row(Icons.Filled.Devices, "Sign out of all devices") { confirmLogout = true }
            ListItem(headlineContent = { Text("Delete account", color = MaterialTheme.colorScheme.error) },
                leadingContent = { Icon(Icons.Filled.DeleteForever, null, tint = MaterialTheme.colorScheme.error) },
                modifier = Modifier.clickable { deleteDialog = true }.testTag("delete_account"))
            Spacer(Modifier.height(24.dp))
        }
    }
    confirmLogout?.let { all ->
        ConfirmDialog(if (all) "Sign out everywhere?" else "Sign out?",
            if (all) "You'll be signed out on every device, including this one." else "Pending offline changes are synced before signing out.",
            "Sign out", { confirmLogout = null; viewModel.logout(all) }, { confirmLogout = null })
    }
    if (deleteDialog) {
        var password by remember { mutableStateOf("") }
        var acknowledged by remember { mutableStateOf(false) }
        AlertDialog(onDismissRequest = { deleteDialog = false; viewModel.clearDeleteError() }, title = { Text("Delete account permanently?") }, text = {
            Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                Text("Your profile, progress, notes, interviews and submissions will be deleted. Active Google Play subscriptions must be cancelled in Google Play separately. This cannot be undone.")
                if (s.hasPassword) DiTextField(password, { password = it }, "Password", isPassword = true, imeAction = androidx.compose.ui.text.input.ImeAction.Done)
                androidx.compose.foundation.layout.Row(verticalAlignment = Alignment.CenterVertically, modifier = Modifier.selectable(acknowledged) { acknowledged = !acknowledged }) {
                    Checkbox(acknowledged, null); Spacer(Modifier.width(8.dp)); Text("I understand this is permanent")
                }
                s.deleteError?.let { Text(it, color = MaterialTheme.colorScheme.error) }
            }
        }, confirmButton = {
            TextButton(onClick = { viewModel.deleteAccount(password) }, enabled = acknowledged && (!s.hasPassword || password.isNotBlank()) && !s.busy,
                colors = ButtonDefaults.textButtonColors(contentColor = MaterialTheme.colorScheme.error)) { Text("Delete") }
        }, dismissButton = { TextButton(onClick = { deleteDialog = false; viewModel.clearDeleteError() }) { Text("Cancel") } })
    }
}

@Composable
private fun Group(title: String) {
    Text(title, style = MaterialTheme.typography.labelLarge, color = MaterialTheme.colorScheme.primary, modifier = Modifier.padding(start = 16.dp, top = 20.dp, bottom = 6.dp))
}

@Composable
private fun Row(icon: ImageVector, title: String, enabled: Boolean = true, onClick: () -> Unit) {
    ListItem(headlineContent = { Text(title) }, leadingContent = { Icon(icon, null) }, trailingContent = { Icon(Icons.Filled.ChevronRight, null) },
        modifier = Modifier.clickable(enabled = enabled, onClick = onClick))
}

@Composable
fun ChangePasswordScreen(nav: NavController, viewModel: ChangePasswordViewModel = hiltViewModel()) {
    val s by viewModel.state.collectAsStateWithLifecycle()
    LaunchedEffect(Unit) { viewModel.done.collect { nav.popBackStack() } }
    Scaffold(topBar = { DiTopBar("Change password", onBack = { nav.popBackStack() }) }) { padding ->
        Column(Modifier.padding(padding).imePadding().padding(16.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
            DiTextField(s.current, { v -> viewModel.update { it.copy(current = v) } }, "Current password", error = s.errors["current"], isPassword = true)
            DiTextField(s.new, { v -> viewModel.update { it.copy(new = v) } }, "New password", error = s.errors["new"], isPassword = true,
                supportingText = "8+ characters with upper and lower case letters and a digit.")
            DiTextField(s.confirm, { v -> viewModel.update { it.copy(confirm = v) } }, "Confirm new password", error = s.errors["confirm"], isPassword = true,
                imeAction = androidx.compose.ui.text.input.ImeAction.Done, onImeAction = viewModel::save)
            Text("Other devices will be signed out.", style = MaterialTheme.typography.bodySmall)
            s.error?.let { Text(it, color = MaterialTheme.colorScheme.error) }
            PrimaryButton("Update password", onClick = viewModel::save, loading = s.saving, modifier = Modifier.fillMaxWidth())
        }
    }
}

private fun categoryLabel(c: String) = when (c) {
    "DailyPractice" -> "Daily practice reminder"; "DailyChallenge" -> "Daily coding challenge"; "StreakReminder" -> "Streak at risk"
    "MockInterviewReminder" -> "Weekly mock interview nudge"; "WeeklySummary" -> "Weekly progress summary"; "LearningGoal" -> "Learning goal progress"
    "System" -> "Announcements"; else -> c
}

@Composable
fun NotificationSettingsScreen(nav: NavController, viewModel: NotificationSettingsViewModel = hiltViewModel()) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    val snackbar = remember { SnackbarHostState() }
    LaunchedEffect(Unit) { viewModel.events.collect { snackbar.showSnackbar(it) } }
    Scaffold(topBar = { DiTopBar("Notification preferences", onBack = { nav.popBackStack() }) }, snackbarHost = { SnackbarHost(snackbar) }) { padding ->
        LoadableContent(state, onRetry = { viewModel.load() }, modifier = Modifier.padding(padding)) { prefs ->
            LazyColumn(Modifier.padding(padding), contentPadding = PaddingValues(16.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                items(prefs, key = { it.category }) { p -> PreferenceCard(p, viewModel::update) }
                item { Text("Times are in your local time zone.", style = MaterialTheme.typography.labelSmall) }
            }
        }
    }
}

@Composable
private fun PreferenceCard(p: NotificationPreference, onChange: (NotificationPreference) -> Unit) {
    DiCard {
        androidx.compose.foundation.layout.Row(verticalAlignment = Alignment.CenterVertically) {
            Text(categoryLabel(p.category), Modifier.weight(1f), style = MaterialTheme.typography.titleSmall)
            Switch(p.enabled, { onChange(p.copy(enabled = it)) })
        }
        if (p.enabled && p.category in setOf("DailyPractice", "DailyChallenge", "StreakReminder", "WeeklySummary", "LearningGoal", "MockInterviewReminder")) {
            var hour by remember(p.preferredHourLocal) { mutableFloatStateOf(p.preferredHourLocal.toFloat()) }
            Text("Around %02d:00".format(hour.toInt()), style = MaterialTheme.typography.bodySmall)
            Slider(hour, { hour = it }, valueRange = 0f..23f, steps = 22, onValueChangeFinished = { onChange(p.copy(preferredHourLocal = hour.toInt())) })
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun RemindersScreen(nav: NavController, viewModel: RemindersViewModel = hiltViewModel()) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    val snackbar = remember { SnackbarHostState() }
    var form by remember { mutableStateOf<ReminderForm?>(null) }
    LaunchedEffect(Unit) { viewModel.events.collect { snackbar.showSnackbar(it) } }
    val fmt = remember { DateTimeFormatter.ofPattern("EEE d MMM yyyy, HH:mm") }
    Scaffold(topBar = { DiTopBar("Interview reminders", onBack = { nav.popBackStack() }) }, snackbarHost = { SnackbarHost(snackbar) },
        floatingActionButton = { ExtendedFloatingActionButton(onClick = { form = ReminderForm() }, icon = { Icon(Icons.Filled.Add, null) }, text = { Text("Add interview") }) }) { padding ->
        LoadableContent(state, onRetry = { viewModel.load() }, modifier = Modifier.padding(padding)) { reminders ->
            if (reminders.isEmpty()) EmptyState("No upcoming interviews", "Add your real interview dates and we'll send you a reminder during the 24 hours before.",
                Modifier.padding(padding), Icons.Filled.Event)
            else LazyColumn(Modifier.padding(padding), contentPadding = PaddingValues(16.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                items(reminders, key = { it.id }) { r ->
                    DiCard {
                        androidx.compose.foundation.layout.Row(verticalAlignment = Alignment.CenterVertically) {
                            Column(Modifier.weight(1f)) {
                                Text(r.title, style = MaterialTheme.typography.titleMedium)
                                Text(listOfNotNull(r.companyName, TimeFormat.parse(r.scheduledAt)?.atZone(ZoneId.systemDefault())?.format(fmt)).joinToString(" · "), style = MaterialTheme.typography.bodySmall)
                                r.notes?.let { Text(it, style = MaterialTheme.typography.bodySmall) }
                            }
                            IconButton(onClick = { viewModel.delete(r.id) }) { Icon(Icons.Filled.Delete, "Delete reminder") }
                        }
                    }
                }
            }
        }
    }
    form?.let { f ->
        var showDate by remember { mutableStateOf(false) }
        var showTime by remember { mutableStateOf(false) }
        AlertDialog(onDismissRequest = { form = null }, title = { Text("Add interview") }, text = {
            Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                OutlinedTextField(f.title, { form = f.copy(title = it.take(150)) }, label = { Text("Title (e.g. Backend developer – round 2)") }, singleLine = true)
                OutlinedTextField(f.company, { form = f.copy(company = it.take(120)) }, label = { Text("Company (optional)") }, singleLine = true)
                OutlinedTextField(f.notes, { form = f.copy(notes = it.take(1000)) }, label = { Text("Notes (optional)") })
                TextButton(onClick = { showDate = true }) { Icon(Icons.Filled.CalendarMonth, null); Spacer(Modifier.width(6.dp)); Text(f.dateTime.toLocalDate().toString()) }
                TextButton(onClick = { showTime = true }) { Icon(Icons.Filled.Schedule, null); Spacer(Modifier.width(6.dp)); Text("%02d:%02d".format(f.dateTime.hour, f.dateTime.minute)) }
            }
        }, confirmButton = { TextButton(onClick = { viewModel.create(f); form = null }, enabled = f.title.isNotBlank()) { Text("Save") } },
            dismissButton = { TextButton(onClick = { form = null }) { Text("Cancel") } })
        if (showDate) {
            val picker = rememberDatePickerState(initialSelectedDateMillis = f.dateTime.toLocalDate().atStartOfDay(ZoneId.of("UTC")).toInstant().toEpochMilli())
            DatePickerDialog(onDismissRequest = { showDate = false }, confirmButton = {
                TextButton(onClick = {
                    picker.selectedDateMillis?.let { ms ->
                        val date = java.time.Instant.ofEpochMilli(ms).atZone(ZoneId.of("UTC")).toLocalDate()
                        form = f.copy(dateTime = LocalDateTime.of(date, f.dateTime.toLocalTime()))
                    }
                    showDate = false
                }) { Text("OK") }
            }) { DatePicker(picker) }
        }
        if (showTime) {
            val picker = rememberTimePickerState(f.dateTime.hour, f.dateTime.minute)
            AlertDialog(onDismissRequest = { showTime = false }, text = { TimePicker(picker) }, confirmButton = {
                TextButton(onClick = { form = f.copy(dateTime = LocalDateTime.of(f.dateTime.toLocalDate(), LocalTime.of(picker.hour, picker.minute))); showTime = false }) { Text("OK") }
            })
        }
    }
}
