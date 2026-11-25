package com.devinterview.feature.profile

import android.app.Activity
import android.content.Context
import android.content.ContextWrapper
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.PickVisualMediaRequest
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.HelpOutline
import androidx.compose.material.icons.filled.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.navigation.NavController
import androidx.navigation.NavGraphBuilder
import androidx.navigation.compose.composable
import com.devinterview.core.common.TimeFormat
import com.devinterview.core.designsystem.*
import com.devinterview.core.navigation.Routes
import com.devinterview.data.billing.PurchaseState
import com.devinterview.domain.usecase.FormValidator
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

fun NavGraphBuilder.profileGraph(nav: NavController, openDrawer: () -> Unit) {
    composable<Routes.Profile> { ProfileScreen(nav, openDrawer) }
    composable<Routes.EditProfile> { EditProfileScreen(nav) }
    composable<Routes.Subscription> { SubscriptionScreen(nav) }
    composable<Routes.Help> { HelpScreen(nav) }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun ProfileScreen(nav: NavController, openDrawer: () -> Unit, viewModel: ProfileViewModel = hiltViewModel()) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    LaunchedEffect(Unit) { viewModel.load() }
    Scaffold(topBar = {
        TopAppBar(title = { Text("Profile") }, navigationIcon = { IconButton(onClick = openDrawer) { Icon(Icons.Filled.Menu, "Open menu") } },
            actions = { IconButton(onClick = { nav.navigate(Routes.Settings) }) { Icon(Icons.Filled.Settings, "Settings") } })
    }) { padding ->
        LoadableContent(state, onRetry = { viewModel.load() }, modifier = Modifier.padding(padding)) { d ->
            val p = d.profile
            LazyColumn(Modifier.padding(padding), contentPadding = PaddingValues(16.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
                item {
                    DiCard {
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            Avatar("${p.firstName} ${p.lastName}", p.profilePhotoUrl, size = 72.dp)
                            Spacer(Modifier.width(16.dp))
                            Column(Modifier.weight(1f)) {
                                Text("${p.firstName} ${p.lastName}", style = MaterialTheme.typography.titleLarge)
                                Text(p.email, style = MaterialTheme.typography.bodySmall)
                                Text(listOfNotNull(p.experienceLevel, p.currentJobRole).joinToString(" · "), style = MaterialTheme.typography.bodySmall)
                                p.targetJobRole?.let { Text("Target: $it", style = MaterialTheme.typography.labelMedium, color = MaterialTheme.colorScheme.primary) }
                            }
                            IconButton(onClick = { nav.navigate(Routes.EditProfile) }, modifier = Modifier.testTag("edit_profile")) { Icon(Icons.Filled.Edit, "Edit profile") }
                        }
                        p.bio?.let { Spacer(Modifier.height(8.dp)); Text(it, style = MaterialTheme.typography.bodyMedium) }
                        if (p.preferredTechnologies.isNotEmpty()) {
                            Spacer(Modifier.height(8.dp))
                            Text(p.preferredTechnologies.joinToString(" · ") { it.name }, style = MaterialTheme.typography.labelMedium)
                        }
                    }
                }
                d.dashboard?.let { s ->
                    item {
                        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                            StatTile("Level", "${s.level.level}", Icons.Filled.MilitaryTech, Modifier.weight(1f))
                            StatTile("XP", "${s.level.totalXp}", Icons.Filled.Bolt, Modifier.weight(1f), tint = DiTheme.colors.warning)
                            StatTile("Best streak", "${s.longestStreak}", Icons.Filled.LocalFireDepartment, Modifier.weight(1f), tint = DiTheme.colors.danger)
                        }
                    }
                }
                item {
                    val premium = d.subscription?.tier == "Premium"
                    if (premium) DiCard(onClick = { nav.navigate(Routes.Subscription) }) {
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            PremiumBadge(); Spacer(Modifier.width(8.dp))
                            Text(d.subscription?.planName ?: "Premium", style = MaterialTheme.typography.titleMedium, modifier = Modifier.weight(1f))
                            d.subscription?.expiresAt?.let { Text(if (d.subscription.autoRenewing) "Renews ${TimeFormat.relative(it)}" else "Ends ${TimeFormat.relative(it)}", style = MaterialTheme.typography.labelSmall) }
                        }
                    } else GradientCard(brush = DiTheme.colors.premium, onClick = { nav.navigate(Routes.Subscription) }) {
                        Text("Go Premium", style = MaterialTheme.typography.titleLarge, color = Color.White)
                        Text("Unlimited AI interviews and evaluations, every coding challenge and company preparation.", color = Color.White.copy(alpha = .9f))
                    }
                }
                item { MenuRow(Icons.Filled.Insights, "Analytics") { nav.navigate(Routes.Analytics) } }
                item { MenuRow(Icons.Filled.EmojiEvents, "Achievements") { nav.navigate(Routes.Achievements) } }
                item { MenuRow(Icons.Filled.Leaderboard, "Leaderboard") { nav.navigate(Routes.Leaderboard) } }
                item { MenuRow(Icons.Filled.Bookmark, "Bookmarks") { nav.navigate(Routes.Bookmarks) } }
                item { MenuRow(Icons.Filled.EditNote, "My notes") { nav.navigate(Routes.Notes) } }
                item { MenuRow(Icons.Filled.Event, "Interview reminders") { nav.navigate(Routes.InterviewReminders) } }
                item { MenuRow(Icons.Filled.Settings, "Settings") { nav.navigate(Routes.Settings) } }
                item { MenuRow(Icons.AutoMirrored.Filled.HelpOutline, "Help & FAQ") { nav.navigate(Routes.Help) } }
                item { Text("Member since ${p.createdAt.take(10)}", style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant) }
            }
        }
    }
}

@Composable
private fun MenuRow(icon: ImageVector, title: String, onClick: () -> Unit) {
    ListItem(headlineContent = { Text(title) }, leadingContent = { Icon(icon, null) }, trailingContent = { Icon(Icons.Filled.ChevronRight, null) },
        modifier = Modifier.clickable(onClick = onClick))
}

@OptIn(ExperimentalLayoutApi::class)
@Composable
fun EditProfileScreen(nav: NavController, viewModel: EditProfileViewModel = hiltViewModel()) {
    val s by viewModel.state.collectAsStateWithLifecycle()
    val context = LocalContext.current
    val snackbar = remember { SnackbarHostState() }
    val scope = rememberCoroutineScope()
    LaunchedEffect(Unit) { viewModel.events.collect { snackbar.showSnackbar(it) } }
    LaunchedEffect(Unit) { viewModel.saved.collect { nav.popBackStack() } }
    val picker = rememberLauncherForActivityResult(ActivityResultContracts.PickVisualMedia()) { uri ->
        if (uri != null) scope.launch {
            val mime = context.contentResolver.getType(uri) ?: "image/jpeg"
            val bytes = withContext(Dispatchers.IO) { runCatching { context.contentResolver.openInputStream(uri)?.use { it.readBytes() } }.getOrNull() }
            if (bytes != null) viewModel.uploadPhoto(bytes, mime) else snackbar.showSnackbar("Couldn't read that image.")
        }
    }
    Scaffold(topBar = {
        DiTopBar("Edit profile", onBack = { nav.popBackStack() }) {
            TextButton(onClick = viewModel::save, enabled = s.loaded && !s.saving, modifier = Modifier.testTag("save_profile")) { Text("Save") }
        }
    }, snackbarHost = { SnackbarHost(snackbar) }) { padding ->
        when {
            s.loadError != null -> ErrorState(s.loadError!!, onRetry = { viewModel.load() }, modifier = Modifier.padding(padding))
            !s.loaded -> LoadingState(Modifier.padding(padding))
            else -> Column(Modifier.padding(padding).imePadding().verticalScroll(rememberScrollState()).padding(16.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
                if (s.saving) LinearProgressIndicator(Modifier.fillMaxWidth())
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Avatar("${s.firstName} ${s.lastName}", s.photoUrl, size = 72.dp)
                    Spacer(Modifier.width(16.dp))
                    SecondaryButton(if (s.uploading) "Uploading…" else "Change photo", enabled = !s.uploading, icon = Icons.Filled.PhotoCamera,
                        onClick = { picker.launch(PickVisualMediaRequest(ActivityResultContracts.PickVisualMedia.ImageOnly)) })
                }
                Row(horizontalArrangement = Arrangement.spacedBy(12.dp)) {
                    DiTextField(s.firstName, { v -> viewModel.update { it.copy(firstName = v.take(100)) } }, "First name", error = s.errors["firstName"], modifier = Modifier.weight(1f))
                    DiTextField(s.lastName, { v -> viewModel.update { it.copy(lastName = v.take(100)) } }, "Last name", error = s.errors["lastName"], modifier = Modifier.weight(1f))
                }
                DropdownField("Experience level", FormValidator.EXPERIENCE_LEVELS, s.experienceLevel, { v -> viewModel.update { it.copy(experienceLevel = v) } }, Modifier.fillMaxWidth())
                DiTextField(s.currentRole, { v -> viewModel.update { it.copy(currentRole = v.take(100)) } }, "Current job role")
                DiTextField(s.targetRole, { v -> viewModel.update { it.copy(targetRole = v.take(100)) } }, "Target job role")
                DiTextField(s.bio, { v -> viewModel.update { it.copy(bio = v.take(1000)) } }, "Bio", error = s.errors["bio"], singleLine = false, minLines = 3, supportingText = "${s.bio.length}/1000")
                if (s.technologies.isNotEmpty()) {
                    Text("Preferred technologies", style = MaterialTheme.typography.titleSmall)
                    FlowRow(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                        s.technologies.forEach { t -> FilterChip(t.id in s.selected, onClick = { viewModel.toggle(t.id) }, label = { Text(t.name) }) }
                    }
                }
                HorizontalDivider()
                Text("Goals", style = MaterialTheme.typography.titleSmall)
                Text("Daily questions: ${s.dailyGoal}")
                Slider(s.dailyGoal.toFloat(), { v -> viewModel.update { it.copy(dailyGoal = v.toInt()) } }, valueRange = 1f..100f)
                Text("Weekly study: ${TimeFormat.minutes(s.weeklyGoalMinutes)}")
                Slider(s.weeklyGoalMinutes.toFloat(), { v -> viewModel.update { it.copy(weeklyGoalMinutes = (v / 15).toInt() * 15) } }, valueRange = 30f..1200f)
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Column(Modifier.weight(1f)) {
                        Text("Show me on the leaderboard", style = MaterialTheme.typography.titleSmall)
                        Text("Only your display name, level and XP are shown.", style = MaterialTheme.typography.bodySmall)
                    }
                    Switch(s.showOnLeaderboard, { v -> viewModel.update { it.copy(showOnLeaderboard = v) } })
                }
                Text("Time zone: ${s.timeZoneId} (used for streaks and daily goals)", style = MaterialTheme.typography.labelSmall)
            }
        }
    }
}

private tailrec fun Context.findActivity(): Activity? = when (this) { is Activity -> this; is ContextWrapper -> baseContext.findActivity(); else -> null }

@Composable
fun SubscriptionScreen(nav: NavController, viewModel: SubscriptionViewModel = hiltViewModel()) {
    val s by viewModel.state.collectAsStateWithLifecycle()
    val purchase by viewModel.purchase.collectAsStateWithLifecycle()
    val context = LocalContext.current
    val snackbar = remember { SnackbarHostState() }
    LaunchedEffect(Unit) { viewModel.events.collect { snackbar.showSnackbar(it) } }
    Scaffold(topBar = { DiTopBar("Subscription", onBack = { nav.popBackStack() }) }, snackbarHost = { SnackbarHost(snackbar) }) { padding ->
        LoadableContent(s.status, onRetry = { viewModel.load() }, modifier = Modifier.padding(padding)) { status ->
            LazyColumn(Modifier.padding(padding), contentPadding = PaddingValues(16.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
                item {
                    DiCard {
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            Text(if (status.tier == "Premium") "Premium" else "Free plan", style = MaterialTheme.typography.titleLarge, modifier = Modifier.weight(1f).testTag("current_tier"))
                            if (status.tier == "Premium") PremiumBadge()
                        }
                        status.planName?.let { KeyValueRow("Plan", it) }
                        status.status?.let { KeyValueRow("Status", it) }
                        status.expiresAt?.let { KeyValueRow(if (status.autoRenewing) "Renews" else "Access until", it.take(10)) }
                        status.provider?.let { KeyValueRow("Billed through", it) }
                    }
                }
                if (status.usageToday.isNotEmpty()) item {
                    DiCard {
                        Text("Today's usage", style = MaterialTheme.typography.titleMedium)
                        status.usageToday.forEach { u ->
                            KeyValueRow(u.feature.replace(Regex("([a-z])([A-Z])"), "$1 $2"), if (u.limit == null) "${u.used} · unlimited" else "${u.used} / ${u.limit}")
                            u.limit?.let { AnimatedProgressBar(u.used / it.toFloat().coerceAtLeast(1f)) }
                        }
                    }
                }
                if (purchase is PurchaseState.Verifying) item { DiCard { Row(verticalAlignment = Alignment.CenterVertically) { CircularProgressIndicator(Modifier.size(20.dp), strokeWidth = 2.dp); Spacer(Modifier.width(12.dp)); Text("Confirming your purchase with Google Play…") } } }
                val premiumPlans = s.plans.filter { it.tier == "Premium" }
                if (status.tier != "Premium" && premiumPlans.isNotEmpty()) {
                    item { SectionHeader("Upgrade") }
                    items(premiumPlans, key = { it.id }) { plan ->
                        val offer = s.offers.firstOrNull { it.plan.id == plan.id }
                        DiCard {
                            Text(plan.name, style = MaterialTheme.typography.titleMedium)
                            Text(plan.description, style = MaterialTheme.typography.bodySmall)
                            BulletList(plan.features)
                            Spacer(Modifier.height(8.dp))
                            if (offer != null) PrimaryButton("Subscribe · ${offer.formattedPrice}", enabled = purchase !is PurchaseState.Verifying, modifier = Modifier.fillMaxWidth().testTag("buy_${plan.code}"),
                                onClick = { context.findActivity()?.let { viewModel.buy(it, offer) } })
                            else Text("Available through Google Play on supported devices.", style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                        }
                    }
                    s.billingError?.let { item { Text(it, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.error) } }
                }
                s.plans.firstOrNull { it.tier == "Free" }?.let { free ->
                    item { DiCard { Text("Free plan includes", style = MaterialTheme.typography.titleSmall); BulletList(free.features, icon = Icons.Filled.Check) } }
                }
                item {
                    SecondaryButton(if (s.restoring) "Restoring…" else "Restore purchases", onClick = viewModel::restore, enabled = !s.restoring, modifier = Modifier.fillMaxWidth())
                    Text("Subscriptions renew automatically until cancelled. Manage or cancel any time in Google Play › Payments & subscriptions.",
                        style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant, modifier = Modifier.padding(top = 8.dp))
                }
                if (s.history.isNotEmpty()) {
                    item { SectionHeader("Purchase history") }
                    items(s.history, key = { it.id }) { h ->
                        ListItem(headlineContent = { Text(h.planName) }, supportingContent = { Text("${h.provider} · ${h.startsAt.take(10)} → ${h.expiresAt.take(10)}${h.orderId?.let { " · $it" } ?: ""}") },
                            trailingContent = { Text(h.status) })
                    }
                }
            }
        }
    }
}

private val faq = listOf(
    "How is my answer graded?" to "Multiple-choice answers are graded on our servers against the published answer key. For open questions you compare your answer with the model answer and rate yourself, or ask the AI evaluator for detailed feedback.",
    "Does the app work offline?" to "Yes. Bookmarks, notes and flashcard reviews are saved on the device and synchronised when you're back online. Download a learning path to read its lessons offline. Quizzes, code execution and AI features need a connection.",
    "Where does my code run?" to "Code you run or submit is executed on DevInterview servers in an isolated sandbox with no network access and strict time and memory limits. It never runs on your phone.",
    "How accurate is the AI feedback?" to "AI evaluations and reviews are practice aids. They can be wrong, and an AI code review is not a substitute for a professional security audit. Always verify important details.",
    "Are company questions real interview questions?" to "Company practice sets are labelled by source. 'Illustrative practice' content is original material written to match the skills a role typically needs; it is not a claim that a company asked these questions.",
    "How do streaks work?" to "Your streak grows each day you practise in your own time zone (set from your device). Missing a day resets it.",
    "How do I cancel Premium?" to "Open Google Play › Payments & subscriptions › Subscriptions, choose DevInterview and cancel. You keep Premium until the end of the paid period.",
    "How do I delete my account or export my data?" to "Go to Settings. 'Export my data' creates a JSON copy of your data; 'Delete account' permanently removes your personal data.",
    "I found a mistake in a question" to "Open the question, tap the menu and choose 'Report a problem'. Our content team reviews every report.",
)

@Composable
fun HelpScreen(nav: NavController) {
    Scaffold(topBar = { DiTopBar("Help & FAQ", onBack = { nav.popBackStack() }) }) { padding ->
        LazyColumn(Modifier.padding(padding), contentPadding = PaddingValues(16.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
            items(faq, key = { it.first }) { (q, a) ->
                var open by remember { mutableStateOf(false) }
                DiCard(onClick = { open = !open }) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Text(q, style = MaterialTheme.typography.titleSmall, modifier = Modifier.weight(1f))
                        Icon(if (open) Icons.Filled.ExpandLess else Icons.Filled.ExpandMore, null)
                    }
                    if (open) Text(a, style = MaterialTheme.typography.bodyMedium, modifier = Modifier.padding(top = 6.dp))
                }
            }
        }
    }
}
