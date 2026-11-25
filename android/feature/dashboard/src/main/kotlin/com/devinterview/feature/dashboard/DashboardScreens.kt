package com.devinterview.feature.dashboard

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.LazyRow
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.lazy.rememberLazyListState
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.MenuBook
import androidx.compose.material.icons.filled.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.navigation.NavController
import androidx.navigation.NavGraphBuilder
import androidx.navigation.compose.composable
import com.devinterview.core.common.TimeFormat
import com.devinterview.core.designsystem.*
import com.devinterview.core.navigation.DeepLinks
import com.devinterview.core.navigation.Routes
import com.devinterview.domain.model.*

fun NavGraphBuilder.dashboardGraph(nav: NavController, openDrawer: () -> Unit) {
    composable<Routes.Home> { HomeScreen(nav, openDrawer) }
    composable<Routes.Practice> { PracticeScreen(nav, openDrawer) }
    composable<Routes.Notifications> { NotificationsScreen(nav) }
    composable<Routes.Search> { SearchScreen(nav) }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun HomeScreen(nav: NavController, openDrawer: () -> Unit, viewModel: HomeViewModel = hiltViewModel()) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    val refreshing by viewModel.refreshing.collectAsStateWithLifecycle()
    val unread = state.dataOrNull?.summary?.unreadNotifications ?: 0
    Scaffold(topBar = {
        TopAppBar(
            title = { Text("DevInterview") },
            navigationIcon = { IconButton(onClick = openDrawer) { Icon(Icons.Filled.Menu, contentDescription = "Open menu") } },
            actions = {
                IconButton(onClick = { nav.navigate(Routes.Search()) }) { Icon(Icons.Filled.Search, contentDescription = "Search") }
                IconButton(onClick = { nav.navigate(Routes.Notifications) }) {
                    BadgedBox(badge = { if (unread > 0) Badge { Text(if (unread > 99) "99+" else unread.toString()) } }) {
                        Icon(Icons.Filled.Notifications, contentDescription = "Notifications")
                    }
                }
            },
        )
    }) { padding ->
        LoadableContent(state, onRetry = { viewModel.load() }, modifier = Modifier.padding(padding), loading = {
            Column(Modifier.padding(padding).padding(16.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) { repeat(5) { SkeletonCard() } }
        }) { data ->
            Refreshable(refreshing, onRefresh = { viewModel.load(pullToRefresh = true) }, modifier = Modifier.padding(padding)) {
                HomeContent(data, nav)
            }
        }
    }
}

@OptIn(ExperimentalLayoutApi::class)
@Composable
private fun HomeContent(data: HomeData, nav: NavController) {
    val s = data.summary
    LazyColumn(Modifier.fillMaxSize().testTag("home_list"), contentPadding = PaddingValues(16.dp), verticalArrangement = Arrangement.spacedBy(16.dp)) {
        item {
            GradientCard {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Column(Modifier.weight(1f)) {
                        Text("Hi, ${s.firstName}", style = MaterialTheme.typography.headlineSmall, color = Color.White)
                        Text("Level ${s.level.level} · ${s.level.title}", style = MaterialTheme.typography.bodyMedium, color = Color.White.copy(alpha = .9f))
                        Spacer(Modifier.height(10.dp))
                        AnimatedProgressBar(s.level.progress.toFloat(), color = Color.White)
                        Text("${s.level.currentLevelXp} / ${s.level.nextLevelXp} XP to next level", style = MaterialTheme.typography.labelSmall, color = Color.White.copy(alpha = .85f))
                    }
                    Spacer(Modifier.width(16.dp))
                    ScoreRing(s.preparationScore, size = 92.dp, stroke = 9.dp, label = "Ready", color = Color.White)
                }
                if (s.tier != "Premium") {
                    Spacer(Modifier.height(8.dp))
                    TextButton(onClick = { nav.navigate(Routes.Subscription) }, colors = ButtonDefaults.textButtonColors(contentColor = Color.White)) {
                        Icon(Icons.Filled.WorkspacePremium, null); Spacer(Modifier.width(6.dp)); Text("Unlock Premium")
                    }
                }
            }
        }
        item {
            Row(horizontalArrangement = Arrangement.spacedBy(12.dp)) {
                StatTile("Day streak", s.currentStreak.toString(), Icons.Filled.LocalFireDepartment, Modifier.weight(1f), tint = DiTheme.colors.warning)
                StatTile("Answered", s.questionsAttempted.toString(), Icons.Filled.QuestionAnswer, Modifier.weight(1f))
                StatTile("Accuracy", if (s.questionsAttempted == 0) "—" else "${s.correctAnswers * 100 / s.questionsAttempted}%", Icons.Filled.TrackChanges, Modifier.weight(1f), tint = DiTheme.colors.success)
            }
        }
        item {
            Row(horizontalArrangement = Arrangement.spacedBy(12.dp)) {
                StatTile("Challenges", s.challengesCompleted.toString(), Icons.Filled.Code, Modifier.weight(1f), tint = MaterialTheme.colorScheme.secondary)
                StatTile("Mock interviews", s.mockInterviewsCompleted.toString(), Icons.Filled.RecordVoiceOver, Modifier.weight(1f), tint = MaterialTheme.colorScheme.tertiary)
                StatTile("Learning", TimeFormat.minutes(s.learningMinutes), Icons.Filled.Schedule, Modifier.weight(1f))
            }
        }
        item {
            DiCard {
                Text("Today's goals", style = MaterialTheme.typography.titleMedium)
                Spacer(Modifier.height(8.dp))
                Text("Questions: ${s.todayQuestions} / ${s.dailyQuestionGoal}", style = MaterialTheme.typography.bodyMedium)
                AnimatedProgressBar(if (s.dailyQuestionGoal == 0) 0f else s.todayQuestions / s.dailyQuestionGoal.toFloat())
                Spacer(Modifier.height(8.dp))
                Text("This week: ${TimeFormat.minutes(s.weeklyMinutes)} of ${TimeFormat.minutes(s.weeklyGoalMinutes)}", style = MaterialTheme.typography.bodyMedium)
                AnimatedProgressBar(if (s.weeklyGoalMinutes == 0) 0f else s.weeklyMinutes / s.weeklyGoalMinutes.toFloat(), color = MaterialTheme.colorScheme.secondary)
            }
        }
        item {
            SectionHeader("Quick actions")
            FlowRow(horizontalArrangement = Arrangement.spacedBy(8.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                QuickAction("Daily quiz", Icons.Filled.Quiz) { nav.navigate(Routes.QuizHome) }
                QuickAction("Random question", Icons.Filled.Shuffle) { nav.navigate(Routes.RandomQuestion()) }
                QuickAction("Flashcards", Icons.Filled.Style) { nav.navigate(Routes.FlashcardDecks) }
                QuickAction("Mock interview", Icons.Filled.RecordVoiceOver) { nav.navigate(Routes.InterviewSetup()) }
                QuickAction("Evaluate answer", Icons.Filled.Grading) { nav.navigate(Routes.AnswerEvaluator()) }
                QuickAction("Review code", Icons.Filled.Reviews) { nav.navigate(Routes.CodeReviewer) }
            }
        }
        if (s.continueLearning.isNotEmpty()) {
            item { SectionHeader("Continue learning", action = "All paths", onAction = { nav.navigate(Routes.Learn) }) }
            items(s.continueLearning, key = { "c" + it.pathId }) { c ->
                DiCard(onClick = { c.lessonId?.let { nav.navigate(Routes.LessonDetail(it)) } ?: nav.navigate(Routes.LearningPathDetail(c.pathId)) }) {
                    Text(c.pathTitle, style = MaterialTheme.typography.titleMedium)
                    c.lessonTitle?.let { Text("Next: $it", style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurfaceVariant) }
                    Spacer(Modifier.height(8.dp))
                    AnimatedProgressBar(c.progressPercent / 100f)
                    Text("${c.progressPercent}% complete", style = MaterialTheme.typography.labelSmall)
                }
            }
        }
        s.dailyChallenge?.let { ch ->
            item {
                SectionHeader("Daily coding challenge")
                DiCard(onClick = { nav.navigate(Routes.ChallengeDetail(ch.id)) }) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Icon(Icons.Filled.Terminal, null, tint = MaterialTheme.colorScheme.primary)
                        Spacer(Modifier.width(12.dp))
                        Column(Modifier.weight(1f)) {
                            Text(ch.title, style = MaterialTheme.typography.titleMedium)
                            Text(ch.category, style = MaterialTheme.typography.bodySmall)
                        }
                        DifficultyChip(ch.difficulty)
                    }
                }
            }
        }
        if (data.activity.isNotEmpty()) {
            item {
                DiCard {
                    Text("Last 14 days", style = MaterialTheme.typography.titleMedium)
                    Spacer(Modifier.height(8.dp))
                    BarChart(data.activity.map { ChartPoint(it.date.takeLast(2), it.questions.toFloat()) })
                    Text("Questions answered per day", style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                }
            }
        }
        if (data.skills.isNotEmpty()) {
            item {
                SectionHeader("Skills", action = "Analytics", onAction = { nav.navigate(Routes.Analytics) })
                DiCard {
                    data.skills.take(6).forEach { sk ->
                        Row(Modifier.fillMaxWidth().padding(vertical = 4.dp), verticalAlignment = Alignment.CenterVertically) {
                            Text(sk.technology, Modifier.width(110.dp), maxLines = 1, overflow = TextOverflow.Ellipsis, style = MaterialTheme.typography.bodyMedium)
                            AnimatedProgressBar((sk.accuracy / 100).toFloat(), Modifier.weight(1f))
                            Text(" ${sk.accuracy.toInt()}%", style = MaterialTheme.typography.labelMedium)
                        }
                    }
                }
            }
        }
        if (s.recommendedPaths.isNotEmpty()) {
            item {
                SectionHeader("Recommended for you")
                LazyRow(horizontalArrangement = Arrangement.spacedBy(12.dp)) {
                    items(s.recommendedPaths, key = { it.id }) { p ->
                        DiCard(Modifier.width(240.dp), onClick = { nav.navigate(Routes.LearningPathDetail(p.id)) }) {
                            Row { TagChip(p.level); if (p.isPremium) { Spacer(Modifier.width(6.dp)); PremiumBadge() } }
                            Spacer(Modifier.height(6.dp))
                            Text(p.title, style = MaterialTheme.typography.titleMedium, maxLines = 2, overflow = TextOverflow.Ellipsis)
                            Text("${p.lessonCount} lessons · ${p.estimatedHours} h", style = MaterialTheme.typography.bodySmall)
                        }
                    }
                }
            }
        }
        if (s.recentQuestions.isNotEmpty()) {
            item { SectionHeader("Recently practised") }
            items(s.recentQuestions, key = { "r" + it.questionId + it.attemptedAt }) { q ->
                ListItem(
                    headlineContent = { Text(q.title, maxLines = 2, overflow = TextOverflow.Ellipsis) },
                    supportingContent = { Text("${q.technology} · ${TimeFormat.relative(q.attemptedAt)}") },
                    leadingContent = {
                        when (q.isCorrect) {
                            true -> Icon(Icons.Filled.CheckCircle, "Correct", tint = DiTheme.colors.success)
                            false -> Icon(Icons.Filled.Cancel, "Incorrect", tint = DiTheme.colors.danger)
                            null -> Icon(Icons.AutoMirrored.Filled.MenuBook, "Reviewed")
                        }
                    },
                    modifier = Modifier.clickable { nav.navigate(Routes.QuestionDetail(q.questionId)) },
                )
            }
        }
        if (s.upcomingReminders.isNotEmpty()) {
            item { SectionHeader("Upcoming interviews", action = "Manage", onAction = { nav.navigate(Routes.InterviewReminders) }) }
            items(s.upcomingReminders, key = { it.id }) { r ->
                DiCard {
                    Text(r.title, style = MaterialTheme.typography.titleMedium)
                    Text(listOfNotNull(r.companyName, TimeFormat.relative(r.scheduledAt)).joinToString(" · "), style = MaterialTheme.typography.bodySmall)
                }
            }
        }
    }
}

@Composable
private fun QuickAction(label: String, icon: ImageVector, onClick: () -> Unit) {
    AssistChip(onClick = onClick, label = { Text(label) }, leadingIcon = { Icon(icon, null, Modifier.size(18.dp)) })
}

private data class Hub(val title: String, val subtitle: String, val icon: ImageVector, val route: Any)

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun PracticeScreen(nav: NavController, openDrawer: () -> Unit) {
    val hubs = listOf(
        Hub("Question bank", "Browse questions by technology and category", Icons.AutoMirrored.Filled.MenuBook, Routes.TechnologyExplorer),
        Hub("Quizzes", "Daily quiz, timed tests and custom quizzes", Icons.Filled.Quiz, Routes.QuizHome),
        Hub("Coding challenges", "Solve problems and run them in a secure sandbox", Icons.Filled.Code, Routes.ChallengeList()),
        Hub("Flashcards", "Spaced repetition for quick revision", Icons.Filled.Style, Routes.FlashcardDecks),
        Hub("Random question", "Test yourself with a surprise question", Icons.Filled.Shuffle, Routes.RandomQuestion()),
        Hub("AI answer evaluator", "Get detailed feedback on your written answers", Icons.Filled.Grading, Routes.AnswerEvaluator()),
        Hub("AI code reviewer", "Quality, performance and security review", Icons.Filled.Reviews, Routes.CodeReviewer),
        Hub("Bookmarks", "Questions you saved for later", Icons.Filled.Bookmark, Routes.Bookmarks),
        Hub("My notes", "Personal notes on questions", Icons.Filled.EditNote, Routes.Notes),
        Hub("Collections", "Your custom study lists", Icons.Filled.CollectionsBookmark, Routes.Collections),
    )
    Scaffold(topBar = {
        TopAppBar(title = { Text("Practice") }, navigationIcon = { IconButton(onClick = openDrawer) { Icon(Icons.Filled.Menu, "Open menu") } })
    }) { padding ->
        LazyColumn(Modifier.padding(padding), contentPadding = PaddingValues(16.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
            items(hubs, key = { it.title }) { h ->
                DiCard(onClick = { nav.navigate(h.route) }) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Icon(h.icon, null, tint = MaterialTheme.colorScheme.primary, modifier = Modifier.size(32.dp))
                        Spacer(Modifier.width(16.dp))
                        Column(Modifier.weight(1f)) {
                            Text(h.title, style = MaterialTheme.typography.titleMedium)
                            Text(h.subtitle, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                        }
                        Icon(Icons.Filled.ChevronRight, null)
                    }
                }
            }
        }
    }
}

@Composable
fun NotificationsScreen(nav: NavController, viewModel: NotificationsViewModel = hiltViewModel()) {
    val s by viewModel.state.collectAsStateWithLifecycle()
    val listState = rememberLazyListState()
    val nearEnd by remember { derivedStateOf { listState.layoutInfo.visibleItemsInfo.lastOrNull()?.index?.let { it >= listState.layoutInfo.totalItemsCount - 3 } == true } }
    LaunchedEffect(nearEnd) { if (nearEnd) viewModel.loadMore() }
    Scaffold(topBar = {
        DiTopBar("Notifications", onBack = { nav.popBackStack() }) {
            if (s.items.any { it.readAt == null }) TextButton(onClick = { viewModel.markAllRead() }) { Text("Mark all read") }
        }
    }) { padding ->
        when {
            s.items.isEmpty() && s.loading -> LoadingState(Modifier.padding(padding))
            s.items.isEmpty() && s.error != null -> ErrorState(s.error!!, onRetry = viewModel::refresh, modifier = Modifier.padding(padding))
            s.items.isEmpty() -> EmptyState("You're all caught up", "Reminders, achievements and announcements will appear here.", Modifier.padding(padding), Icons.Filled.NotificationsNone)
            else -> LazyColumn(Modifier.padding(padding), state = listState) {
                items(s.items, key = { it.id }) { n ->
                    ListItem(
                        headlineContent = { Text(n.title, fontWeight = if (n.readAt == null) FontWeight.Bold else FontWeight.Normal) },
                        supportingContent = { Text(n.body) },
                        overlineContent = { Text("${n.category} · ${TimeFormat.relative(n.createdAt)}") },
                        leadingContent = { if (n.readAt == null) Badge() },
                        modifier = Modifier.clickable {
                            viewModel.open(n)
                            n.deepLink?.let(DeepLinks::toRoute)?.let { nav.navigate(it) }
                        },
                    )
                    HorizontalDivider()
                }
                if (s.loading) item { LinearProgressIndicator(Modifier.fillMaxWidth().padding(16.dp)) }
            }
        }
    }
}

private val searchTypes = listOf(null to "All", "question" to "Questions", "challenge" to "Challenges", "learningPath" to "Paths",
    "technology" to "Technologies", "category" to "Categories", "flashcard" to "Flashcards", "note" to "Notes")

@Composable
fun SearchScreen(nav: NavController, viewModel: SearchViewModel = hiltViewModel()) {
    val s by viewModel.state.collectAsStateWithLifecycle()
    Scaffold(topBar = { DiTopBar("Search", onBack = { nav.popBackStack() }) }) { padding ->
        Column(Modifier.padding(padding).fillMaxSize()) {
            OutlinedTextField(
                s.query, viewModel::onQuery, Modifier.fillMaxWidth().padding(horizontal = 16.dp).testTag("search_field"),
                placeholder = { Text("Questions, technologies, challenges, notes…") }, leadingIcon = { Icon(Icons.Filled.Search, null) },
                trailingIcon = { if (s.query.isNotEmpty()) IconButton(onClick = { viewModel.onQuery("") }) { Icon(Icons.Filled.Clear, "Clear") } },
                singleLine = true, keyboardOptions = KeyboardOptions(imeAction = ImeAction.Search), shape = MaterialTheme.shapes.large,
            )
            LazyRow(contentPadding = PaddingValues(16.dp, 8.dp), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                items(searchTypes, key = { it.second }) { (type, label) ->
                    val count = type?.let { s.results?.countsByType?.get(it) }
                    FilterChip(selected = s.type == type, onClick = { viewModel.onType(type) }, label = { Text(if (count != null) "$label ($count)" else label) })
                }
            }
            if (s.loading) LinearProgressIndicator(Modifier.fillMaxWidth())
            val hits = s.results?.hits?.items.orEmpty()
            when {
                s.error != null -> ErrorState(s.error!!, onRetry = { viewModel.onQuery(s.query) })
                s.query.trim().length < 2 -> EmptyState("Search everything", "Type at least two characters.", icon = Icons.Filled.ManageSearch)
                !s.loading && hits.isEmpty() && s.results != null -> EmptyState("No results", "Try a different keyword or filter.", icon = Icons.Filled.SearchOff)
                else -> LazyColumn {
                    items(hits, key = { it.type + it.id }) { h ->
                        ListItem(
                            headlineContent = { Text(h.title, maxLines = 2, overflow = TextOverflow.Ellipsis) },
                            supportingContent = { Text(listOfNotNull(h.technology, h.subtitle, h.difficulty).joinToString(" · "), maxLines = 1, overflow = TextOverflow.Ellipsis) },
                            overlineContent = { Text(searchTypes.firstOrNull { it.first == h.type }?.second ?: h.type) },
                            modifier = Modifier.clickable { searchRoute(h)?.let { nav.navigate(it) } },
                        )
                    }
                }
            }
        }
    }
}

private fun searchRoute(h: SearchHit): Any? = when (h.type) {
    "question", "note" -> Routes.QuestionDetail(h.id)
    "technology" -> Routes.Categories(h.id, h.title)
    "category" -> Routes.QuestionList(technologyId = h.parentId, categoryId = h.id, title = h.title)
    "challenge" -> Routes.ChallengeDetail(h.id)
    "learningPath" -> Routes.LearningPathDetail(h.id)
    "flashcard" -> Routes.FlashcardStudy(technologyId = h.parentId, deckName = h.technology ?: "Flashcards")
    else -> null
}
