package com.devinterview.feature.analytics

import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.grid.GridCells
import androidx.compose.foundation.lazy.grid.LazyVerticalGrid
import androidx.compose.foundation.lazy.grid.items
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.alpha
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.ViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewModelScope
import androidx.navigation.NavController
import androidx.navigation.NavGraphBuilder
import androidx.navigation.compose.composable
import com.devinterview.core.common.TimeFormat
import com.devinterview.core.designsystem.*
import com.devinterview.core.navigation.Routes
import com.devinterview.domain.model.*
import com.devinterview.domain.repository.AnalyticsRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.async
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.launch
import javax.inject.Inject
import kotlin.math.roundToInt

data class AnalyticsData(val overview: AnalyticsOverview, val technologies: List<TechnologyAnalytics>, val weekly: List<PeriodActivity>, val monthly: List<PeriodActivity>)

@HiltViewModel
class AnalyticsViewModel @Inject constructor(private val repo: AnalyticsRepository) : ViewModel() {
    private val _state = MutableStateFlow<Loadable<AnalyticsData>>(Loadable.Loading)
    val state: StateFlow<Loadable<AnalyticsData>> = _state
    val period = MutableStateFlow("weekly")

    init { load() }

    fun load() = viewModelScope.launch {
        if (_state.value !is Loadable.Ready) _state.value = Loadable.Loading
        val tech = async { repo.technologies() }
        val weekly = async { repo.weekly(8) }
        val monthly = async { repo.monthly(6) }
        _state.value = when (val o = repo.overview()) {
            is AppResult.Success -> Loadable.Ready(AnalyticsData(o.data, tech.await().getOrNull().orEmpty(), weekly.await().getOrNull().orEmpty(), monthly.await().getOrNull().orEmpty()))
            is AppResult.Failure -> Loadable.Failed(o.error)
        }
    }
}

@HiltViewModel
class AchievementsViewModel @Inject constructor(private val repo: AnalyticsRepository) : ViewModel() {
    private val _state = MutableStateFlow<Loadable<GamificationProfile>>(Loadable.Loading)
    val state: StateFlow<Loadable<GamificationProfile>> = _state
    init { load() }
    fun load() = viewModelScope.launch { _state.value = repo.gamification().toLoadable() }
}

@HiltViewModel
class LeaderboardViewModel @Inject constructor(private val repo: AnalyticsRepository) : ViewModel() {
    private val _state = MutableStateFlow<Loadable<Leaderboard>>(Loadable.Loading)
    val state: StateFlow<Loadable<Leaderboard>> = _state
    val period = MutableStateFlow("weekly")
    init { load() }
    fun select(p: String) { period.value = p; load() }
    fun load() = viewModelScope.launch { _state.value = Loadable.Loading; _state.value = repo.leaderboard(period.value).toLoadable() }
}

fun NavGraphBuilder.analyticsGraph(nav: NavController) {
    composable<Routes.Analytics> { AnalyticsScreen(nav) }
    composable<Routes.Achievements> { AchievementsScreen(nav) }
    composable<Routes.Leaderboard> { LeaderboardScreen(nav) }
}

private fun pct(v: Double) = "${v.roundToInt()}%"

@Composable
fun AnalyticsScreen(nav: NavController, viewModel: AnalyticsViewModel = hiltViewModel()) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    val period by viewModel.period.collectAsStateWithLifecycle()
    Scaffold(topBar = {
        DiTopBar("Analytics", onBack = { nav.popBackStack() }) {
            IconButton(onClick = { nav.navigate(Routes.Achievements) }) { Icon(Icons.Filled.EmojiEvents, "Achievements") }
            IconButton(onClick = { nav.navigate(Routes.Leaderboard) }) { Icon(Icons.Filled.Leaderboard, "Leaderboard") }
        }
    }) { padding ->
        LoadableContent(state, onRetry = { viewModel.load() }, modifier = Modifier.padding(padding)) { d ->
            val o = d.overview
            LazyColumn(Modifier.padding(padding).testTag("analytics"), contentPadding = PaddingValues(16.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
                item {
                    DiCard {
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            ScoreRing(o.preparationScore, label = "Prep score")
                            Spacer(Modifier.width(16.dp))
                            Column(Modifier.weight(1f)) {
                                Text("Interview readiness", style = MaterialTheme.typography.titleMedium)
                                KeyValueRow("Questions", "${o.components.questions}")
                                KeyValueRow("Coding", "${o.components.coding}")
                                KeyValueRow("Mock interviews", "${o.components.mockInterviews}")
                                KeyValueRow("Learning", "${o.components.learning}")
                            }
                        }
                        Text("Weighted by how much evidence you have in each area — practise more to raise confidence.", style = MaterialTheme.typography.labelSmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant)
                    }
                }
                item {
                    Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                        StatTile("Answered", "${o.totalAttempted}", Icons.Filled.QuestionAnswer, Modifier.weight(1f))
                        StatTile("Accuracy", pct(o.accuracy), Icons.Filled.TrackChanges, Modifier.weight(1f), tint = DiTheme.colors.success)
                        StatTile("Learning", TimeFormat.minutes(o.learningMinutes), Icons.Filled.Schedule, Modifier.weight(1f))
                    }
                }
                item {
                    DiCard {
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            Text("Activity", style = MaterialTheme.typography.titleMedium, modifier = Modifier.weight(1f))
                            FilterChip(period == "weekly", onClick = { viewModel.period.value = "weekly" }, label = { Text("Weekly") })
                            Spacer(Modifier.width(6.dp))
                            FilterChip(period == "monthly", onClick = { viewModel.period.value = "monthly" }, label = { Text("Monthly") })
                        }
                        val series = if (period == "weekly") d.weekly else d.monthly
                        if (series.isEmpty() || series.all { it.questions == 0 && it.minutes == 0 }) Text("No activity recorded yet.", style = MaterialTheme.typography.bodySmall)
                        else {
                            BarChart(series.map { ChartPoint(it.periodStart.drop(5).take(5), it.questions.toFloat()) })
                            Text("Questions answered", style = MaterialTheme.typography.labelSmall)
                            LineChart(series.map { ChartPoint(it.periodStart.drop(5).take(5), it.minutes.toFloat()) })
                            Text("Minutes studied", style = MaterialTheme.typography.labelSmall)
                        }
                    }
                }
                if (o.difficultyBreakdown.any { it.attempted > 0 }) item {
                    DiCard {
                        Text("By difficulty", style = MaterialTheme.typography.titleMedium)
                        DonutChart(o.difficultyBreakdown.filter { it.attempted > 0 }.map { ChartPoint(it.difficulty, it.attempted.toFloat()) to difficultyColor(it.difficulty) },
                            Modifier.align(Alignment.CenterHorizontally), centerText = "${o.totalAttempted}")
                        o.difficultyBreakdown.forEach { KeyValueRow(it.difficulty, "${it.correct}/${it.attempted} · ${pct(it.accuracy)}") }
                    }
                }
                item {
                    Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                        DiCard(Modifier.weight(1f)) {
                            Text("Quizzes", style = MaterialTheme.typography.titleSmall)
                            KeyValueRow("Taken", "${o.quizzes.quizzesTaken}"); KeyValueRow("Average", pct(o.quizzes.averagePercentage)); KeyValueRow("Pass rate", pct(o.quizzes.passRate))
                        }
                        DiCard(Modifier.weight(1f)) {
                            Text("Coding", style = MaterialTheme.typography.titleSmall)
                            KeyValueRow("Solved", "${o.coding.challengesSolved}/${o.coding.challengesAttempted}"); KeyValueRow("Success", pct(o.coding.successRate)); KeyValueRow("Submits", "${o.coding.submissions}")
                        }
                    }
                }
                if (o.mockInterviews.completed > 0) item {
                    DiCard {
                        Text("Mock interviews", style = MaterialTheme.typography.titleMedium)
                        KeyValueRow("Completed", "${o.mockInterviews.completed}")
                        KeyValueRow("Average overall", "${o.mockInterviews.averageOverall}")
                        KeyValueRow("Technical / communication", "${o.mockInterviews.averageTechnical} / ${o.mockInterviews.averageCommunication}")
                        if (o.mockInterviews.trend.size > 1) LineChart(o.mockInterviews.trend.map { ChartPoint(it.date.drop(5).take(5), it.score.toFloat()) }, maxValue = 100f)
                    }
                }
                if (d.technologies.isNotEmpty()) item {
                    DiCard {
                        Text("Technologies", style = MaterialTheme.typography.titleMedium)
                        val top = d.technologies.filter { it.attempted > 0 }.take(6)
                        if (top.size >= 3) RadarChart(top.map { ChartPoint(it.technology, it.accuracy.toFloat()) }, Modifier.align(Alignment.CenterHorizontally))
                        d.technologies.forEach { t ->
                            Row(Modifier.fillMaxWidth().padding(vertical = 3.dp), verticalAlignment = Alignment.CenterVertically) {
                                Text(t.technology, Modifier.width(110.dp), style = MaterialTheme.typography.bodySmall)
                                AnimatedProgressBar((t.accuracy / 100).toFloat(), Modifier.weight(1f))
                                Text(" ${t.correct}/${t.attempted}", style = MaterialTheme.typography.labelSmall)
                            }
                        }
                    }
                }
                if (o.weakTopics.isNotEmpty()) item { TopicCard("Focus areas", o.weakTopics, Icons.Filled.TrendingDown, DiTheme.colors.danger) }
                if (o.strongTopics.isNotEmpty()) item { TopicCard("Strengths", o.strongTopics, Icons.Filled.TrendingUp, DiTheme.colors.success) }
                if (o.recommendations.isNotEmpty()) {
                    item { SectionHeader("Recommended next steps") }
                    items(o.recommendations, key = { it.title }) { r ->
                        DiCard(onClick = recommendationRoute(r)?.let { route -> { nav.navigate(route) } }) {
                            Text(r.title, style = MaterialTheme.typography.titleSmall)
                            Text(r.reason, style = MaterialTheme.typography.bodySmall)
                        }
                    }
                }
            }
        }
    }
}

private fun recommendationRoute(r: Recommendation): Any? = when (r.actionType) {
    "category" -> r.targetId?.let { Routes.QuestionList(categoryId = it, title = r.title.removePrefix("Revise ")) } ?: Routes.TechnologyExplorer
    "practice" -> Routes.TechnologyExplorer
    "difficulty" -> Routes.QuizConfig()
    "challenge" -> r.targetId?.let { Routes.ChallengeDetail(it) } ?: Routes.ChallengeList()
    "mockInterview" -> Routes.InterviewSetup()
    "flashcards" -> Routes.FlashcardDecks
    "learningPath" -> r.targetId?.let { Routes.LearningPathDetail(it) } ?: Routes.Learn
    else -> null
}

@Composable
private fun TopicCard(title: String, topics: List<TopicPerformance>, icon: ImageVector, tint: androidx.compose.ui.graphics.Color) {
    DiCard {
        Row(verticalAlignment = Alignment.CenterVertically) { Icon(icon, null, tint = tint); Spacer(Modifier.width(8.dp)); Text(title, style = MaterialTheme.typography.titleMedium) }
        topics.forEach { KeyValueRow(it.name, "${it.correct}/${it.total} · ${pct(it.accuracy)}") }
    }
}

private fun achievementIcon(key: String): ImageVector = when (key) {
    "flame" -> Icons.Filled.LocalFireDepartment
    "code" -> Icons.Filled.Code
    "quiz" -> Icons.Filled.Quiz
    "database" -> Icons.Filled.Storage
    "puzzle" -> Icons.Filled.Extension
    "sword" -> Icons.Filled.Bolt
    "mic" -> Icons.Filled.RecordVoiceOver
    "trophy" -> Icons.Filled.EmojiEvents
    "book" -> Icons.Filled.MenuBook
    "target" -> Icons.Filled.TrackChanges
    "star" -> Icons.Filled.Star
    else -> Icons.Filled.MilitaryTech
}

@Composable
fun AchievementsScreen(nav: NavController, viewModel: AchievementsViewModel = hiltViewModel()) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    Scaffold(topBar = { DiTopBar("Achievements", onBack = { nav.popBackStack() }) }) { padding ->
        LoadableContent(state, onRetry = { viewModel.load() }, modifier = Modifier.padding(padding)) { g ->
            LazyVerticalGrid(GridCells.Adaptive(150.dp), Modifier.padding(padding), contentPadding = PaddingValues(16.dp),
                horizontalArrangement = Arrangement.spacedBy(12.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
                item(span = { androidx.compose.foundation.lazy.grid.GridItemSpan(maxLineSpan) }) {
                    GradientCard {
                        Text("Level ${g.level.level} · ${g.level.title}", style = MaterialTheme.typography.titleLarge, color = androidx.compose.ui.graphics.Color.White)
                        Text("${g.level.totalXp} XP total", color = androidx.compose.ui.graphics.Color.White)
                        AnimatedProgressBar(g.level.progress.toFloat(), color = androidx.compose.ui.graphics.Color.White)
                        Spacer(Modifier.height(8.dp))
                        Text("Streak ${g.currentStreak} days (best ${g.longestStreak}) · Weekly goal ${g.weeklyGoal.currentMinutes}/${g.weeklyGoal.targetMinutes} min",
                            color = androidx.compose.ui.graphics.Color.White, style = MaterialTheme.typography.labelMedium)
                    }
                }
                items(g.achievements, key = { it.code }) { a ->
                    DiCard(Modifier.alpha(if (a.earned) 1f else 0.5f)) {
                        Column(Modifier.fillMaxWidth(), horizontalAlignment = Alignment.CenterHorizontally) {
                            Icon(if (a.earned) achievementIcon(a.iconKey) else Icons.Filled.Lock, null, Modifier.size(40.dp),
                                tint = if (a.earned) DiTheme.colors.warning else MaterialTheme.colorScheme.outline)
                            Text(a.name, style = MaterialTheme.typography.titleSmall, textAlign = TextAlign.Center)
                            Text(a.description, style = MaterialTheme.typography.bodySmall, textAlign = TextAlign.Center)
                            Text("+${a.xpReward} XP", style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.primary)
                            a.awardedAt?.let { Text(TimeFormat.relative(it), style = MaterialTheme.typography.labelSmall) }
                        }
                    }
                }
                if (g.recentXp.isNotEmpty()) {
                    item(span = { androidx.compose.foundation.lazy.grid.GridItemSpan(maxLineSpan) }) { SectionHeader("Recent XP") }
                    items(g.recentXp.size, span = { androidx.compose.foundation.lazy.grid.GridItemSpan(maxLineSpan) }) { i ->
                        val x = g.recentXp[i]
                        KeyValueRow("${x.reason} · ${TimeFormat.relative(x.createdAt)}", "+${x.amount}")
                    }
                }
            }
        }
    }
}

@Composable
fun LeaderboardScreen(nav: NavController, viewModel: LeaderboardViewModel = hiltViewModel()) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    val period by viewModel.period.collectAsStateWithLifecycle()
    Scaffold(topBar = { DiTopBar("Leaderboard", onBack = { nav.popBackStack() }) }) { padding ->
        Column(Modifier.padding(padding)) {
            Row(Modifier.padding(16.dp, 8.dp), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                listOf("weekly" to "This week", "monthly" to "This month", "alltime" to "All time").forEach { (k, l) ->
                    FilterChip(period == k, onClick = { viewModel.select(k) }, label = { Text(l) })
                }
            }
            LoadableContent(state, onRetry = { viewModel.load() }) { lb ->
                if (lb.entries.isEmpty()) EmptyState("No rankings yet", "Earn XP by practising to appear on the leaderboard.", icon = Icons.Filled.Leaderboard)
                else LazyColumn(contentPadding = PaddingValues(16.dp), verticalArrangement = Arrangement.spacedBy(6.dp)) {
                    items(lb.entries, key = { it.userId }) { e -> LeaderRow(e) }
                    lb.currentUser?.takeIf { me -> lb.entries.none { it.userId == me.userId } }?.let { me ->
                        item { HorizontalDivider(Modifier.padding(vertical = 8.dp)) }
                        item { LeaderRow(me) }
                    }
                    item {
                        Text("Only members who opt in appear here. Change this in Settings.", style = MaterialTheme.typography.labelSmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant, modifier = Modifier.padding(top = 8.dp))
                    }
                }
            }
        }
    }
}

@Composable
private fun LeaderRow(e: LeaderboardEntry) {
    Surface(color = if (e.isCurrentUser) MaterialTheme.colorScheme.primaryContainer else MaterialTheme.colorScheme.surface, shape = MaterialTheme.shapes.medium) {
        Row(Modifier.fillMaxWidth().padding(12.dp), verticalAlignment = Alignment.CenterVertically) {
            Text("#${e.rank}", Modifier.width(44.dp), fontWeight = FontWeight.Bold, color = when (e.rank) { 1 -> DiTheme.colors.warning; else -> MaterialTheme.colorScheme.onSurface })
            Avatar(e.displayName, e.profilePhotoUrl, size = 36.dp)
            Spacer(Modifier.width(12.dp))
            Column(Modifier.weight(1f)) { Text(e.displayName + if (e.isCurrentUser) " (you)" else ""); Text("Level ${e.level}", style = MaterialTheme.typography.labelSmall) }
            Text("${e.xp} XP", fontWeight = FontWeight.SemiBold)
        }
    }
}
