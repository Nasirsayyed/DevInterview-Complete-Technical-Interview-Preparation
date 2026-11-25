package com.devinterview.feature.questions

import android.content.Context
import android.content.Intent
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.LazyRow
import androidx.compose.foundation.lazy.grid.GridCells
import androidx.compose.foundation.lazy.grid.LazyVerticalGrid
import androidx.compose.foundation.lazy.grid.items
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.selection.selectable
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.automirrored.filled.ArrowForward
import androidx.compose.material.icons.automirrored.filled.MenuBook
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
import androidx.paging.LoadState
import androidx.paging.compose.collectAsLazyPagingItems
import androidx.paging.compose.itemKey
import com.devinterview.core.common.TimeFormat
import com.devinterview.core.designsystem.*
import com.devinterview.core.navigation.Routes
import com.devinterview.domain.model.*

fun NavGraphBuilder.questionsGraph(nav: NavController) {
    val back: () -> Unit = { nav.popBackStack() }
    val upgrade: () -> Unit = { nav.navigate(Routes.Subscription) }
    composable<Routes.TechnologyExplorer> { TechnologyExplorerScreen(back, onTechnology = { nav.navigate(Routes.Categories(it.id, it.name)) }) }
    composable<Routes.Categories> { CategoriesScreen(nav) }
    composable<Routes.QuestionList> { QuestionListScreen(back, onQuestion = { nav.navigate(Routes.QuestionDetail(it)) }) }
    composable<Routes.QuestionDetail> { QuestionDetailScreen(back, upgrade, onEvaluate = { id, text -> nav.navigate(Routes.AnswerEvaluator(id, text)) }) }
    composable<Routes.RandomQuestion> { RandomQuestionScreen(nav) }
    composable<Routes.Bookmarks> { BookmarksScreen(back, onQuestion = { nav.navigate(Routes.QuestionDetail(it)) }) }
    composable<Routes.Notes> { NotesScreen(back, onQuestion = { nav.navigate(Routes.QuestionDetail(it)) }) }
    composable<Routes.Collections> { CollectionsScreen(back, onOpen = { nav.navigate(Routes.CollectionDetail(it.id, it.name)) }) }
    composable<Routes.CollectionDetail> { CollectionDetailScreen(nav) }
}

internal fun Context.shareText(text: String) {
    startActivity(Intent.createChooser(Intent(Intent.ACTION_SEND).setType("text/plain").putExtra(Intent.EXTRA_TEXT, text), "Share"))
}

@Composable
fun TechnologyExplorerScreen(onBack: () -> Unit, onTechnology: (Technology) -> Unit, viewModel: TechnologyExplorerViewModel = hiltViewModel()) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    val query by viewModel.query.collectAsStateWithLifecycle()
    Scaffold(topBar = { DiTopBar("Technologies", onBack = onBack) }) { padding ->
        LoadableContent(state, onRetry = { viewModel.load() }, modifier = Modifier.padding(padding)) { techs ->
            Column(Modifier.padding(padding)) {
                OutlinedTextField(query, { viewModel.query.value = it }, Modifier.fillMaxWidth().padding(16.dp, 8.dp), placeholder = { Text("Filter technologies") },
                    leadingIcon = { Icon(Icons.Filled.Search, null) }, singleLine = true)
                val filtered = techs.filter { query.isBlank() || it.name.contains(query, true) || it.group.contains(query, true) }
                LazyVerticalGrid(GridCells.Adaptive(160.dp), contentPadding = PaddingValues(16.dp), horizontalArrangement = Arrangement.spacedBy(12.dp),
                    verticalArrangement = Arrangement.spacedBy(12.dp)) {
                    items(filtered, key = { it.id }) { t ->
                        DiCard(onClick = { onTechnology(t) }, modifier = Modifier.testTag("tech_${t.slug}")) {
                            Text(t.group, style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.primary)
                            Text(t.name, style = MaterialTheme.typography.titleMedium, maxLines = 1, overflow = TextOverflow.Ellipsis)
                            Spacer(Modifier.height(4.dp))
                            Text("${t.questionCount} questions · ${t.categoryCount} categories", style = MaterialTheme.typography.bodySmall,
                                color = MaterialTheme.colorScheme.onSurfaceVariant)
                        }
                    }
                }
            }
        }
    }
}

@Composable
fun CategoriesScreen(nav: NavController, viewModel: CategoriesViewModel = hiltViewModel()) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    val r = viewModel.route
    Scaffold(topBar = {
        DiTopBar(r.technologyName, onBack = { nav.popBackStack() }) {
            IconButton(onClick = { nav.navigate(Routes.RandomQuestion(r.technologyId)) }) { Icon(Icons.Filled.Shuffle, "Random question") }
        }
    }) { padding ->
        LoadableContent(state, onRetry = { viewModel.load() }, modifier = Modifier.padding(padding)) { categories ->
            LazyColumn(Modifier.padding(padding), contentPadding = PaddingValues(16.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
                item {
                    GradientCard(onClick = { nav.navigate(Routes.QuestionList(technologyId = r.technologyId, title = "All ${r.technologyName}")) }) {
                        Text("All ${r.technologyName} questions", style = MaterialTheme.typography.titleMedium, color = Color.White)
                        Text("${categories.sumOf { it.questionCount }} questions across ${categories.size} categories", color = Color.White.copy(alpha = .85f))
                    }
                }
                item {
                    Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                        AssistChip(onClick = { nav.navigate(Routes.QuizConfig(r.technologyId)) }, label = { Text("Take a quiz") }, leadingIcon = { Icon(Icons.Filled.Quiz, null) })
                        AssistChip(onClick = { nav.navigate(Routes.FlashcardStudy(r.technologyId, r.technologyName)) }, label = { Text("Flashcards") }, leadingIcon = { Icon(Icons.Filled.Style, null) })
                    }
                }
                if (categories.isEmpty()) item { EmptyState("No categories yet", "Content for this technology is on its way.") }
                items(categories, key = { it.id }) { c ->
                    DiCard(onClick = { nav.navigate(Routes.QuestionList(technologyId = r.technologyId, categoryId = c.id, title = c.name)) }) {
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            Column(Modifier.weight(1f)) {
                                Text(c.name, style = MaterialTheme.typography.titleMedium)
                                c.description?.let { Text(it, style = MaterialTheme.typography.bodySmall, maxLines = 2, overflow = TextOverflow.Ellipsis) }
                                if (c.topics.isNotEmpty()) Text(c.topics.joinToString(" · ") { it.name }, style = MaterialTheme.typography.labelSmall,
                                    color = MaterialTheme.colorScheme.onSurfaceVariant, maxLines = 1, overflow = TextOverflow.Ellipsis)
                            }
                            Text("${c.questionCount}", style = MaterialTheme.typography.titleMedium, color = MaterialTheme.colorScheme.primary)
                        }
                    }
                }
            }
        }
    }
}

private val difficulties = listOf("Beginner", "Intermediate", "Advanced", "Expert")
private val attemptFilters = listOf("unattempted" to "Not attempted", "incorrect" to "Got wrong", "correct" to "Got right")
private val sorts = listOf(null to "Easiest first", "difficulty_desc" to "Hardest first", "newest" to "Newest", "title" to "A–Z")

@Composable
fun QuestionListScreen(onBack: () -> Unit, onQuestion: (String) -> Unit, viewModel: QuestionListViewModel = hiltViewModel()) {
    val filter by viewModel.filter.collectAsStateWithLifecycle()
    val items = viewModel.questions.collectAsLazyPagingItems()
    var sortMenu by remember { mutableStateOf(false) }
    Scaffold(topBar = {
        DiTopBar(viewModel.route.title, onBack = onBack) {
            Box {
                IconButton(onClick = { sortMenu = true }) { Icon(Icons.Filled.Sort, "Sort") }
                DropdownMenu(sortMenu, onDismissRequest = { sortMenu = false }) {
                    sorts.forEach { (key, label) ->
                        DropdownMenuItem(text = { Text(label) }, onClick = { viewModel.update { it.copy(sort = key) }; sortMenu = false },
                            leadingIcon = { if (filter.sort == key) Icon(Icons.Filled.Check, null) })
                    }
                }
            }
        }
    }) { padding ->
        Column(Modifier.padding(padding)) {
            OutlinedTextField(filter.search.orEmpty(), { v -> viewModel.update { it.copy(search = v.ifBlank { null }) } },
                Modifier.fillMaxWidth().padding(16.dp, 4.dp).testTag("question_search"), placeholder = { Text("Search questions") },
                leadingIcon = { Icon(Icons.Filled.Search, null) }, singleLine = true)
            LazyRow(contentPadding = PaddingValues(horizontal = 16.dp, vertical = 4.dp), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                items(difficulties) { d ->
                    FilterChip(filter.difficulty == d, onClick = { viewModel.update { it.copy(difficulty = if (it.difficulty == d) null else d) } }, label = { Text(d) })
                }
                items(attemptFilters) { (key, label) ->
                    FilterChip(filter.attemptFilter == key, onClick = { viewModel.update { it.copy(attemptFilter = if (it.attemptFilter == key) null else key) } }, label = { Text(label) })
                }
                item { FilterChip(filter.bookmarkedOnly, onClick = { viewModel.update { it.copy(bookmarkedOnly = !it.bookmarkedOnly) } }, label = { Text("Bookmarked") }) }
            }
            when (val refresh = items.loadState.refresh) {
                is LoadState.Loading -> Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) { repeat(6) { SkeletonCard(height = 72.dp) } }
                is LoadState.Error -> ErrorState((refresh.error as? AppErrorException)?.error ?: AppError.Unknown(), onRetry = { items.retry() })
                else -> if (items.itemCount == 0) EmptyState("No questions found", "Try removing some filters.", icon = Icons.Filled.SearchOff)
                else LazyColumn(Modifier.testTag("question_list"), contentPadding = PaddingValues(16.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
                    items(items.itemCount, key = items.itemKey { it.id }) { i ->
                        items[i]?.let { q -> QuestionRow(q) { onQuestion(q.id) } }
                    }
                    when (val append = items.loadState.append) {
                        is LoadState.Loading -> item { LinearProgressIndicator(Modifier.fillMaxWidth()) }
                        is LoadState.Error -> item { TextButton(onClick = { items.retry() }) { Text("Couldn't load more: ${append.error.message}. Retry") } }
                        else -> Unit
                    }
                }
            }
        }
    }
}

@OptIn(ExperimentalLayoutApi::class)
@Composable
fun QuestionRow(q: QuestionSummary, onClick: () -> Unit) {
    DiCard(onClick = onClick) {
        Row(verticalAlignment = Alignment.Top) {
            Column(Modifier.weight(1f)) {
                Text(q.title, style = MaterialTheme.typography.titleSmall, maxLines = 3, overflow = TextOverflow.Ellipsis)
                Spacer(Modifier.height(6.dp))
                FlowRow(horizontalArrangement = Arrangement.spacedBy(6.dp), verticalArrangement = Arrangement.spacedBy(4.dp)) {
                    DifficultyChip(q.difficulty)
                    TagChip(q.category.name)
                    if (q.isPremium) PremiumBadge()
                }
            }
            when (q.lastAttemptCorrect) {
                true -> Icon(Icons.Filled.CheckCircle, "Answered correctly", tint = DiTheme.colors.success)
                false -> Icon(Icons.Filled.Cancel, "Answered incorrectly", tint = DiTheme.colors.danger)
                null -> if (q.isBookmarked) Icon(Icons.Filled.Bookmark, "Bookmarked", tint = MaterialTheme.colorScheme.primary)
            }
        }
    }
}

@OptIn(ExperimentalLayoutApi::class)
@Composable
fun QuestionDetailScreen(onBack: () -> Unit, onUpgrade: () -> Unit, onEvaluate: (String, String) -> Unit, viewModel: QuestionDetailViewModel = hiltViewModel()) {
    val s by viewModel.state.collectAsStateWithLifecycle()
    val bookmarked by viewModel.isBookmarked.collectAsStateWithLifecycle()
    val note by viewModel.note.collectAsStateWithLifecycle()
    val context = LocalContext.current
    val snackbar = remember { SnackbarHostState() }
    var noteDialog by remember { mutableStateOf(false) }
    var reportDialog by remember { mutableStateOf(false) }
    var collectionDialog by remember { mutableStateOf(false) }
    var menu by remember { mutableStateOf(false) }
    LaunchedEffect(Unit) {
        viewModel.events.collect { e ->
            when (e) {
                is QuestionEvent.Message -> snackbar.showSnackbar(e.text)
                is QuestionEvent.Share -> context.shareText(e.text)
                is QuestionEvent.Navigate -> Unit
            }
        }
    }
    val q = s.question.dataOrNull
    Scaffold(
        topBar = {
            DiTopBar(q?.technology?.name ?: "Question", onBack = onBack) {
                if (q != null) {
                    IconButton(onClick = viewModel::toggleBookmark, modifier = Modifier.testTag("bookmark_button")) {
                        Icon(if (bookmarked) Icons.Filled.Bookmark else Icons.Filled.BookmarkBorder, if (bookmarked) "Remove bookmark" else "Bookmark")
                    }
                    IconButton(onClick = { noteDialog = true }) { Icon(if (note.isNullOrBlank()) Icons.Filled.NoteAdd else Icons.Filled.EditNote, "Note") }
                    Box {
                        IconButton(onClick = { menu = true }) { Icon(Icons.Filled.MoreVert, "More") }
                        DropdownMenu(menu, { menu = false }) {
                            DropdownMenuItem(text = { Text("Share") }, leadingIcon = { Icon(Icons.Filled.Share, null) }, onClick = { menu = false; viewModel.share() })
                            DropdownMenuItem(text = { Text("Add to collection") }, leadingIcon = { Icon(Icons.Filled.PlaylistAdd, null) },
                                onClick = { menu = false; viewModel.loadCollections(); collectionDialog = true })
                            DropdownMenuItem(text = { Text("Report a problem") }, leadingIcon = { Icon(Icons.Filled.Flag, null) },
                                onClick = { menu = false; viewModel.loadReportReasons(); reportDialog = true })
                        }
                    }
                }
            }
        },
        snackbarHost = { SnackbarHost(snackbar) },
        bottomBar = {
            if (q != null) BottomAppBar {
                TextButton(onClick = { q.previousId?.let(viewModel::goTo) }, enabled = q.previousId != null) {
                    Icon(Icons.AutoMirrored.Filled.ArrowBack, null); Spacer(Modifier.width(4.dp)); Text("Previous")
                }
                Spacer(Modifier.weight(1f))
                TextButton(onClick = { q.nextId?.let(viewModel::goTo) }, enabled = q.nextId != null, modifier = Modifier.testTag("next_question")) {
                    Text("Next"); Spacer(Modifier.width(4.dp)); Icon(Icons.AutoMirrored.Filled.ArrowForward, null)
                }
            }
        },
    ) { padding ->
        LoadableContent(s.question, onRetry = { viewModel.load() }, modifier = Modifier.padding(padding), onUpgrade = onUpgrade) { question ->
            Column(Modifier.padding(padding).verticalScroll(rememberScrollState()).padding(16.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
                FlowRow(horizontalArrangement = Arrangement.spacedBy(6.dp), verticalArrangement = Arrangement.spacedBy(4.dp)) {
                    DifficultyChip(question.difficulty); TagChip(question.category.name); question.topic?.let { TagChip(it.name) }
                    TagChip("~${(question.estimatedSeconds + 59) / 60} min")
                }
                Text(question.title, style = MaterialTheme.typography.titleLarge, modifier = Modifier.testTag("question_title"))
                if (question.body.isNotBlank() && question.body != question.title) MarkdownText(question.body)
                question.codeSnippet?.let { CodeBlock(it, question.codeLanguage) }

                if (question.isObjective && question.options.isNotEmpty()) {
                    if (question.allowsMultipleAnswers) Text("Select all that apply", style = MaterialTheme.typography.labelMedium, color = MaterialTheme.colorScheme.onSurfaceVariant)
                    question.options.sortedBy { it.displayOrder }.forEach { o ->
                        val correctIds = s.result?.correctOptionIds ?: s.answer?.correctOptionIds.orEmpty()
                        OptionRow(o, selected = o.id in s.selected, revealed = s.result != null, correct = o.id in correctIds,
                            explanation = s.answer?.options?.firstOrNull { it.optionId == o.id }?.explanation, multiple = question.allowsMultipleAnswers) {
                            viewModel.toggleOption(o.id)
                        }
                    }
                    if (s.result == null) {
                        PrimaryButton("Check answer", onClick = viewModel::submitAnswer, enabled = s.selected.isNotEmpty(), loading = s.submitting,
                            modifier = Modifier.fillMaxWidth().testTag("check_answer"))
                    } else {
                        val correct = s.result?.isCorrect == true
                        DiCard {
                            Row(verticalAlignment = Alignment.CenterVertically) {
                                Icon(if (correct) Icons.Filled.CheckCircle else Icons.Filled.Cancel, null, tint = if (correct) DiTheme.colors.success else DiTheme.colors.danger)
                                Spacer(Modifier.width(8.dp))
                                Text(if (correct) "Correct!" else "Not quite.", style = MaterialTheme.typography.titleMedium, modifier = Modifier.testTag("attempt_result"))
                            }
                        }
                    }
                } else if (s.answer == null) {
                    PrimaryButton("Show answer", onClick = viewModel::revealAnswer, loading = s.revealing, modifier = Modifier.fillMaxWidth(), icon = Icons.Filled.Visibility)
                    SecondaryButton("Write your answer and get AI feedback", icon = Icons.Filled.Grading, modifier = Modifier.fillMaxWidth(),
                        onClick = { onEvaluate(question.id, question.title) })
                }

                s.answer?.let { a ->
                    HorizontalDivider()
                    Text("Answer", style = MaterialTheme.typography.titleMedium)
                    Text(a.shortAnswer, style = MaterialTheme.typography.bodyLarge)
                    Text("Explanation", style = MaterialTheme.typography.titleMedium)
                    MarkdownText(a.detailedExplanation)
                    if (!question.isObjective && s.result == null) {
                        Text("How did you do?", style = MaterialTheme.typography.titleSmall)
                        Row(horizontalArrangement = Arrangement.spacedBy(12.dp)) {
                            SecondaryButton("I missed parts", onClick = { viewModel.selfAssess(false) }, modifier = Modifier.weight(1f))
                            PrimaryButton("I knew it", onClick = { viewModel.selfAssess(true) }, modifier = Modifier.weight(1f))
                        }
                    }
                }
                if (!note.isNullOrBlank()) {
                    DiCard(onClick = { noteDialog = true }) {
                        Text("My note", style = MaterialTheme.typography.labelLarge, color = MaterialTheme.colorScheme.primary)
                        Text(note!!, style = MaterialTheme.typography.bodyMedium)
                    }
                }
                if (question.tags.isNotEmpty()) FlowRow(horizontalArrangement = Arrangement.spacedBy(6.dp)) { question.tags.forEach { TagChip("#$it") } }
            }
        }
    }
    if (noteDialog) NoteDialog(note.orEmpty(), onDismiss = { noteDialog = false }) { viewModel.saveNote(it); noteDialog = false }
    if (reportDialog) ReportDialog(s.reportReasons, onDismiss = { reportDialog = false }) { r, d -> viewModel.report(r, d); reportDialog = false }
    if (collectionDialog) CollectionPickerDialog(s.collections, onDismiss = { collectionDialog = false }) { id, name -> viewModel.addToCollection(id, name); collectionDialog = false }
}

@Composable
private fun OptionRow(option: QuestionOption, selected: Boolean, revealed: Boolean, correct: Boolean, explanation: String?, multiple: Boolean, onClick: () -> Unit) {
    val border = when {
        revealed && correct -> DiTheme.colors.success
        revealed && selected -> DiTheme.colors.danger
        selected -> MaterialTheme.colorScheme.primary
        else -> MaterialTheme.colorScheme.outlineVariant
    }
    OutlinedCard(
        Modifier.fillMaxWidth().selectable(selected, enabled = !revealed, onClick = onClick).testTag("option_${option.displayOrder}"),
        border = BorderStroke(if (selected || (revealed && correct)) 2.dp else 1.dp, border),
    ) {
        Row(Modifier.padding(12.dp), verticalAlignment = Alignment.CenterVertically) {
            if (multiple) Checkbox(selected, null) else RadioButton(selected, null)
            Spacer(Modifier.width(8.dp))
            Column(Modifier.weight(1f)) {
                Text(option.text, style = MaterialTheme.typography.bodyLarge)
                if (revealed && !explanation.isNullOrBlank()) Text(explanation, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
            }
            if (revealed && correct) Icon(Icons.Filled.Check, "Correct option", tint = DiTheme.colors.success)
        }
    }
}

@Composable
private fun NoteDialog(initial: String, onDismiss: () -> Unit, onSave: (String) -> Unit) {
    var text by remember { mutableStateOf(initial) }
    AlertDialog(onDismissRequest = onDismiss, title = { Text("My note") },
        text = { OutlinedTextField(text, { if (it.length <= 4000) text = it }, Modifier.fillMaxWidth().heightIn(min = 140.dp), placeholder = { Text("Key points, mnemonics, links…") }) },
        confirmButton = { TextButton(onClick = { onSave(text) }) { Text(if (text.isBlank() && initial.isNotBlank()) "Delete note" else "Save") } },
        dismissButton = { TextButton(onClick = onDismiss) { Text("Cancel") } })
}

@Composable
private fun ReportDialog(reasons: List<String>, onDismiss: () -> Unit, onSubmit: (String, String?) -> Unit) {
    var reason by remember { mutableStateOf<String?>(null) }
    var details by remember { mutableStateOf("") }
    AlertDialog(onDismissRequest = onDismiss, title = { Text("Report a problem") }, text = {
        Column(Modifier.verticalScroll(rememberScrollState())) {
            if (reasons.isEmpty()) LinearProgressIndicator(Modifier.fillMaxWidth())
            reasons.forEach { r ->
                Row(Modifier.fillMaxWidth().selectable(reason == r) { reason = r }.padding(vertical = 4.dp), verticalAlignment = Alignment.CenterVertically) {
                    RadioButton(reason == r, null); Spacer(Modifier.width(8.dp)); Text(r.replace(Regex("([a-z])([A-Z])"), "$1 $2"))
                }
            }
            OutlinedTextField(details, { if (it.length <= 1000) details = it }, Modifier.fillMaxWidth(), label = { Text("Details (optional)") }, minLines = 2)
        }
    }, confirmButton = { TextButton(onClick = { reason?.let { onSubmit(it, details) } }, enabled = reason != null) { Text("Submit") } },
        dismissButton = { TextButton(onClick = onDismiss) { Text("Cancel") } })
}

@Composable
internal fun CollectionPickerDialog(collections: List<SavedCollection>, onDismiss: () -> Unit, onPick: (String?, String?) -> Unit) {
    var newName by remember { mutableStateOf("") }
    AlertDialog(onDismissRequest = onDismiss, title = { Text("Add to collection") }, text = {
        Column {
            collections.forEach { c -> ListItem(headlineContent = { Text(c.name) }, supportingContent = { Text("${c.itemCount} items") }, modifier = Modifier.clickable { onPick(c.id, null) }) }
            OutlinedTextField(newName, { newName = it.take(100) }, Modifier.fillMaxWidth(), label = { Text("New collection name") }, singleLine = true)
        }
    }, confirmButton = { TextButton(onClick = { onPick(null, newName) }, enabled = newName.isNotBlank()) { Text("Create & add") } },
        dismissButton = { TextButton(onClick = onDismiss) { Text("Cancel") } })
}

@Composable
fun RandomQuestionScreen(nav: NavController, viewModel: RandomQuestionViewModel = hiltViewModel()) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    LaunchedEffect(state) {
        (state as? Loadable.Ready)?.data?.let { q -> nav.navigate(Routes.QuestionDetail(q.id)) { popUpTo<Routes.RandomQuestion> { inclusive = true } } }
    }
    Scaffold(topBar = { DiTopBar("Random question", onBack = { nav.popBackStack() }) }) { padding ->
        LoadableContent(state, onRetry = { viewModel.load() }, modifier = Modifier.padding(padding), onUpgrade = { nav.navigate(Routes.Subscription) }) { LoadingState(Modifier.padding(padding)) }
    }
}

@Composable
fun BookmarksScreen(onBack: () -> Unit, onQuestion: (String) -> Unit, viewModel: BookmarksViewModel = hiltViewModel()) {
    val items by viewModel.bookmarks.collectAsStateWithLifecycle()
    Scaffold(topBar = { DiTopBar("Bookmarks", onBack = onBack) }) { padding ->
        when {
            items == null -> LoadingState(Modifier.padding(padding))
            items!!.isEmpty() -> EmptyState("No bookmarks yet", "Tap the bookmark icon on any question to save it here — available offline too.",
                Modifier.padding(padding), Icons.Filled.BookmarkBorder)
            else -> LazyColumn(Modifier.padding(padding), contentPadding = PaddingValues(16.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
                items(items!!, key = { it.id }) { q ->
                    Box {
                        QuestionRow(q) { onQuestion(q.id) }
                        IconButton(onClick = { viewModel.remove(q) }, modifier = Modifier.align(Alignment.TopEnd)) { Icon(Icons.Filled.BookmarkRemove, "Remove bookmark") }
                    }
                }
            }
        }
    }
}

@Composable
fun NotesScreen(onBack: () -> Unit, onQuestion: (String) -> Unit, viewModel: NotesViewModel = hiltViewModel()) {
    val notes by viewModel.notes.collectAsStateWithLifecycle()
    val context = LocalContext.current
    val snackbar = remember { SnackbarHostState() }
    var confirmDelete by remember { mutableStateOf<Note?>(null) }
    LaunchedEffect(Unit) { viewModel.events.collect { e -> when (e) { is QuestionEvent.Share -> context.shareText(e.text); is QuestionEvent.Message -> snackbar.showSnackbar(e.text); else -> Unit } } }
    Scaffold(topBar = {
        DiTopBar("My notes", onBack = onBack) { if (!notes.isNullOrEmpty()) IconButton(onClick = viewModel::export) { Icon(Icons.Filled.IosShare, "Export notes") } }
    }, snackbarHost = { SnackbarHost(snackbar) }) { padding ->
        when {
            notes == null -> LoadingState(Modifier.padding(padding))
            notes!!.isEmpty() -> EmptyState("No notes yet", "Add personal notes to any question. They sync across your devices.", Modifier.padding(padding), Icons.Filled.EditNote)
            else -> LazyColumn(Modifier.padding(padding), contentPadding = PaddingValues(16.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
                items(notes!!, key = { it.questionId }) { n ->
                    DiCard(onClick = { onQuestion(n.questionId) }) {
                        Row {
                            Column(Modifier.weight(1f)) {
                                Text(n.technology, style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.primary)
                                Text(n.questionTitle, style = MaterialTheme.typography.titleSmall, maxLines = 2, overflow = TextOverflow.Ellipsis)
                            }
                            IconButton(onClick = { confirmDelete = n }) { Icon(Icons.Filled.Delete, "Delete note") }
                        }
                        Text(n.content, style = MaterialTheme.typography.bodyMedium, maxLines = 4, overflow = TextOverflow.Ellipsis)
                        TimeFormat.parse(n.updatedAt)?.let { Text("Updated ${TimeFormat.relative(n.updatedAt)}", style = MaterialTheme.typography.labelSmall) }
                    }
                }
            }
        }
    }
    confirmDelete?.let { n -> ConfirmDialog("Delete note?", "Your note on \"${n.questionTitle}\" will be removed.", "Delete", { viewModel.delete(n); confirmDelete = null }, { confirmDelete = null }, destructive = true) }
}

@Composable
fun CollectionsScreen(onBack: () -> Unit, onOpen: (SavedCollection) -> Unit, viewModel: CollectionsViewModel = hiltViewModel()) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    var create by remember { mutableStateOf(false) }
    var delete by remember { mutableStateOf<SavedCollection?>(null) }
    val snackbar = remember { SnackbarHostState() }
    LaunchedEffect(Unit) { viewModel.events.collect { if (it is QuestionEvent.Message) snackbar.showSnackbar(it.text) } }
    Scaffold(topBar = { DiTopBar("Collections", onBack = onBack) }, snackbarHost = { SnackbarHost(snackbar) },
        floatingActionButton = { ExtendedFloatingActionButton(onClick = { create = true }, icon = { Icon(Icons.Filled.Add, null) }, text = { Text("New collection") }) }) { padding ->
        LoadableContent(state, onRetry = { viewModel.load() }, modifier = Modifier.padding(padding)) { list ->
            if (list.isEmpty()) EmptyState("No collections", "Group questions and challenges into your own study lists.", Modifier.padding(padding), Icons.Filled.CollectionsBookmark)
            else LazyColumn(Modifier.padding(padding), contentPadding = PaddingValues(16.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
                items(list, key = { it.id }) { c ->
                    DiCard(onClick = { onOpen(c) }) {
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            Column(Modifier.weight(1f)) {
                                Text(c.name, style = MaterialTheme.typography.titleMedium)
                                Text(listOfNotNull(c.description, "${c.itemCount} items").joinToString(" · "), style = MaterialTheme.typography.bodySmall)
                            }
                            IconButton(onClick = { delete = c }) { Icon(Icons.Filled.Delete, "Delete collection") }
                        }
                    }
                }
            }
        }
    }
    if (create) {
        var name by remember { mutableStateOf("") }
        var desc by remember { mutableStateOf("") }
        AlertDialog(onDismissRequest = { create = false }, title = { Text("New collection") }, text = {
            Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                OutlinedTextField(name, { name = it.take(100) }, label = { Text("Name") }, singleLine = true)
                OutlinedTextField(desc, { desc = it.take(500) }, label = { Text("Description (optional)") })
            }
        }, confirmButton = { TextButton(onClick = { viewModel.create(name, desc); create = false }, enabled = name.isNotBlank()) { Text("Create") } },
            dismissButton = { TextButton(onClick = { create = false }) { Text("Cancel") } })
    }
    delete?.let { c -> ConfirmDialog("Delete \"${c.name}\"?", "The collection is removed; the questions themselves are not affected.", "Delete", { viewModel.delete(c.id); delete = null }, { delete = null }, destructive = true) }
}

@Composable
fun CollectionDetailScreen(nav: NavController, viewModel: CollectionDetailViewModel = hiltViewModel()) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    Scaffold(topBar = { DiTopBar(viewModel.route.name, onBack = { nav.popBackStack() }) }) { padding ->
        LoadableContent(state, onRetry = { viewModel.load() }, modifier = Modifier.padding(padding)) { c ->
            if (c.items.isEmpty()) EmptyState("Empty collection", "Use \"Add to collection\" on a question or challenge.", Modifier.padding(padding))
            else LazyColumn(Modifier.padding(padding), contentPadding = PaddingValues(16.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                c.description?.let { item { Text(it, style = MaterialTheme.typography.bodyMedium) } }
                items(c.items, key = { it.id }) { item ->
                    ListItem(
                        headlineContent = { Text(item.title, maxLines = 2, overflow = TextOverflow.Ellipsis) },
                        supportingContent = { item.subtitle?.let { Text(it) } },
                        leadingContent = { Icon(when (item.itemType) { "CodingChallenge" -> Icons.Filled.Code; "AnswerEvaluation" -> Icons.Filled.Grading; else -> Icons.AutoMirrored.Filled.MenuBook }, null) },
                        trailingContent = { IconButton(onClick = { viewModel.remove(item) }) { Icon(Icons.Filled.RemoveCircleOutline, "Remove") } },
                        modifier = Modifier.clickable {
                            nav.navigate(when (item.itemType) {
                                "CodingChallenge" -> Routes.ChallengeDetail(item.itemId)
                                "AnswerEvaluation" -> Routes.EvaluationDetail(item.itemId)
                                else -> Routes.QuestionDetail(item.itemId)
                            })
                        },
                    )
                }
            }
        }
    }
}
