package com.devinterview.feature.coding

import androidx.compose.foundation.clickable
import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.LazyRow
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.lazy.rememberLazyListState
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.text.selection.SelectionContainer
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
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
import kotlin.math.roundToInt

fun NavGraphBuilder.codingGraph(nav: NavController) {
    composable<Routes.ChallengeList> { ChallengeListScreen(nav) }
    composable<Routes.ChallengeDetail> { ChallengeDetailScreen(nav) }
    composable<Routes.CodeEditor> { CodeEditorScreen(nav) }
    composable<Routes.Submissions> { SubmissionsScreen(nav) }
    composable<Routes.SubmissionDetail> { SubmissionDetailScreen(nav) }
}

@Composable
fun ChallengeListScreen(nav: NavController, viewModel: ChallengeListViewModel = hiltViewModel()) {
    val s by viewModel.state.collectAsStateWithLifecycle()
    val listState = rememberLazyListState()
    val nearEnd by remember { derivedStateOf { listState.layoutInfo.visibleItemsInfo.lastOrNull()?.index?.let { it >= listState.layoutInfo.totalItemsCount - 3 } == true } }
    LaunchedEffect(nearEnd) { if (nearEnd) viewModel.loadMore() }
    Scaffold(topBar = {
        DiTopBar("Coding challenges", onBack = { nav.popBackStack() }) {
            IconButton(onClick = { nav.navigate(Routes.Submissions()) }) { Icon(Icons.Filled.History, "My submissions") }
        }
    }) { padding ->
        Column(Modifier.padding(padding)) {
            OutlinedTextField(s.search, { v -> viewModel.update { it.copy(search = v) } }, Modifier.fillMaxWidth().padding(16.dp, 4.dp),
                placeholder = { Text("Search challenges") }, leadingIcon = { Icon(Icons.Filled.Search, null) }, singleLine = true)
            LazyRow(contentPadding = PaddingValues(16.dp, 4.dp), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                items(listOf("Easy", "Medium", "Hard")) { d ->
                    FilterChip(s.difficulty == d, onClick = { viewModel.update { it.copy(difficulty = if (it.difficulty == d) null else d) } }, label = { Text(d) })
                }
                items(listOf("unsolved" to "Unsolved", "attempted" to "Attempted", "solved" to "Solved")) { (k, l) ->
                    FilterChip(s.status == k, onClick = { viewModel.update { it.copy(status = if (it.status == k) null else k) } }, label = { Text(l) })
                }
            }
            LazyRow(contentPadding = PaddingValues(16.dp, 4.dp), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                items(ChallengeCategories) { c ->
                    FilterChip(s.category == c, onClick = { viewModel.update { it.copy(category = if (it.category == c) null else c) } }, label = { Text(c.humanize()) })
                }
            }
            when {
                s.items.isEmpty() && s.loading -> Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) { repeat(6) { SkeletonCard(height = 64.dp) } }
                s.items.isEmpty() && s.error != null -> ErrorState(s.error!!, onRetry = viewModel::retry)
                s.items.isEmpty() -> EmptyState("No challenges match", "Try another category or difficulty.", icon = Icons.Filled.SearchOff)
                else -> LazyColumn(state = listState, contentPadding = PaddingValues(16.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
                    items(s.items, key = { it.id }) { c ->
                        DiCard(onClick = { nav.navigate(Routes.ChallengeDetail(c.id)) }) {
                            Row(verticalAlignment = Alignment.CenterVertically) {
                                Icon(if (c.isSolved) Icons.Filled.CheckCircle else if (c.isAttempted) Icons.Filled.Pending else Icons.Filled.Code, null,
                                    tint = if (c.isSolved) DiTheme.colors.success else MaterialTheme.colorScheme.primary)
                                Spacer(Modifier.width(12.dp))
                                Column(Modifier.weight(1f)) {
                                    Text(c.title, style = MaterialTheme.typography.titleSmall)
                                    Text("${c.category.humanize()} · ${c.acceptanceRate.roundToInt()}% acceptance", style = MaterialTheme.typography.bodySmall)
                                }
                                if (c.isPremium) { PremiumBadge(); Spacer(Modifier.width(6.dp)) }
                                DifficultyChip(c.difficulty)
                            }
                        }
                    }
                    if (s.loading) item { LinearProgressIndicator(Modifier.fillMaxWidth()) }
                    s.error?.let { e -> item { TextButton(onClick = viewModel::loadMore) { Text("${e.message} Retry") } } }
                }
            }
        }
    }
}

@OptIn(ExperimentalLayoutApi::class)
@Composable
fun ChallengeDetailScreen(nav: NavController, viewModel: ChallengeDetailViewModel = hiltViewModel()) {
    val s by viewModel.state.collectAsStateWithLifecycle()
    val snackbar = remember { SnackbarHostState() }
    val scope = rememberCoroutineScope()
    var collections by remember { mutableStateOf<List<SavedCollection>?>(null) }
    LaunchedEffect(s.message) { s.message?.let { snackbar.showSnackbar(it); viewModel.clearMessage() } }
    Scaffold(
        topBar = {
            DiTopBar(s.challenge.dataOrNull?.title ?: "Challenge", onBack = { nav.popBackStack() }) {
                IconButton(onClick = { scope.launch { collections = viewModel.collections() } }) { Icon(Icons.Filled.PlaylistAdd, "Save to collection") }
                IconButton(onClick = { nav.navigate(Routes.Submissions(viewModel.challengeId)) }) { Icon(Icons.Filled.History, "Submissions") }
            }
        },
        snackbarHost = { SnackbarHost(snackbar) },
        bottomBar = {
            if (s.challenge is Loadable.Ready) Surface(tonalElevation = 3.dp) {
                PrimaryButton("Solve challenge", onClick = { nav.navigate(Routes.CodeEditor(viewModel.challengeId)) }, icon = Icons.Filled.Code,
                    modifier = Modifier.fillMaxWidth().padding(16.dp).navigationBarsPadding().testTag("open_editor"))
            }
        },
    ) { padding ->
        LoadableContent(s.challenge, onRetry = { viewModel.load() }, modifier = Modifier.padding(padding), onUpgrade = { nav.navigate(Routes.Subscription) }) { c ->
            Column(Modifier.padding(padding).verticalScroll(rememberScrollState()).padding(16.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
                FlowRow(horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                    DifficultyChip(c.difficulty); TagChip(c.category.humanize()); if (c.isSolved) TagChip("Solved ✓")
                    TagChip("${c.timeLimitMs} ms"); TagChip("${c.memoryLimitMb} MB")
                }
                MarkdownText(c.problemStatement)
                Section("Input") { MarkdownText(c.inputDescription) }
                Section("Output") { MarkdownText(c.outputDescription) }
                Section("Constraints") { MarkdownText(c.constraints) }
                if (c.expectedTimeComplexity != null || c.expectedSpaceComplexity != null) {
                    Section("Expected complexity") {
                        c.expectedTimeComplexity?.let { KeyValueRow("Time", it) }
                        c.expectedSpaceComplexity?.let { KeyValueRow("Space", it) }
                    }
                }
                c.sampleTestCases.forEachIndexed { i, t ->
                    Section("Example ${i + 1}") {
                        Text("Input", style = MaterialTheme.typography.labelMedium); CodeBlock(t.input, null)
                        Text("Output", style = MaterialTheme.typography.labelMedium); CodeBlock(t.expectedOutput, null)
                        t.explanation?.let { Text(it, style = MaterialTheme.typography.bodySmall) }
                    }
                }
                if (c.hiddenTestCaseCount > 0) Text("+ ${c.hiddenTestCaseCount} hidden test cases are used when you submit.", style = MaterialTheme.typography.bodySmall)
                if (c.hintCount > 0) {
                    Section("Hints") {
                        s.hints.forEach { h -> Text("${h.index + 1}. ${h.text}", style = MaterialTheme.typography.bodyMedium, modifier = Modifier.padding(vertical = 2.dp)) }
                        if (s.hints.size < c.hintCount) TextButton(onClick = viewModel::nextHint, enabled = !s.loadingHint) {
                            Icon(Icons.Filled.Lightbulb, null); Spacer(Modifier.width(6.dp)); Text("Reveal hint ${s.hints.size + 1} of ${c.hintCount}")
                        }
                    }
                }
                if (c.solutionAvailable) {
                    Section("Solution approach") {
                        val sol = s.solution
                        if (sol == null) TextButton(onClick = viewModel::showSolution, enabled = !s.loadingSolution) { Text("Show solution explanation") }
                        else {
                            MarkdownText(sol.explanation)
                            sol.expectedTimeComplexity?.let { KeyValueRow("Time", it) }
                            sol.expectedSpaceComplexity?.let { KeyValueRow("Space", it) }
                        }
                    }
                }
                Spacer(Modifier.height(64.dp))
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
private fun Section(title: String, content: @Composable ColumnScope.() -> Unit) {
    DiCard { Text(title, style = MaterialTheme.typography.titleSmall, color = MaterialTheme.colorScheme.primary); Spacer(Modifier.height(4.dp)); content() }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun CodeEditorScreen(nav: NavController, viewModel: CodeEditorViewModel = hiltViewModel()) {
    val s by viewModel.state.collectAsStateWithLifecycle()
    val snackbar = remember { SnackbarHostState() }
    var tab by remember { mutableIntStateOf(0) }
    var confirmReset by remember { mutableStateOf(false) }
    LaunchedEffect(Unit) { viewModel.events.collect { snackbar.showSnackbar(it) } }
    LaunchedEffect(s.runResult, s.submission, s.error) { if (s.runResult != null || s.submission != null || s.error != null) tab = 1 }
    Scaffold(
        topBar = {
            DiTopBar(s.challenge.dataOrNull?.title ?: "Editor", onBack = { nav.popBackStack() }) {
                IconButton(onClick = { confirmReset = true }) { Icon(Icons.Filled.RestartAlt, "Reset to starter code") }
            }
        },
        snackbarHost = { SnackbarHost(snackbar) },
        bottomBar = {
            Surface(tonalElevation = 3.dp) {
                Row(Modifier.fillMaxWidth().padding(12.dp).navigationBarsPadding().imePadding(), horizontalArrangement = Arrangement.spacedBy(12.dp)) {
                    SecondaryButton(if (s.running) "Running…" else "Run", onClick = viewModel::run, enabled = !s.running && !s.submitting,
                        modifier = Modifier.weight(1f).testTag("run_code"), icon = Icons.Filled.PlayArrow)
                    PrimaryButton("Submit", onClick = viewModel::submit, loading = s.submitting, enabled = !s.running,
                        modifier = Modifier.weight(1f).testTag("submit_code"), icon = Icons.Filled.CloudUpload)
                }
            }
        },
    ) { padding ->
        LoadableContent(s.challenge, onRetry = { viewModel.load() }, modifier = Modifier.padding(padding), onUpgrade = { nav.navigate(Routes.Subscription) }) { _ ->
            Column(Modifier.padding(padding).fillMaxSize()) {
                Row(Modifier.padding(horizontal = 12.dp), verticalAlignment = Alignment.CenterVertically) {
                    DropdownField("Language", s.languages.map { it.code }, s.language, { viewModel.selectLanguage(it) }, Modifier.weight(1f),
                        display = { code -> s.languages.firstOrNull { it.code == code }?.let { "${it.displayName} ${it.version}".trim() } ?: code })
                }
                PrimaryTabRow(selectedTabIndex = tab) {
                    Tab(tab == 0, onClick = { tab = 0 }, text = { Text("Code") })
                    Tab(tab == 1, onClick = { tab = 1 }, text = { Text("Console") })
                }
                if (s.running || s.submitting) LinearProgressIndicator(Modifier.fillMaxWidth())
                if (tab == 0) {
                    CodeEditorField(s.code, viewModel::onCode, s.language, Modifier.weight(1f).fillMaxWidth().padding(8.dp).testTag("code_editor"))
                    Row(Modifier.padding(horizontal = 12.dp), verticalAlignment = Alignment.CenterVertically) {
                        Checkbox(s.useCustomInput, viewModel::toggleCustomInput); Text("Run with custom input", style = MaterialTheme.typography.bodyMedium)
                    }
                    if (s.useCustomInput) OutlinedTextField(s.customInput, viewModel::onCustomInput, Modifier.fillMaxWidth().padding(horizontal = 12.dp).heightIn(max = 140.dp),
                        label = { Text("stdin") }, textStyle = CodeStyle)
                } else Console(s, Modifier.weight(1f))
            }
        }
    }
    if (confirmReset) ConfirmDialog("Reset code?", "Your current code for this language will be replaced with the starter template.", "Reset",
        { viewModel.resetCode(); confirmReset = false }, { confirmReset = false }, destructive = true)
}

@Composable
private fun Console(s: EditorUiState, modifier: Modifier) {
    Column(modifier.fillMaxWidth().verticalScroll(rememberScrollState()).padding(12.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
        s.error?.let { e ->
            DiCard { Text(e.message, color = MaterialTheme.colorScheme.error) }
        }
        val status: String?; val compile: String?; val results: List<TestResult>; val passed: Int; val total: Int
        when {
            s.submission != null -> s.submission.let { status = it.status; compile = it.compileOutput; results = it.results; passed = it.passedCount; total = it.totalCount }
            s.runResult != null -> s.runResult.let { status = it.status; compile = it.compileOutput; results = it.results; passed = it.passedCount; total = it.totalCount }
            else -> { if (s.error == null) EmptyState("No output yet", "Run your code against the sample tests, or submit it for full grading.", icon = Icons.Filled.Terminal); return@Column }
        }
        StatusHeader(status!!, passed, total, isSubmission = s.submission != null)
        compile?.takeIf { it.isNotBlank() }?.let { Text("Compiler output", style = MaterialTheme.typography.labelLarge); ConsoleText(it) }
        results.forEach { r -> TestResultCard(r) }
    }
}

@Composable
internal fun StatusHeader(status: String, passed: Int, total: Int, isSubmission: Boolean) {
    val ok = status == "Accepted"
    DiCard {
        Row(verticalAlignment = Alignment.CenterVertically) {
            Icon(if (ok) Icons.Filled.CheckCircle else Icons.Filled.ErrorOutline, null, tint = if (ok) DiTheme.colors.success else DiTheme.colors.danger, modifier = Modifier.size(32.dp))
            Spacer(Modifier.width(12.dp))
            Column {
                Text(submissionStatusLabel(status), style = MaterialTheme.typography.titleMedium, modifier = Modifier.testTag("execution_status"))
                Text("$passed / $total ${if (isSubmission) "tests" else "sample tests"} passed", style = MaterialTheme.typography.bodySmall)
            }
        }
    }
}

@Composable
internal fun TestResultCard(r: TestResult) {
    var expanded by remember { mutableStateOf(!r.passed) }
    DiCard(onClick = { expanded = !expanded }) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            Icon(if (r.passed) Icons.Filled.Check else Icons.Filled.Close, null, tint = if (r.passed) DiTheme.colors.success else DiTheme.colors.danger)
            Spacer(Modifier.width(8.dp))
            Text(when { r.isCustom -> "Custom input"; r.isSample -> "Sample ${r.ordinal + 1}"; else -> "Hidden test ${r.ordinal + 1}" }, Modifier.weight(1f), style = MaterialTheme.typography.titleSmall)
            Text("${r.runtimeMs} ms", style = MaterialTheme.typography.labelSmall)
        }
        if (expanded) {
            r.input?.let { Text("Input", style = MaterialTheme.typography.labelMedium); ConsoleText(it) }
            r.expectedOutput?.let { Text("Expected", style = MaterialTheme.typography.labelMedium); ConsoleText(it) }
            r.actualOutput?.let { Text("Your output", style = MaterialTheme.typography.labelMedium); ConsoleText(it) }
            r.errorOutput?.takeIf { it.isNotBlank() }?.let { Text("stderr", style = MaterialTheme.typography.labelMedium, color = MaterialTheme.colorScheme.error); ConsoleText(it) }
            if (!r.isSample && !r.isCustom && r.input == null) Text("Hidden test details are not shown.", style = MaterialTheme.typography.bodySmall)
        }
    }
}

@Composable
private fun ConsoleText(text: String) {
    Surface(color = MaterialTheme.colorScheme.surfaceVariant, shape = MaterialTheme.shapes.small, modifier = Modifier.fillMaxWidth()) {
        SelectionContainer { Text(text.take(4000), style = CodeStyle, modifier = Modifier.horizontalScroll(rememberScrollState()).padding(8.dp)) }
    }
}

@Composable
fun SubmissionsScreen(nav: NavController, viewModel: SubmissionsViewModel = hiltViewModel()) {
    val s by viewModel.state.collectAsStateWithLifecycle()
    val listState = rememberLazyListState()
    val nearEnd by remember { derivedStateOf { listState.layoutInfo.visibleItemsInfo.lastOrNull()?.index?.let { it >= listState.layoutInfo.totalItemsCount - 3 } == true } }
    LaunchedEffect(nearEnd) { if (nearEnd) viewModel.loadMore() }
    Scaffold(topBar = { DiTopBar("Submissions", onBack = { nav.popBackStack() }) }) { padding ->
        when {
            s.items.isEmpty() && s.loading -> LoadingState(Modifier.padding(padding))
            s.items.isEmpty() && s.error != null -> ErrorState(s.error!!, onRetry = viewModel::refresh, modifier = Modifier.padding(padding))
            s.items.isEmpty() -> EmptyState("No submissions yet", "Your runs and submissions will be listed here.", Modifier.padding(padding), Icons.Filled.History)
            else -> LazyColumn(Modifier.padding(padding), state = listState) {
                items(s.items, key = { it.id }) { sub ->
                    ListItem(
                        headlineContent = { Text(sub.challengeTitle, maxLines = 1, overflow = TextOverflow.Ellipsis) },
                        supportingContent = { Text("${submissionStatusLabel(sub.status)} · ${sub.passedCount}/${sub.totalCount} · ${sub.languageCode} · ${TimeFormat.relative(sub.createdAt)}") },
                        overlineContent = { Text(sub.kind) },
                        leadingContent = { Icon(if (sub.status == "Accepted") Icons.Filled.CheckCircle else Icons.Filled.ErrorOutline, null, tint = if (sub.status == "Accepted") DiTheme.colors.success else DiTheme.colors.danger) },
                        modifier = Modifier.clickable { nav.navigate(Routes.SubmissionDetail(sub.id)) },
                    )
                    HorizontalDivider()
                }
                if (s.loading) item { LinearProgressIndicator(Modifier.fillMaxWidth().padding(16.dp)) }
            }
        }
    }
}

@Composable
fun SubmissionDetailScreen(nav: NavController, viewModel: SubmissionDetailViewModel = hiltViewModel()) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    Scaffold(topBar = { DiTopBar("Submission", onBack = { nav.popBackStack() }) }) { padding ->
        LoadableContent(state, onRetry = { viewModel.load() }, modifier = Modifier.padding(padding)) { sub ->
            LazyColumn(Modifier.padding(padding), contentPadding = PaddingValues(16.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
                item {
                    Text(sub.challengeTitle, style = MaterialTheme.typography.titleLarge)
                    Text("${sub.kind} · ${sub.languageCode} · ${TimeFormat.relative(sub.createdAt)}${sub.maxRuntimeMs?.let { " · max $it ms" } ?: ""}", style = MaterialTheme.typography.bodySmall)
                }
                item { StatusHeader(sub.status, sub.passedCount, sub.totalCount, isSubmission = sub.kind == "Submit") }
                sub.sourceCode?.let { code -> item { Text("Code", style = MaterialTheme.typography.titleSmall); CodeBlock(code, sub.languageCode) } }
                sub.compileOutput?.takeIf { it.isNotBlank() }?.let { item { Text("Compiler output", style = MaterialTheme.typography.titleSmall); CodeBlock(it, null) } }
                items(sub.results, key = { "${it.ordinal}-${it.isSample}-${it.isCustom}" }) { TestResultCard(it) }
                item {
                    SecondaryButton("Open challenge", onClick = { nav.navigate(Routes.ChallengeDetail(sub.challengeId)) }, modifier = Modifier.fillMaxWidth())
                }
            }
        }
    }
}
