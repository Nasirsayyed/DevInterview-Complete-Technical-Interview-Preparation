package com.devinterview.feature.mockinterview

import android.app.Activity
import android.content.ActivityNotFoundException
import android.content.Intent
import android.speech.RecognizerIntent
import android.speech.tts.TextToSpeech
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.lazy.rememberLazyListState
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.Send
import androidx.compose.material.icons.filled.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.testTag
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
import kotlinx.coroutines.launch
import java.util.Locale

fun NavGraphBuilder.interviewGraph(nav: NavController, openDrawer: () -> Unit) {
    composable<Routes.MockInterviewHome> { InterviewHomeScreen(nav, openDrawer) }
    composable<Routes.InterviewSetup> { InterviewSetupScreen(nav) }
    composable<Routes.InterviewChat> { InterviewChatScreen(nav) }
    composable<Routes.InterviewReport> { InterviewReportScreen(nav) }
    composable<Routes.AnswerEvaluator> { AnswerEvaluatorScreen(nav) }
    composable<Routes.EvaluationDetail> { EvaluationDetailScreen(nav) }
    composable<Routes.CodeReviewer> { CodeReviewerScreen(nav) }
}

@Composable
private fun AiUnavailableCard(status: AiStatus?, onUpgrade: () -> Unit) {
    if (status == null || status.available && (status.isPremium || status.remainingToday > 0)) return
    DiCard {
        Row(verticalAlignment = Alignment.CenterVertically) {
            Icon(Icons.Filled.Info, null, tint = DiTheme.colors.warning)
            Spacer(Modifier.width(12.dp))
            Column(Modifier.weight(1f)) {
                Text(if (!status.available) "AI features are temporarily unavailable" else "Daily AI limit reached", style = MaterialTheme.typography.titleSmall)
                Text(status.reason ?: "Upgrade to Premium for more AI interviews and evaluations.", style = MaterialTheme.typography.bodySmall)
            }
        }
        if (status.available && !status.isPremium) TextButton(onClick = onUpgrade) { Text("See Premium") }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun InterviewHomeScreen(nav: NavController, openDrawer: () -> Unit, viewModel: InterviewHomeViewModel = hiltViewModel()) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    Scaffold(topBar = {
        TopAppBar(title = { Text("Mock interview") }, navigationIcon = { IconButton(onClick = openDrawer) { Icon(Icons.Filled.Menu, "Open menu") } })
    }) { padding ->
        LoadableContent(state, onRetry = { viewModel.load() }, modifier = Modifier.padding(padding)) { data ->
            LazyColumn(Modifier.padding(padding), contentPadding = PaddingValues(16.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
                item { AiUnavailableCard(data.status) { nav.navigate(Routes.Subscription) } }
                item {
                    GradientCard(onClick = { nav.navigate(Routes.InterviewSetup()) }, modifier = Modifier.testTag("start_interview")) {
                        Icon(Icons.Filled.RecordVoiceOver, null, tint = Color.White, modifier = Modifier.size(36.dp))
                        Text("Start a mock interview", style = MaterialTheme.typography.titleLarge, color = Color.White)
                        Text("An AI interviewer adapts questions and follow-ups to your answers, then gives a detailed report.", color = Color.White.copy(alpha = .9f))
                        data.status?.takeIf { it.available && !it.isPremium }?.let { Text("${it.remainingToday} AI requests left today", color = Color.White, style = MaterialTheme.typography.labelLarge) }
                    }
                }
                item {
                    Row(horizontalArrangement = Arrangement.spacedBy(12.dp)) {
                        DiCard(Modifier.weight(1f), onClick = { nav.navigate(Routes.AnswerEvaluator()) }) {
                            Icon(Icons.Filled.Grading, null, tint = MaterialTheme.colorScheme.primary); Text("Answer evaluator", style = MaterialTheme.typography.titleSmall)
                            Text("Score a written answer", style = MaterialTheme.typography.bodySmall)
                        }
                        DiCard(Modifier.weight(1f), onClick = { nav.navigate(Routes.CodeReviewer) }) {
                            Icon(Icons.Filled.Reviews, null, tint = MaterialTheme.colorScheme.secondary); Text("Code reviewer", style = MaterialTheme.typography.titleSmall)
                            Text("Quality & security review", style = MaterialTheme.typography.bodySmall)
                        }
                    }
                }
                item { SectionHeader("Quick start") }
                item {
                    Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                        listOf("Hr", "DotNetDeveloper", "SystemDesign").forEach { t ->
                            AssistChip(onClick = { nav.navigate(Routes.InterviewSetup(t)) }, label = { Text(interviewTypeLabel(t), maxLines = 1) })
                        }
                    }
                }
                item { SectionHeader("Interview history") }
                if (data.interviews.isEmpty()) item { EmptyState("No interviews yet", "Your completed interviews and reports appear here.", icon = Icons.Filled.History) }
                items(data.interviews, key = { it.id }) { i ->
                    ListItem(
                        headlineContent = { Text("${interviewTypeLabel(i.type)} · ${i.technology}") },
                        supportingContent = { Text("${i.status} · ${TimeFormat.relative(i.completedAt ?: i.startedAt)}") },
                        trailingContent = { i.overallScore?.let { ScoreRing(it, size = 44.dp, stroke = 4.dp) } },
                        modifier = Modifier.clickable { nav.navigate(if (i.status == "InProgress") Routes.InterviewChat(i.id) else Routes.InterviewReport(i.id)) },
                    )
                }
                if (data.evaluations.isNotEmpty()) {
                    item { SectionHeader("Recent answer evaluations") }
                    items(data.evaluations, key = { "e" + it.id }) { e ->
                        ListItem(headlineContent = { Text(e.questionText, maxLines = 2, overflow = TextOverflow.Ellipsis) },
                            supportingContent = { Text(TimeFormat.relative(e.createdAt)) }, trailingContent = { Text("${e.overallScore}/100") },
                            modifier = Modifier.clickable { nav.navigate(Routes.EvaluationDetail(e.id)) })
                    }
                }
            }
        }
    }
}

@OptIn(ExperimentalLayoutApi::class)
@Composable
fun InterviewSetupScreen(nav: NavController, viewModel: InterviewSetupViewModel = hiltViewModel()) {
    val s by viewModel.state.collectAsStateWithLifecycle()
    LaunchedEffect(Unit) { viewModel.events.collect { if (it is AiEvent.Open) nav.navigate(it.route) { popUpTo<Routes.InterviewSetup> { inclusive = true } } } }
    Scaffold(topBar = { DiTopBar("Interview setup", onBack = { nav.popBackStack() }) }) { padding ->
        Column(Modifier.padding(padding).verticalScroll(rememberScrollState()).padding(16.dp), verticalArrangement = Arrangement.spacedBy(14.dp)) {
            Text("Interview type", style = MaterialTheme.typography.titleSmall)
            FlowRow(horizontalArrangement = Arrangement.spacedBy(8.dp), verticalArrangement = Arrangement.spacedBy(4.dp)) {
                InterviewTypes.forEach { (k, l) -> FilterChip(s.type == k, onClick = { viewModel.update { it.copy(type = k) } }, label = { Text(l) }) }
            }
            DiTextField(s.technology, { v -> viewModel.update { it.copy(technology = v.take(80)) } }, "Technology / stack", supportingText = "e.g. C#, ASP.NET Core, React, SQL Server")
            DiTextField(s.targetRole, { v -> viewModel.update { it.copy(targetRole = v.take(80)) } }, "Target role")
            DropdownField("Experience level", Levels, s.level, { v -> viewModel.update { it.copy(level = v) } }, Modifier.fillMaxWidth())
            DropdownField("Starting difficulty", Levels, s.difficulty, { v -> viewModel.update { it.copy(difficulty = v) } }, Modifier.fillMaxWidth())
            Text("Questions: ${s.questionCount}", style = MaterialTheme.typography.titleSmall)
            Slider(s.questionCount.toFloat(), { v -> viewModel.update { it.copy(questionCount = v.toInt()) } }, valueRange = 3f..15f, steps = 11)
            Text("Duration: ${s.durationMinutes} min", style = MaterialTheme.typography.titleSmall)
            Slider(s.durationMinutes.toFloat(), { v -> viewModel.update { it.copy(durationMinutes = (v / 5).toInt() * 5) } }, valueRange = 10f..90f)
            Text("Answer by typing or with your voice. Difficulty adapts to your answers. AI feedback is a practice aid, not a hiring decision.",
                style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
            s.error?.let { e -> if (e is AppError.PremiumRequired || e is AppError.QuotaExceeded) PremiumGate(e.message, onUpgrade = { nav.navigate(Routes.Subscription) }) else Text(e.message, color = MaterialTheme.colorScheme.error) }
            PrimaryButton("Start interview", onClick = viewModel::start, loading = s.starting, modifier = Modifier.fillMaxWidth().testTag("begin_interview"), icon = Icons.Filled.PlayArrow)
        }
    }
}

/** Text-to-speech for interviewer questions. Degrades silently (button hidden) when no TTS engine is available. */
@Composable
private fun rememberSpeaker(): Pair<Boolean, (String) -> Unit> {
    val context = LocalContext.current
    var ready by remember { mutableStateOf(false) }
    val tts = remember {
        var engine: TextToSpeech? = null
        engine = TextToSpeech(context) { status ->
            ready = status == TextToSpeech.SUCCESS && engine?.setLanguage(Locale.getDefault())?.let { it >= TextToSpeech.LANG_AVAILABLE } == true
        }
        engine
    }
    DisposableEffect(Unit) { onDispose { tts.stop(); tts.shutdown() } }
    return ready to { text: String -> tts.speak(text, TextToSpeech.QUEUE_FLUSH, null, "q") }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun InterviewChatScreen(nav: NavController, viewModel: InterviewChatViewModel = hiltViewModel()) {
    val s by viewModel.state.collectAsStateWithLifecycle()
    val listState = rememberLazyListState()
    val snackbar = remember { SnackbarHostState() }
    val scope = rememberCoroutineScope()
    var confirmFinish by remember { mutableStateOf(false) }
    var autoSpeak by remember { mutableStateOf(false) }
    val (ttsReady, speak) = rememberSpeaker()
    val voiceLauncher = rememberLauncherForActivityResult(ActivityResultContracts.StartActivityForResult()) { result ->
        if (result.resultCode == Activity.RESULT_OK) {
            result.data?.getStringArrayListExtra(RecognizerIntent.EXTRA_RESULTS)?.firstOrNull()?.let(viewModel::onVoiceText)
        }
    }
    LaunchedEffect(Unit) {
        viewModel.events.collect { if (it is AiEvent.Open) nav.navigate(it.route) { popUpTo<Routes.InterviewChat> { inclusive = true } } }
    }
    LaunchedEffect(s.error) { s.error?.let { snackbar.showSnackbar(it); viewModel.clearError() } }
    val interview = s.interview.dataOrNull
    val messages = interview?.messages.orEmpty()
    LaunchedEffect(messages.size) {
        if (messages.isNotEmpty()) listState.animateScrollToItem(messages.lastIndex)
        messages.lastOrNull()?.takeIf { autoSpeak && ttsReady && it.role == "interviewer" }?.let { speak(it.text) }
    }
    Scaffold(
        topBar = {
            TopAppBar(
                title = {
                    Column {
                        Text(interview?.let { "${interviewTypeLabel(it.type)} interview" } ?: "Interview", maxLines = 1)
                        interview?.let { Text("Question ${it.mainQuestionsAsked} of ${it.plannedQuestionCount} · ${it.currentDifficulty}", style = MaterialTheme.typography.labelSmall) }
                    }
                },
                navigationIcon = { IconButton(onClick = { nav.popBackStack() }) { Icon(Icons.Filled.Close, "Leave (you can resume later)") } },
                actions = {
                    if (ttsReady) IconButton(onClick = { autoSpeak = !autoSpeak }) {
                        Icon(if (autoSpeak) Icons.Filled.VolumeUp else Icons.Filled.VolumeOff, if (autoSpeak) "Stop reading questions aloud" else "Read questions aloud")
                    }
                    TextButton(onClick = { confirmFinish = true }, enabled = interview != null && !s.finishing) { Text("Finish") }
                },
            )
        },
        snackbarHost = { SnackbarHost(snackbar) },
    ) { padding ->
        LoadableContent(s.interview, onRetry = { viewModel.load() }, modifier = Modifier.padding(padding), onUpgrade = { nav.navigate(Routes.Subscription) }) { iv ->
            Column(Modifier.padding(padding).fillMaxSize().imePadding()) {
                LazyColumn(Modifier.weight(1f).testTag("chat_list"), state = listState, contentPadding = PaddingValues(12.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    items(messages.size) { i -> ChatBubble(messages[i], onSpeak = if (ttsReady) speak else null) }
                    if (s.sending) item { Row(verticalAlignment = Alignment.CenterVertically) { CircularProgressIndicator(Modifier.size(18.dp), strokeWidth = 2.dp); Spacer(Modifier.width(8.dp)); Text("Interviewer is thinking…", style = MaterialTheme.typography.bodySmall) } }
                    if (s.finishing) item { Text("Preparing your report…", style = MaterialTheme.typography.bodySmall) }
                }
                if (iv.currentQuestion != null && !s.finishing) {
                    Surface(tonalElevation = 3.dp) {
                        Column(Modifier.padding(8.dp).navigationBarsPadding()) {
                            OutlinedTextField(s.draft, viewModel::onDraft, Modifier.fillMaxWidth().heightIn(min = 56.dp, max = 180.dp).testTag("answer_input"),
                                placeholder = { Text("Type your answer…") }, enabled = !s.sending)
                            Row(verticalAlignment = Alignment.CenterVertically) {
                                IconButton(onClick = {
                                    val intent = Intent(RecognizerIntent.ACTION_RECOGNIZE_SPEECH)
                                        .putExtra(RecognizerIntent.EXTRA_LANGUAGE_MODEL, RecognizerIntent.LANGUAGE_MODEL_FREE_FORM)
                                        .putExtra(RecognizerIntent.EXTRA_PROMPT, "Speak your answer")
                                    try { voiceLauncher.launch(intent) } catch (_: ActivityNotFoundException) {
                                        scope.launch { snackbar.showSnackbar("Speech recognition isn't available on this device — please type your answer.") }
                                    }
                                }, enabled = !s.sending) { Icon(Icons.Filled.Mic, "Answer with voice") }
                                TextButton(onClick = viewModel::skip, enabled = !s.sending) { Text("Skip") }
                                Spacer(Modifier.weight(1f))
                                Text("${s.draft.length}/6000", style = MaterialTheme.typography.labelSmall)
                                Spacer(Modifier.width(8.dp))
                                FilledIconButton(onClick = viewModel::send, enabled = s.draft.isNotBlank() && !s.sending, modifier = Modifier.testTag("send_answer")) {
                                    Icon(Icons.AutoMirrored.Filled.Send, "Send answer")
                                }
                            }
                        }
                    }
                }
            }
        }
    }
    if (confirmFinish) ConfirmDialog("Finish interview?", "You'll get a report based on the questions answered so far.", "Finish",
        { confirmFinish = false; viewModel.finish() }, { confirmFinish = false })
}

@Composable
private fun ChatBubble(m: InterviewMessage, onSpeak: ((String) -> Unit)?) {
    val mine = m.role == "candidate"
    Row(Modifier.fillMaxWidth(), horizontalArrangement = if (mine) Arrangement.End else Arrangement.Start) {
        Surface(
            color = if (mine) MaterialTheme.colorScheme.primaryContainer else MaterialTheme.colorScheme.surfaceVariant,
            shape = RoundedCornerShape(16.dp, 16.dp, if (mine) 4.dp else 16.dp, if (mine) 16.dp else 4.dp),
            modifier = Modifier.widthIn(max = 320.dp),
        ) {
            Column(Modifier.padding(12.dp)) {
                if (!mine && m.isFollowUp) Text("Follow-up", style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.primary)
                Text(m.text, style = MaterialTheme.typography.bodyMedium)
                if (!mine && onSpeak != null) IconButton(onClick = { onSpeak(m.text) }, modifier = Modifier.size(28.dp)) { Icon(Icons.Filled.VolumeUp, "Read aloud", Modifier.size(18.dp)) }
            }
        }
    }
}

@Composable
private fun ScoreBars(scores: List<Pair<String, Int>>) {
    scores.forEach { (label, v) ->
        Row(Modifier.fillMaxWidth().padding(vertical = 3.dp), verticalAlignment = Alignment.CenterVertically) {
            Text(label, Modifier.width(130.dp), style = MaterialTheme.typography.bodySmall)
            AnimatedProgressBar(v / 100f, Modifier.weight(1f), color = if (v >= 70) DiTheme.colors.success else if (v >= 45) DiTheme.colors.warning else DiTheme.colors.danger)
            Text(" $v", style = MaterialTheme.typography.labelMedium)
        }
    }
}

@Composable
private fun ListCard(title: String, items: List<String>, icon: androidx.compose.ui.graphics.vector.ImageVector, tint: Color) {
    if (items.isEmpty()) return
    DiCard { Text(title, style = MaterialTheme.typography.titleMedium); Spacer(Modifier.height(4.dp)); BulletList(items, icon = icon, tint = tint) }
}

@Composable
fun InterviewReportScreen(nav: NavController, viewModel: InterviewReportViewModel = hiltViewModel()) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    Scaffold(topBar = { DiTopBar("Interview report", onBack = { nav.popBackStack() }) }) { padding ->
        LoadableContent(state, onRetry = { viewModel.load() }, modifier = Modifier.padding(padding)) { r ->
            LazyColumn(Modifier.padding(padding).testTag("interview_report"), contentPadding = PaddingValues(16.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
                item {
                    DiCard {
                        Column(Modifier.fillMaxWidth(), horizontalAlignment = Alignment.CenterHorizontally) {
                            Text("${interviewTypeLabel(r.type)} · ${r.technology}", style = MaterialTheme.typography.titleMedium)
                            Text(r.targetRole, style = MaterialTheme.typography.bodySmall)
                            Spacer(Modifier.height(8.dp))
                            ScoreRing(r.overallScore, label = "Overall")
                        }
                        Spacer(Modifier.height(8.dp))
                        RadarChart(listOf(ChartPoint("Technical", r.technicalScore.toFloat()), ChartPoint("Communication", r.communicationScore.toFloat()),
                            ChartPoint("Problem solving", r.problemSolvingScore.toFloat()), ChartPoint("Completeness", r.completenessScore.toFloat()),
                            ChartPoint("Practical", r.practicalKnowledgeScore.toFloat())), Modifier.align(Alignment.CenterHorizontally))
                        ScoreBars(listOf("Technical" to r.technicalScore, "Communication" to r.communicationScore, "Problem solving" to r.problemSolvingScore,
                            "Completeness" to r.completenessScore, "Practical knowledge" to r.practicalKnowledgeScore))
                    }
                }
                item { DiCard { Text("Summary", style = MaterialTheme.typography.titleMedium); Text(r.summary) } }
                item { ListCard("Strengths", r.strengths, Icons.Filled.ThumbUp, DiTheme.colors.success) }
                item { ListCard("Areas to improve", r.weaknesses, Icons.Filled.TrendingUp, DiTheme.colors.warning) }
                item { ListCard("Missing concepts", r.missingConcepts, Icons.Filled.HelpOutline, DiTheme.colors.danger) }
                item { ListCard("Recommendations", r.recommendations, Icons.Filled.Lightbulb, MaterialTheme.colorScheme.primary) }
                item { ListCard("Suggested learning path", r.learningPath, Icons.Filled.Route, MaterialTheme.colorScheme.secondary) }
                item { ListCard("Interaction signals", r.interactionSignals, Icons.Filled.Insights, MaterialTheme.colorScheme.tertiary) }
                if (r.recommendedPaths.isNotEmpty()) item {
                    DiCard {
                        Text("Recommended learning paths", style = MaterialTheme.typography.titleMedium)
                        r.recommendedPaths.forEach { p -> TextButton(onClick = { nav.navigate(Routes.LearningPathDetail(p.id)) }) { Text(p.name) } }
                    }
                }
                if (r.questions.isNotEmpty()) {
                    item { SectionHeader("Question-by-question") }
                    items(r.questions, key = { it.sequence }) { q ->
                        DiCard {
                            Row { Text("${if (q.isFollowUp) "Follow-up" else "Q${q.sequence}"}: ${q.question}", Modifier.weight(1f), style = MaterialTheme.typography.titleSmall); q.score?.let { Text("$it") } }
                            Text(q.answer.ifBlank { "(skipped)" }, style = MaterialTheme.typography.bodySmall, maxLines = 6, overflow = TextOverflow.Ellipsis)
                            q.feedback?.let { Text(it, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.primary) }
                        }
                    }
                }
                if (r.suggestedAnswers.isNotEmpty()) {
                    item { SectionHeader("Stronger answers") }
                    items(r.suggestedAnswers, key = { it.question }) { a ->
                        DiCard { Text(a.question, style = MaterialTheme.typography.titleSmall); Spacer(Modifier.height(4.dp)); MarkdownText(a.suggestedAnswer) }
                    }
                }
                item {
                    Text("AI-generated feedback for practice. It may contain mistakes and is not an assessment by a real employer.",
                        style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                }
                item { PrimaryButton("Practise again", onClick = { nav.navigate(Routes.InterviewSetup(r.type)) }, modifier = Modifier.fillMaxWidth()) }
            }
        }
    }
}

@Composable
private fun EvaluationContent(e: AnswerEvaluation) {
    Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
        DiCard {
            Row(verticalAlignment = Alignment.CenterVertically) {
                ScoreRing(e.overallScore, size = 96.dp, label = "Score")
                Spacer(Modifier.width(16.dp))
                Column(Modifier.weight(1f)) {
                    ScoreBars(listOf("Accuracy" to e.technicalAccuracy, "Completeness" to e.completeness, "Clarity" to e.clarity, "Examples" to e.practicalExamples))
                }
            }
            if (e.scoreCappedForLength) Text("Score limited because the answer is very short.", style = MaterialTheme.typography.labelSmall, color = DiTheme.colors.warning)
        }
        ListCard("Strengths", e.strengths, Icons.Filled.ThumbUp, DiTheme.colors.success)
        ListCard("Missing concepts", e.missingConcepts, Icons.Filled.HelpOutline, DiTheme.colors.warning)
        ListCard("Incorrect statements", e.incorrectStatements, Icons.Filled.ErrorOutline, DiTheme.colors.danger)
        ListCard("Suggestions", e.suggestions, Icons.Filled.Lightbulb, MaterialTheme.colorScheme.primary)
        if (e.improvedAnswer.isNotBlank()) DiCard {
            Text("Improved answer", style = MaterialTheme.typography.titleMedium); MarkdownText(e.improvedAnswer)
            if (e.whyBetter.isNotBlank()) { Spacer(Modifier.height(6.dp)); Text("Why it's better", style = MaterialTheme.typography.titleSmall); Text(e.whyBetter, style = MaterialTheme.typography.bodySmall) }
        }
        Text("AI feedback can be wrong — verify important details against official documentation.", style = MaterialTheme.typography.labelSmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant)
    }
}

@Composable
fun AnswerEvaluatorScreen(nav: NavController, viewModel: AnswerEvaluatorViewModel = hiltViewModel()) {
    val s by viewModel.state.collectAsStateWithLifecycle()
    val snackbar = remember { SnackbarHostState() }
    val scope = rememberCoroutineScope()
    var collections by remember { mutableStateOf<List<SavedCollection>?>(null) }
    LaunchedEffect(Unit) { viewModel.events.collect { if (it is AiEvent.Message) snackbar.showSnackbar(it.text) } }
    Scaffold(topBar = {
        DiTopBar("Answer evaluator", onBack = { nav.popBackStack() }) {
            if (s.result != null) IconButton(onClick = { scope.launch { collections = viewModel.collections() } }) { Icon(Icons.Filled.PlaylistAdd, "Save to collection") }
        }
    }, snackbarHost = { SnackbarHost(snackbar) }) { padding ->
        Column(Modifier.padding(padding).verticalScroll(rememberScrollState()).padding(16.dp).imePadding(), verticalArrangement = Arrangement.spacedBy(12.dp)) {
            val result = s.result
            if (result == null) {
                DiTextField(s.questionText, viewModel::onQuestion, "Interview question", singleLine = false, minLines = 2)
                DiTextField(s.answer, viewModel::onAnswer, "Your answer", singleLine = false, minLines = 8, supportingText = "${s.answer.length}/12000",
                    modifier = Modifier.testTag("evaluator_answer"))
                s.error?.let { e -> if (e is AppError.PremiumRequired || e is AppError.QuotaExceeded) PremiumGate(e.message, onUpgrade = { nav.navigate(Routes.Subscription) }) else Text(e.message, color = MaterialTheme.colorScheme.error) }
                PrimaryButton("Evaluate my answer", onClick = viewModel::evaluate, loading = s.evaluating, modifier = Modifier.fillMaxWidth().testTag("evaluate"), icon = Icons.Filled.AutoAwesome)
            } else {
                Text(result.questionText, style = MaterialTheme.typography.titleMedium)
                EvaluationContent(result)
                SecondaryButton("Try another answer", onClick = viewModel::reset, modifier = Modifier.fillMaxWidth())
            }
        }
    }
    collections?.let { list ->
        AlertDialog(onDismissRequest = { collections = null }, title = { Text("Save to collection") }, text = {
            if (list.isEmpty()) Text("Create a collection from the Collections screen first.")
            else Column { list.forEach { c -> ListItem(headlineContent = { Text(c.name) }, modifier = Modifier.clickable { viewModel.saveToCollection(c.id); collections = null }) } }
        }, confirmButton = { TextButton(onClick = { collections = null }) { Text("Close") } })
    }
}

@Composable
fun EvaluationDetailScreen(nav: NavController, viewModel: EvaluationDetailViewModel = hiltViewModel()) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    Scaffold(topBar = { DiTopBar("Evaluation", onBack = { nav.popBackStack() }) }) { padding ->
        LoadableContent(state, onRetry = { viewModel.load() }, modifier = Modifier.padding(padding)) { e ->
            Column(Modifier.padding(padding).verticalScroll(rememberScrollState()).padding(16.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
                Text(e.questionText, style = MaterialTheme.typography.titleMedium)
                DiCard { Text("Your answer", style = MaterialTheme.typography.labelLarge); Text(e.userAnswer, style = MaterialTheme.typography.bodyMedium) }
                EvaluationContent(e)
            }
        }
    }
}

@Composable
fun CodeReviewerScreen(nav: NavController, viewModel: CodeReviewerViewModel = hiltViewModel()) {
    val s by viewModel.state.collectAsStateWithLifecycle()
    Scaffold(topBar = { DiTopBar("AI code reviewer", onBack = { nav.popBackStack() }) }) { padding ->
        Column(Modifier.padding(padding).verticalScroll(rememberScrollState()).padding(16.dp).imePadding(), verticalArrangement = Arrangement.spacedBy(12.dp)) {
            val r = s.result
            if (r == null) {
                DropdownField("Language", ReviewLanguages.map { it.first }, s.language, { v -> viewModel.update { it.copy(language = v) } }, Modifier.fillMaxWidth(),
                    display = { k -> ReviewLanguages.first { it.first == k }.second })
                Text("Code", style = MaterialTheme.typography.titleSmall)
                CodeEditorField(s.code, { v -> if (v.length <= 16000) viewModel.update { it.copy(code = v) } }, s.language, Modifier.fillMaxWidth().heightIn(min = 240.dp).testTag("review_code"))
                DiTextField(s.context, { v -> viewModel.update { it.copy(context = v.take(1500)) } }, "What does this code do? (optional)", singleLine = false, minLines = 2)
                s.error?.let { e -> if (e is AppError.PremiumRequired || e is AppError.QuotaExceeded) PremiumGate(e.message, onUpgrade = { nav.navigate(Routes.Subscription) }) else Text(e.message, color = MaterialTheme.colorScheme.error) }
                PrimaryButton("Review code", onClick = viewModel::review, loading = s.reviewing, modifier = Modifier.fillMaxWidth(), icon = Icons.Filled.AutoAwesome)
            } else {
                DiCard {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        ScoreRing(r.overallScore, size = 96.dp, label = "Overall")
                        Spacer(Modifier.width(16.dp))
                        Column(Modifier.weight(1f)) {
                            ScoreBars(listOf("Quality" to r.scores.quality, "Performance" to r.scores.performance, "Security" to r.scores.security,
                                "Readability" to r.scores.readability, "Maintainability" to r.scores.maintainability, "Best practices" to r.scores.bestPractices))
                        }
                    }
                    if (r.timeComplexity.isNotBlank()) KeyValueRow("Time complexity", r.timeComplexity)
                    if (r.spaceComplexity.isNotBlank()) KeyValueRow("Space complexity", r.spaceComplexity)
                }
                if (r.issues.isNotEmpty()) {
                    Text("Issues (${r.issues.size})", style = MaterialTheme.typography.titleMedium)
                    r.issues.forEach { i ->
                        DiCard {
                            Row(verticalAlignment = Alignment.CenterVertically) {
                                TagChip(i.severity); Spacer(Modifier.width(6.dp)); TagChip(i.category)
                                i.line?.let { Spacer(Modifier.width(6.dp)); Text("line $it", style = MaterialTheme.typography.labelSmall) }
                            }
                            Text(i.title, style = MaterialTheme.typography.titleSmall)
                            Text(i.description, style = MaterialTheme.typography.bodySmall)
                            Text("Fix: ${i.recommendation}", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.primary)
                        }
                    }
                }
                ListCard("Potential bugs", r.potentialBugs, Icons.Filled.BugReport, DiTheme.colors.danger)
                if (r.optimizedCode.isNotBlank()) { Text("Suggested version", style = MaterialTheme.typography.titleMedium); CodeBlock(r.optimizedCode, r.languageCode) }
                if (r.explanation.isNotBlank()) DiCard { MarkdownText(r.explanation) }
                Text(r.disclaimer.ifBlank { "AI review is not a substitute for a professional security audit." }, style = MaterialTheme.typography.labelSmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant)
                SecondaryButton("Review other code", onClick = { viewModel.update { it.copy(result = null) } }, modifier = Modifier.fillMaxWidth())
            }
        }
    }
}
