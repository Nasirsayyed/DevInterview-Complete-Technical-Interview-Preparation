package com.devinterview.feature.quiz

import androidx.activity.compose.BackHandler
import androidx.compose.animation.AnimatedContent
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.LazyRow
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.lazy.itemsIndexed
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.selection.selectable
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.automirrored.filled.ArrowForward
import androidx.compose.material.icons.filled.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.navigation.NavController
import androidx.navigation.NavGraphBuilder
import androidx.navigation.compose.composable
import com.devinterview.core.common.TimeFormat
import com.devinterview.core.designsystem.*
import com.devinterview.core.navigation.Routes
import com.devinterview.domain.model.*
import kotlin.math.roundToInt

fun NavGraphBuilder.quizGraph(nav: NavController) {
    composable<Routes.QuizHome> { QuizHomeScreen(nav) }
    composable<Routes.QuizConfig> {
        QuizConfigScreen(onBack = { nav.popBackStack() }, onStart = { id -> nav.navigate(Routes.QuizPlayer(id)) { popUpTo<Routes.QuizConfig> { inclusive = true } } })
    }
    composable<Routes.QuizPlayer> {
        QuizPlayerScreen(onExit = { nav.popBackStack() }, onUpgrade = { nav.navigate(Routes.Subscription) },
            onResult = { id -> nav.navigate(Routes.QuizResult(id)) { popUpTo<Routes.QuizPlayer> { inclusive = true } } })
    }
    composable<Routes.QuizResult> { QuizResultScreen(nav) }
}

@Composable
fun QuizHomeScreen(nav: NavController, viewModel: QuizHomeViewModel = hiltViewModel()) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    Scaffold(topBar = { DiTopBar("Quizzes", onBack = { nav.popBackStack() }) }, floatingActionButton = {
        ExtendedFloatingActionButton(onClick = { nav.navigate(Routes.QuizConfig()) }, icon = { Icon(Icons.Filled.Tune, null) }, text = { Text("Custom quiz") })
    }) { padding ->
        LoadableContent(state, onRetry = { viewModel.load() }, modifier = Modifier.padding(padding)) { data ->
            LazyColumn(Modifier.padding(padding), contentPadding = PaddingValues(16.dp, 16.dp, 16.dp, 88.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
                data.daily?.let { d ->
                    item {
                        GradientCard(onClick = { nav.navigate(Routes.QuizPlayer(d.id)) }, modifier = Modifier.testTag("daily_quiz")) {
                            Row(verticalAlignment = Alignment.CenterVertically) {
                                Icon(Icons.Filled.Today, null, tint = Color.White, modifier = Modifier.size(40.dp))
                                Spacer(Modifier.width(12.dp))
                                Column(Modifier.weight(1f)) {
                                    Text("Daily quiz", style = MaterialTheme.typography.titleLarge, color = Color.White)
                                    Text("${d.questionCount} questions${d.timeLimitSeconds?.let { " · ${it / 60} min" } ?: ""}", color = Color.White.copy(alpha = .9f))
                                    d.bestPercentage?.let { Text("Today's best: ${it.roundToInt()}%", color = Color.White, style = MaterialTheme.typography.labelLarge) }
                                }
                                Icon(Icons.Filled.PlayCircle, "Start", tint = Color.White, modifier = Modifier.size(40.dp))
                            }
                        }
                    }
                }
                item { SectionHeader("Available quizzes") }
                if (data.quizzes.isEmpty()) item { EmptyState("No quizzes yet", "Create a custom quiz to get started.") }
                items(data.quizzes, key = { it.id }) { q -> QuizCard(q) { nav.navigate(Routes.QuizPlayer(q.id)) } }
                if (data.history.isNotEmpty()) {
                    item { SectionHeader("Recent attempts") }
                    items(data.history, key = { it.attemptId }) { h ->
                        ListItem(
                            headlineContent = { Text(h.quizTitle) },
                            supportingContent = { Text("${h.correct}/${h.totalQuestions} correct · ${TimeFormat.relative(h.submittedAt ?: h.startedAt)}") },
                            trailingContent = {
                                if (h.status == "InProgress") TagChip("In progress")
                                else Text("${h.percentage.roundToInt()}%", color = if (h.passed) DiTheme.colors.success else DiTheme.colors.danger, fontWeight = FontWeight.Bold)
                            },
                            modifier = Modifier.selectable(false) {
                                if (h.status == "InProgress") nav.navigate(Routes.QuizPlayer(h.quizId)) else nav.navigate(Routes.QuizResult(h.attemptId))
                            },
                        )
                    }
                }
            }
        }
    }
}

@OptIn(ExperimentalLayoutApi::class)
@Composable
private fun QuizCard(q: QuizSummary, onClick: () -> Unit) {
    DiCard(onClick = onClick) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            Column(Modifier.weight(1f)) {
                Text(q.title, style = MaterialTheme.typography.titleMedium)
                q.description?.let { Text(it, style = MaterialTheme.typography.bodySmall, maxLines = 2, overflow = TextOverflow.Ellipsis) }
                Spacer(Modifier.height(6.dp))
                FlowRow(horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                    TagChip("${q.questionCount} Qs")
                    q.timeLimitSeconds?.let { TagChip("${it / 60} min") }
                    q.difficulty?.let { DifficultyChip(it) }
                    if (q.negativeMarkingFraction > 0) TagChip("−${(q.negativeMarkingFraction * 100).roundToInt()}% per wrong")
                    if (q.isPremium) PremiumBadge()
                }
            }
            q.bestPercentage?.let { ScoreRing(it.roundToInt(), size = 52.dp, stroke = 5.dp) }
        }
    }
}

@OptIn(ExperimentalLayoutApi::class)
@Composable
fun QuizConfigScreen(onBack: () -> Unit, onStart: (String) -> Unit, viewModel: QuizConfigViewModel = hiltViewModel()) {
    val s by viewModel.state.collectAsStateWithLifecycle()
    LaunchedEffect(Unit) { viewModel.events.collect { if (it is QuizEvent.OpenPlayer) onStart(it.quizId) } }
    Scaffold(topBar = { DiTopBar("Custom quiz", onBack = onBack) }) { padding ->
        Column(Modifier.padding(padding).verticalScroll(rememberScrollState()).padding(16.dp), verticalArrangement = Arrangement.spacedBy(16.dp)) {
            DropdownField("Technology", s.technologies.map { it.id }, s.technologyId, viewModel::selectTechnology, Modifier.fillMaxWidth(),
                display = { id -> s.technologies.firstOrNull { it.id == id }?.name ?: "Choose" })
            if (s.categories.isNotEmpty()) {
                Text("Topics (optional)", style = MaterialTheme.typography.titleSmall)
                FlowRow(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    s.categories.forEach { c -> FilterChip(c.id in s.categoryIds, onClick = { viewModel.toggleCategory(c.id) }, label = { Text(c.name) }) }
                }
            }
            Text("Difficulty", style = MaterialTheme.typography.titleSmall)
            FlowRow(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                FilterChip(s.difficulty == null, onClick = { viewModel.update { it.copy(difficulty = null) } }, label = { Text("Mixed") })
                listOf("Beginner", "Intermediate", "Advanced", "Expert").forEach { d ->
                    FilterChip(s.difficulty == d, onClick = { viewModel.update { it.copy(difficulty = d) } }, label = { Text(d) })
                }
            }
            Text("Questions: ${s.questionCount}", style = MaterialTheme.typography.titleSmall)
            Slider(s.questionCount.toFloat(), { v -> viewModel.update { it.copy(questionCount = v.roundToInt()) } }, valueRange = 5f..50f, steps = 8)
            Row(verticalAlignment = Alignment.CenterVertically) {
                Text("Timed", Modifier.weight(1f), style = MaterialTheme.typography.titleSmall)
                Switch(s.timed, { v -> viewModel.update { it.copy(timed = v) } })
            }
            if (s.timed) {
                Text("Time limit: ${s.minutes} min", style = MaterialTheme.typography.bodyMedium)
                Slider(s.minutes.toFloat(), { v -> viewModel.update { it.copy(minutes = v.roundToInt()) } }, valueRange = 2f..60f)
            }
            Row(verticalAlignment = Alignment.CenterVertically) {
                Column(Modifier.weight(1f)) {
                    Text("Negative marking", style = MaterialTheme.typography.titleSmall)
                    Text("Wrong answers deduct 25% of a question's points.", style = MaterialTheme.typography.bodySmall)
                }
                Switch(s.negativeMarking, { v -> viewModel.update { it.copy(negativeMarking = v) } })
            }
            s.error?.let { Text(it, color = MaterialTheme.colorScheme.error) }
            PrimaryButton("Start quiz", onClick = viewModel::create, loading = s.creating, modifier = Modifier.fillMaxWidth().testTag("start_quiz"), icon = Icons.Filled.PlayArrow)
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun QuizPlayerScreen(onExit: () -> Unit, onUpgrade: () -> Unit, onResult: (String) -> Unit, viewModel: QuizPlayerViewModel = hiltViewModel()) {
    val s by viewModel.state.collectAsStateWithLifecycle()
    var confirmExit by remember { mutableStateOf(false) }
    LaunchedEffect(Unit) { viewModel.events.collect { if (it is QuizEvent.OpenResult) onResult(it.attemptId) } }
    BackHandler(enabled = s.attempt is Loadable.Ready) { confirmExit = true }
    val attempt = s.attempt.dataOrNull
    Scaffold(topBar = {
        TopAppBar(
            title = { Text(attempt?.title ?: "Quiz", maxLines = 1, overflow = TextOverflow.Ellipsis) },
            navigationIcon = { IconButton(onClick = { if (attempt != null) confirmExit = true else onExit() }) { Icon(Icons.Filled.Close, "Exit quiz") } },
            actions = {
                s.remainingSeconds?.let { secs ->
                    val low = secs <= 60
                    Surface(shape = CircleShape, color = if (low) MaterialTheme.colorScheme.errorContainer else MaterialTheme.colorScheme.secondaryContainer) {
                        Row(Modifier.padding(horizontal = 12.dp, vertical = 6.dp), verticalAlignment = Alignment.CenterVertically) {
                            Icon(Icons.Filled.Timer, null, Modifier.size(18.dp)); Spacer(Modifier.width(4.dp))
                            Text(TimeFormat.clock(secs), style = MaterialTheme.typography.labelLarge, modifier = Modifier.testTag("quiz_timer"))
                        }
                    }
                    Spacer(Modifier.width(8.dp))
                }
            },
        )
    }) { padding ->
        LoadableContent(s.attempt, onRetry = { viewModel.start() }, modifier = Modifier.padding(padding), onUpgrade = onUpgrade) { a ->
            val question = a.questions.getOrNull(s.index) ?: return@LoadableContent
            val answer = s.answers[question.questionId]
            Column(Modifier.padding(padding).fillMaxSize()) {
                AnimatedProgressBar((s.index + 1f) / a.questions.size, Modifier.padding(horizontal = 16.dp))
                LazyRow(contentPadding = PaddingValues(16.dp, 8.dp), horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                    itemsIndexed(a.questions, key = { _, q -> q.questionId }) { i, q ->
                        val ans = s.answers[q.questionId]
                        val bg = when {
                            i == s.index -> MaterialTheme.colorScheme.primary
                            ans?.isMarkedForReview == true -> DiTheme.colors.warning
                            ans?.selectedOptionIds?.isNotEmpty() == true -> MaterialTheme.colorScheme.secondaryContainer
                            else -> MaterialTheme.colorScheme.surfaceVariant
                        }
                        Box(Modifier.size(34.dp).clip(CircleShape).background(bg).selectable(i == s.index) { viewModel.goTo(i) }, contentAlignment = Alignment.Center) {
                            Text("${i + 1}", style = MaterialTheme.typography.labelMedium, color = if (i == s.index) MaterialTheme.colorScheme.onPrimary else MaterialTheme.colorScheme.onSurface)
                        }
                    }
                }
                AnimatedContent(s.index, Modifier.weight(1f), label = "question") { _ ->
                    Column(Modifier.verticalScroll(rememberScrollState()).padding(16.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
                        Text("Question ${s.index + 1} of ${a.questions.size}", style = MaterialTheme.typography.labelLarge, color = MaterialTheme.colorScheme.primary)
                        Text(question.title, style = MaterialTheme.typography.titleLarge)
                        if (question.body.isNotBlank() && question.body != question.title) MarkdownText(question.body)
                        question.codeSnippet?.let { CodeBlock(it, question.codeLanguage) }
                        if (question.allowsMultipleAnswers) Text("Select all that apply", style = MaterialTheme.typography.labelMedium)
                        question.options.sortedBy { it.displayOrder }.forEach { o ->
                            val selected = answer?.selectedOptionIds?.contains(o.id) == true
                            OutlinedCard(Modifier.fillMaxWidth().selectable(selected) { viewModel.toggleOption(question, o.id) }.testTag("quiz_option_${o.displayOrder}"),
                                border = BorderStroke(if (selected) 2.dp else 1.dp, if (selected) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.outlineVariant)) {
                                Row(Modifier.padding(12.dp), verticalAlignment = Alignment.CenterVertically) {
                                    if (question.allowsMultipleAnswers) Checkbox(selected, null) else RadioButton(selected, null)
                                    Spacer(Modifier.width(8.dp)); Text(o.text, style = MaterialTheme.typography.bodyLarge)
                                }
                            }
                        }
                        FilterChip(answer?.isMarkedForReview == true, onClick = { viewModel.toggleMark(question) }, label = { Text("Mark for review") },
                            leadingIcon = { Icon(Icons.Filled.Flag, null, Modifier.size(18.dp)) })
                    }
                }
                Row(Modifier.fillMaxWidth().padding(16.dp), horizontalArrangement = Arrangement.spacedBy(12.dp)) {
                    SecondaryButton("Previous", onClick = { viewModel.goTo(s.index - 1) }, enabled = s.index > 0, modifier = Modifier.weight(1f), icon = Icons.AutoMirrored.Filled.ArrowBack)
                    if (s.index < a.questions.lastIndex) PrimaryButton("Next", onClick = { viewModel.goTo(s.index + 1) }, modifier = Modifier.weight(1f).testTag("quiz_next"), icon = Icons.AutoMirrored.Filled.ArrowForward)
                    else PrimaryButton("Submit", onClick = viewModel::requestSubmit, loading = s.submitting, modifier = Modifier.weight(1f).testTag("quiz_submit"), icon = Icons.Filled.Send)
                }
            }
        }
    }
    if (s.showSubmitDialog) {
        val p = s.progress
        ConfirmDialog("Submit quiz?",
            buildString {
                append("Answered: ${p?.answered ?: 0}\nUnanswered: ${p?.unanswered ?: 0}")
                if ((p?.markedForReview ?: 0) > 0) append("\nMarked for review: ${p?.markedForReview}")
                append("\n\nYou can't change answers after submitting.")
            }, "Submit", onConfirm = { viewModel.submit() }, onDismiss = viewModel::dismissSubmit)
    }
    if (confirmExit) ConfirmDialog("Leave quiz?", "Your answers are saved and you can resume later" + (if (s.remainingSeconds != null) ", but the timer keeps running." else "."),
        "Leave", onConfirm = { confirmExit = false; onExit() }, onDismiss = { confirmExit = false })
    s.error?.let { AlertDialog(onDismissRequest = viewModel::clearError, confirmButton = { TextButton(onClick = viewModel::clearError) { Text("OK") } }, title = { Text("Couldn't submit") }, text = { Text(it) }) }
}

@Composable
fun QuizResultScreen(nav: NavController, viewModel: QuizResultViewModel = hiltViewModel()) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    val filter by viewModel.reviewFilter.collectAsStateWithLifecycle()
    Scaffold(topBar = { DiTopBar("Results", onBack = { nav.popBackStack() }) }) { padding ->
        LoadableContent(state, onRetry = { viewModel.load() }, modifier = Modifier.padding(padding)) { r ->
            LazyColumn(Modifier.padding(padding).testTag("quiz_result"), contentPadding = PaddingValues(16.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
                item {
                    DiCard {
                        Column(Modifier.fillMaxWidth(), horizontalAlignment = Alignment.CenterHorizontally) {
                            Text(r.quizTitle, style = MaterialTheme.typography.titleMedium)
                            Spacer(Modifier.height(12.dp))
                            ScoreRing(r.percentage.roundToInt(), label = if (r.passed) "Passed" else "Keep going",
                                color = if (r.passed) DiTheme.colors.success else DiTheme.colors.warning)
                            Spacer(Modifier.height(8.dp))
                            Text("Score ${"%.2f".format(r.score)} / ${"%.0f".format(r.maxScore)} · pass mark ${r.passingPercentage}%", style = MaterialTheme.typography.bodyMedium)
                            if (r.status == "Expired") Text("Time ran out — unanswered questions were counted as skipped.", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.error)
                            if (r.xpAwarded > 0) Text("+${r.xpAwarded} XP", style = MaterialTheme.typography.titleMedium, color = MaterialTheme.colorScheme.primary)
                        }
                    }
                }
                item {
                    Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                        StatTile("Correct", "${r.correct}", Icons.Filled.CheckCircle, Modifier.weight(1f), tint = DiTheme.colors.success)
                        StatTile("Wrong", "${r.incorrect}", Icons.Filled.Cancel, Modifier.weight(1f), tint = DiTheme.colors.danger)
                        StatTile("Skipped", "${r.skipped}", Icons.Filled.RemoveCircleOutline, Modifier.weight(1f), tint = MaterialTheme.colorScheme.outline)
                    }
                }
                item {
                    Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                        StatTile("Accuracy", "${r.accuracy.roundToInt()}%", Icons.Filled.TrackChanges, Modifier.weight(1f))
                        StatTile("Time", TimeFormat.clock(r.totalTimeSeconds.toLong()), Icons.Filled.Timer, Modifier.weight(1f))
                    }
                }
                if (r.topicBreakdown.isNotEmpty()) item {
                    DiCard {
                        Text("By topic", style = MaterialTheme.typography.titleMedium)
                        r.topicBreakdown.forEach { t ->
                            Row(Modifier.fillMaxWidth().padding(vertical = 4.dp), verticalAlignment = Alignment.CenterVertically) {
                                Text(t.name, Modifier.weight(1f), style = MaterialTheme.typography.bodyMedium)
                                Text("${t.correct}/${t.total}", style = MaterialTheme.typography.labelLarge)
                            }
                            AnimatedProgressBar((t.accuracy / 100).toFloat(), color = if (t.accuracy >= 60) DiTheme.colors.success else DiTheme.colors.warning)
                        }
                    }
                }
                if (r.recommendedTopics.isNotEmpty()) item {
                    DiCard {
                        Text("Recommended revision", style = MaterialTheme.typography.titleMedium)
                        BulletList(r.recommendedTopics, icon = Icons.Filled.Lightbulb, tint = DiTheme.colors.warning)
                    }
                }
                item {
                    Row(horizontalArrangement = Arrangement.spacedBy(12.dp)) {
                        SecondaryButton("Retake", onClick = { nav.navigate(Routes.QuizPlayer(r.quizId)) { popUpTo<Routes.QuizResult> { inclusive = true } } }, modifier = Modifier.weight(1f), icon = Icons.Filled.Replay)
                        PrimaryButton("More quizzes", onClick = { nav.navigate(Routes.QuizHome) { popUpTo<Routes.QuizHome> { inclusive = true } } }, modifier = Modifier.weight(1f))
                    }
                }
                item {
                    SectionHeader("Review answers")
                    Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                        listOf("all" to "All", "wrong" to "Wrong", "skipped" to "Skipped").forEach { (k, l) ->
                            FilterChip(filter == k, onClick = { viewModel.reviewFilter.value = k }, label = { Text(l) })
                        }
                    }
                }
                val review = r.review.filter { when (filter) { "wrong" -> it.isCorrect == false && !it.skipped; "skipped" -> it.skipped; else -> true } }
                items(review, key = { it.questionId }) { item -> ReviewCard(item) }
            }
        }
    }
}

@Composable
private fun ReviewCard(item: QuizReviewItem) {
    var expanded by remember { mutableStateOf(false) }
    DiCard(onClick = { expanded = !expanded }) {
        Row(verticalAlignment = Alignment.Top) {
            Icon(
                when { item.skipped -> Icons.Filled.RemoveCircleOutline; item.isCorrect == true -> Icons.Filled.CheckCircle; else -> Icons.Filled.Cancel },
                null, tint = when { item.skipped -> MaterialTheme.colorScheme.outline; item.isCorrect == true -> DiTheme.colors.success; else -> DiTheme.colors.danger },
            )
            Spacer(Modifier.width(8.dp))
            Column(Modifier.weight(1f)) {
                Text("${item.order + 1}. ${item.title}", style = MaterialTheme.typography.titleSmall)
                Text("${item.category} · ${"%+.2f".format(item.pointsAwarded)} pts", style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
            }
            Icon(if (expanded) Icons.Filled.ExpandLess else Icons.Filled.ExpandMore, null)
        }
        if (expanded) {
            Spacer(Modifier.height(8.dp))
            item.codeSnippet?.let { CodeBlock(it, null) }
            item.options.forEach { o ->
                val color = when { o.isCorrect -> DiTheme.colors.success; o.selected -> DiTheme.colors.danger; else -> MaterialTheme.colorScheme.onSurface }
                Row(Modifier.padding(vertical = 2.dp)) {
                    Text(if (o.selected) "●" else "○", color = color); Spacer(Modifier.width(6.dp))
                    Column {
                        Text(o.text, color = color, fontWeight = if (o.isCorrect) FontWeight.SemiBold else FontWeight.Normal)
                        o.explanation?.let { Text(it, style = MaterialTheme.typography.bodySmall) }
                    }
                }
            }
            if (item.explanation.isNotBlank()) { Spacer(Modifier.height(6.dp)); MarkdownText(item.explanation) }
        }
    }
}
