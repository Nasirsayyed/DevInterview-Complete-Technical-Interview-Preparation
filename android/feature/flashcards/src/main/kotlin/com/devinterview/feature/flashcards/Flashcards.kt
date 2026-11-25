package com.devinterview.feature.flashcards

import androidx.compose.animation.core.Animatable
import androidx.compose.animation.core.animateFloatAsState
import androidx.compose.animation.core.tween
import androidx.compose.foundation.gestures.detectHorizontalDragGestures
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.IntOffset
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewModelScope
import androidx.navigation.NavController
import androidx.navigation.NavGraphBuilder
import androidx.navigation.compose.composable
import androidx.navigation.toRoute
import com.devinterview.core.designsystem.*
import com.devinterview.core.navigation.Routes
import com.devinterview.domain.model.*
import com.devinterview.domain.repository.FlashcardRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import javax.inject.Inject
import kotlin.math.abs
import kotlin.math.roundToInt

@HiltViewModel
class FlashcardDecksViewModel @Inject constructor(private val repo: FlashcardRepository) : ViewModel() {
    private val _state = MutableStateFlow<Loadable<List<FlashcardDeck>>>(Loadable.Loading)
    val state: StateFlow<Loadable<List<FlashcardDeck>>> = _state
    init { load() }
    fun load() = viewModelScope.launch { if (_state.value !is Loadable.Ready) _state.value = Loadable.Loading; _state.value = repo.decks().toLoadable() }
}

data class StudyUiState(
    val cards: Loadable<List<Flashcard>> = Loadable.Loading, val index: Int = 0, val flipped: Boolean = false, val dueOnly: Boolean = true,
    val reviewed: Int = 0, val grades: Map<FlashcardGrade, Int> = emptyMap(),
) {
    val current: Flashcard? get() = cards.dataOrNull?.getOrNull(index)
    val finished: Boolean get() = cards is Loadable.Ready && current == null
}

/**
 * Study session. Grading is recorded locally first (works offline) and uploaded; the server computes the next review
 * date with SM-2, so the app never schedules cards itself.
 */
@HiltViewModel
class FlashcardStudyViewModel @Inject constructor(private val repo: FlashcardRepository, handle: SavedStateHandle) : ViewModel() {
    val route = handle.toRoute<Routes.FlashcardStudy>()
    private val _state = MutableStateFlow(StudyUiState())
    val state: StateFlow<StudyUiState> = _state

    init { load(dueOnly = true) }

    fun load(dueOnly: Boolean = _state.value.dueOnly) = viewModelScope.launch {
        _state.value = StudyUiState(dueOnly = dueOnly)
        _state.update { it.copy(cards = repo.session(route.technologyId, dueOnly, route.bookmarkedOnly).toLoadable()) }
    }

    fun flip() = _state.update { it.copy(flipped = !it.flipped) }

    fun grade(grade: FlashcardGrade) {
        val card = _state.value.current ?: return
        _state.update { it.copy(index = it.index + 1, flipped = false, reviewed = it.reviewed + 1, grades = it.grades + (grade to (it.grades[grade] ?: 0) + 1)) }
        viewModelScope.launch { repo.review(card, grade) }
    }

    fun toggleBookmark() {
        val card = _state.value.current ?: return
        val target = !card.isBookmarked
        _state.update { s -> s.copy(cards = (s.cards as? Loadable.Ready)?.let { r -> Loadable.Ready(r.data.map { if (it.id == card.id) it.copy(isBookmarked = target) else it }) } ?: s.cards) }
        viewModelScope.launch { repo.setBookmark(card.id, target) }
    }
}

fun NavGraphBuilder.flashcardsGraph(nav: NavController) {
    composable<Routes.FlashcardDecks> { FlashcardDecksScreen(nav) }
    composable<Routes.FlashcardStudy> { FlashcardStudyScreen(nav) }
}

@Composable
fun FlashcardDecksScreen(nav: NavController, viewModel: FlashcardDecksViewModel = hiltViewModel()) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    Scaffold(topBar = {
        DiTopBar("Flashcards", onBack = { nav.popBackStack() }) {
            IconButton(onClick = { nav.navigate(Routes.FlashcardStudy(deckName = "Bookmarked cards", bookmarkedOnly = true)) }) { Icon(Icons.Filled.Bookmarks, "Bookmarked cards") }
        }
    }) { padding ->
        LoadableContent(state, onRetry = { viewModel.load() }, modifier = Modifier.padding(padding)) { decks ->
            LazyColumn(Modifier.padding(padding), contentPadding = PaddingValues(16.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
                val due = decks.sumOf { it.dueCount }
                item {
                    GradientCard(onClick = { nav.navigate(Routes.FlashcardStudy()) }, modifier = Modifier.testTag("study_all")) {
                        Text("Review all", style = MaterialTheme.typography.titleLarge, color = Color.White)
                        Text(if (due > 0) "$due cards due today" else "No cards due — study new cards or review ahead", color = Color.White.copy(alpha = .9f))
                    }
                }
                if (decks.isEmpty()) item { EmptyState("No flashcards yet", "Flashcards will appear once content is published.") }
                items(decks, key = { it.technologyId ?: it.name }) { d ->
                    DiCard(onClick = { nav.navigate(Routes.FlashcardStudy(d.technologyId, d.name)) }) {
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            Column(Modifier.weight(1f)) {
                                Text(d.name, style = MaterialTheme.typography.titleMedium)
                                Text("${d.totalCards} cards · ${d.newCount} new · ${d.masteredCount} mastered", style = MaterialTheme.typography.bodySmall)
                            }
                            if (d.dueCount > 0) Badge(containerColor = MaterialTheme.colorScheme.primary) { Text("${d.dueCount} due", Modifier.padding(4.dp)) }
                        }
                        Spacer(Modifier.height(6.dp))
                        AnimatedProgressBar(if (d.totalCards == 0) 0f else d.masteredCount / d.totalCards.toFloat(), color = DiTheme.colors.success)
                    }
                }
            }
        }
    }
}

@Composable
fun FlashcardStudyScreen(nav: NavController, viewModel: FlashcardStudyViewModel = hiltViewModel()) {
    val s by viewModel.state.collectAsStateWithLifecycle()
    Scaffold(topBar = {
        DiTopBar(viewModel.route.deckName, onBack = { nav.popBackStack() }) {
            s.current?.let { c -> IconButton(onClick = viewModel::toggleBookmark) { Icon(if (c.isBookmarked) Icons.Filled.Bookmark else Icons.Filled.BookmarkBorder, "Bookmark card") } }
        }
    }) { padding ->
        LoadableContent(s.cards, onRetry = { viewModel.load() }, modifier = Modifier.padding(padding), onUpgrade = { nav.navigate(Routes.Subscription) }) { cards ->
            Column(Modifier.padding(padding).fillMaxSize().padding(16.dp), horizontalAlignment = Alignment.CenterHorizontally) {
                val card = s.current
                when {
                    cards.isEmpty() -> EmptyState(
                        if (s.dueOnly) "All caught up!" else "No cards", if (s.dueOnly) "Nothing is due right now. Practise ahead with every card in this deck." else "This deck has no cards yet.",
                        icon = Icons.Filled.Celebration, action = if (s.dueOnly) "Study all cards" else null, onAction = { viewModel.load(dueOnly = false) })
                    card == null -> SessionSummary(s, onAgain = { viewModel.load() }, onDone = { nav.popBackStack() })
                    else -> {
                        Text("${s.index + 1} / ${cards.size}", style = MaterialTheme.typography.labelLarge)
                        AnimatedProgressBar(s.index / cards.size.toFloat(), Modifier.padding(vertical = 8.dp))
                        SwipeableCard(card, s.flipped, onFlip = viewModel::flip,
                            onSwipe = { right -> if (s.flipped) viewModel.grade(if (right) FlashcardGrade.Good else FlashcardGrade.Again) else viewModel.flip() },
                            modifier = Modifier.weight(1f))
                        Spacer(Modifier.height(12.dp))
                        if (!s.flipped) PrimaryButton("Show answer", onClick = viewModel::flip, modifier = Modifier.fillMaxWidth().testTag("flip_card"))
                        else Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                            GradeButton("Again", DiTheme.colors.danger, Modifier.weight(1f)) { viewModel.grade(FlashcardGrade.Again) }
                            GradeButton("Hard", DiTheme.colors.warning, Modifier.weight(1f)) { viewModel.grade(FlashcardGrade.Hard) }
                            GradeButton("Good", DiTheme.colors.success, Modifier.weight(1f)) { viewModel.grade(FlashcardGrade.Good) }
                            GradeButton("Easy", MaterialTheme.colorScheme.primary, Modifier.weight(1f)) { viewModel.grade(FlashcardGrade.Easy) }
                        }
                        Text(if (s.flipped) "Swipe right = Good, left = Again" else "Tap the card to flip", style = MaterialTheme.typography.labelSmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant, modifier = Modifier.padding(top = 8.dp))
                    }
                }
            }
        }
    }
}

@Composable
private fun GradeButton(label: String, color: Color, modifier: Modifier, onClick: () -> Unit) {
    Button(onClick = onClick, modifier = modifier.testTag("grade_$label"), colors = ButtonDefaults.buttonColors(containerColor = color), contentPadding = PaddingValues(4.dp)) { Text(label) }
}

@Composable
private fun SwipeableCard(card: Flashcard, flipped: Boolean, onFlip: () -> Unit, onSwipe: (Boolean) -> Unit, modifier: Modifier) {
    val rotation by animateFloatAsState(if (flipped) 180f else 0f, tween(400), label = "flip")
    val offsetX = remember(card.id) { Animatable(0f) }
    val scope = rememberCoroutineScope()
    Box(
        modifier.fillMaxWidth()
            .offset { IntOffset(offsetX.value.roundToInt(), 0) }
            .graphicsLayer { rotationY = rotation; rotationZ = offsetX.value / 40f; cameraDistance = 12 * density }
            .pointerInput(card.id) {
                detectHorizontalDragGestures(
                    onDragEnd = {
                        scope.launch {
                            if (abs(offsetX.value) > size.width / 3f) { val right = offsetX.value > 0; offsetX.snapTo(0f); onSwipe(right) }
                            else offsetX.animateTo(0f)
                        }
                    },
                ) { change, drag -> change.consume(); scope.launch { offsetX.snapTo(offsetX.value + drag) } }
            }
            .testTag("flashcard"),
    ) {
        ElevatedCard(onClick = onFlip, modifier = Modifier.fillMaxSize()) {
            Column(
                Modifier.fillMaxSize().graphicsLayer { if (rotation > 90f) rotationY = 180f }.verticalScroll(rememberScrollState()).padding(24.dp),
                horizontalAlignment = Alignment.CenterHorizontally, verticalArrangement = Arrangement.Center,
            ) {
                Row { card.technology?.let { TagChip(it.name) }; Spacer(Modifier.width(6.dp)); DifficultyChip(card.difficulty) }
                Spacer(Modifier.height(16.dp))
                if (rotation <= 90f) Text(card.front, style = MaterialTheme.typography.headlineSmall, textAlign = TextAlign.Center)
                else MarkdownText(card.back)
            }
        }
    }
}

@Composable
private fun SessionSummary(s: StudyUiState, onAgain: () -> Unit, onDone: () -> Unit) {
    Column(Modifier.fillMaxWidth().padding(top = 32.dp), horizontalAlignment = Alignment.CenterHorizontally, verticalArrangement = Arrangement.spacedBy(12.dp)) {
        Icon(Icons.Filled.EmojiEvents, null, tint = DiTheme.colors.warning, modifier = Modifier.size(64.dp))
        Text("Session complete", style = MaterialTheme.typography.headlineSmall)
        Text("You reviewed ${s.reviewed} cards. They'll be scheduled for review based on how well you knew them.", textAlign = TextAlign.Center)
        FlashcardGrade.entries.forEach { g -> KeyValueRow(g.name, "${s.grades[g] ?: 0}") }
        PrimaryButton("Study more", onClick = onAgain, modifier = Modifier.fillMaxWidth())
        SecondaryButton("Done", onClick = onDone, modifier = Modifier.fillMaxWidth())
    }
}
