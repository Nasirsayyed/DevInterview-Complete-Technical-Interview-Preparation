package com.devinterview.feature.learning

import android.content.Intent
import android.net.Uri
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.LazyRow
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.rememberScrollState
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
import com.devinterview.core.designsystem.*
import com.devinterview.core.navigation.Routes
import com.devinterview.domain.model.*

fun NavGraphBuilder.learningGraph(nav: NavController, openDrawer: () -> Unit) {
    composable<Routes.Learn> { LearnScreen(nav, openDrawer) }
    composable<Routes.LearningPathDetail> { LearningPathScreen(nav) }
    composable<Routes.LessonDetail> { LessonScreen(nav) }
    composable<Routes.Companies> { CompaniesScreen(nav) }
    composable<Routes.CompanyDetail> { CompanyScreen(nav) }
    composable<Routes.CompanyCollection> { CompanyCollectionScreen(nav) }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun LearnScreen(nav: NavController, openDrawer: () -> Unit, viewModel: LearnViewModel = hiltViewModel()) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    val downloaded by viewModel.downloaded.collectAsStateWithLifecycle()
    val level by viewModel.levelFilter.collectAsStateWithLifecycle()
    Scaffold(topBar = {
        TopAppBar(title = { Text("Learn") }, navigationIcon = { IconButton(onClick = openDrawer) { Icon(Icons.Filled.Menu, "Open menu") } },
            actions = { IconButton(onClick = { nav.navigate(Routes.TechnologyExplorer) }) { Icon(Icons.AutoMirrored.Filled.MenuBook, "Question bank") } })
    }) { padding ->
        LoadableContent(state, onRetry = { viewModel.load() }, modifier = Modifier.padding(padding)) { paths ->
            LazyColumn(Modifier.padding(padding), contentPadding = PaddingValues(16.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
                item {
                    DiCard(onClick = { nav.navigate(Routes.Companies) }) {
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            Icon(Icons.Filled.Business, null, tint = MaterialTheme.colorScheme.secondary, modifier = Modifier.size(32.dp))
                            Spacer(Modifier.width(12.dp))
                            Column(Modifier.weight(1f)) {
                                Text("Company preparation", style = MaterialTheme.typography.titleMedium)
                                Text("Interview processes, focus topics and practice sets", style = MaterialTheme.typography.bodySmall)
                            }
                            PremiumBadge()
                        }
                    }
                }
                item {
                    LazyRow(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                        item { FilterChip(level == null, onClick = { viewModel.levelFilter.value = null }, label = { Text("All levels") }) }
                        items(listOf("Beginner", "Intermediate", "Advanced", "Expert")) { l ->
                            FilterChip(level == l, onClick = { viewModel.levelFilter.value = l }, label = { Text(l) })
                        }
                    }
                }
                val filtered = paths.filter { level == null || it.level == level }
                if (filtered.isEmpty()) item { EmptyState("No paths at this level", "Try another level.") }
                items(filtered, key = { it.id }) { p ->
                    DiCard(onClick = { nav.navigate(Routes.LearningPathDetail(p.id)) }, modifier = Modifier.testTag("path_${p.slug}")) {
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            TagChip(p.level); p.technology?.let { Spacer(Modifier.width(6.dp)); TagChip(it.name) }
                            if (p.isPremium) { Spacer(Modifier.width(6.dp)); PremiumBadge() }
                            Spacer(Modifier.weight(1f))
                            if (p.id in downloaded) Icon(Icons.Filled.OfflinePin, "Available offline", tint = DiTheme.colors.success, modifier = Modifier.size(20.dp))
                        }
                        Spacer(Modifier.height(6.dp))
                        Text(p.title, style = MaterialTheme.typography.titleMedium)
                        Text(p.description, style = MaterialTheme.typography.bodySmall, maxLines = 2, overflow = TextOverflow.Ellipsis)
                        Spacer(Modifier.height(8.dp))
                        AnimatedProgressBar(p.progressPercent / 100f)
                        Text("${p.completedLessons}/${p.lessonCount} lessons · ${p.moduleCount} modules · ~${p.estimatedHours} h", style = MaterialTheme.typography.labelSmall)
                    }
                }
            }
        }
    }
}

@Composable
fun LearningPathScreen(nav: NavController, viewModel: LearningPathViewModel = hiltViewModel()) {
    val s by viewModel.state.collectAsStateWithLifecycle()
    val downloaded by viewModel.isDownloaded.collectAsStateWithLifecycle()
    val snackbar = remember { SnackbarHostState() }
    LaunchedEffect(s.message) { s.message?.let { snackbar.showSnackbar(it); viewModel.clearMessage() } }
    Scaffold(topBar = {
        DiTopBar(s.path.dataOrNull?.title ?: "Learning path", onBack = { nav.popBackStack() }) {
            if (s.path is Loadable.Ready) {
                if (s.downloading) CircularProgressIndicator(Modifier.size(24.dp).padding(2.dp), strokeWidth = 2.dp)
                else IconButton(onClick = viewModel::download) {
                    Icon(if (downloaded) Icons.Filled.OfflinePin else Icons.Filled.Download, if (downloaded) "Update offline copy" else "Download for offline")
                }
            }
        }
    }, snackbarHost = { SnackbarHost(snackbar) }) { padding ->
        LoadableContent(s.path, onRetry = { viewModel.load() }, modifier = Modifier.padding(padding), onUpgrade = { nav.navigate(Routes.Subscription) }) { p ->
            LazyColumn(Modifier.padding(padding), contentPadding = PaddingValues(16.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
                item {
                    GradientCard {
                        Text(p.title, style = MaterialTheme.typography.titleLarge, color = Color.White)
                        Text(p.description, color = Color.White.copy(alpha = .9f))
                        Spacer(Modifier.height(8.dp))
                        AnimatedProgressBar(p.progressPercent / 100f, color = Color.White)
                        Text("${p.completedLessons} of ${p.lessonCount} lessons · ${p.progressPercent}%", color = Color.White, style = MaterialTheme.typography.labelMedium)
                        p.nextLessonId?.let { next ->
                            Spacer(Modifier.height(8.dp))
                            Button(onClick = { nav.navigate(Routes.LessonDetail(next)) }, colors = ButtonDefaults.buttonColors(containerColor = Color.White, contentColor = MaterialTheme.colorScheme.primary)) {
                                Text(if (p.completedLessons == 0) "Start learning" else "Continue")
                            }
                        }
                    }
                }
                items(p.modules.sortedBy { it.order }, key = { it.id }) { m ->
                    DiCard {
                        Text("Module ${m.order + 1}: ${m.title}", style = MaterialTheme.typography.titleMedium)
                        m.description?.let { Text(it, style = MaterialTheme.typography.bodySmall) }
                        Text("${m.completedLessons}/${m.lessons.size} complete", style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.primary)
                        m.lessons.forEach { l ->
                            ListItem(
                                headlineContent = { Text(l.title) },
                                supportingContent = { Text("${l.type} · ${l.estimatedMinutes} min") },
                                leadingContent = {
                                    Icon(when (l.status) { "Completed" -> Icons.Filled.CheckCircle; "InProgress" -> Icons.Filled.PlayCircle; else -> when (l.type) { "Quiz" -> Icons.Filled.Quiz; "Exercise" -> Icons.Filled.Code; else -> Icons.AutoMirrored.Filled.MenuBook } },
                                        null, tint = if (l.status == "Completed") DiTheme.colors.success else MaterialTheme.colorScheme.primary)
                                },
                                modifier = Modifier.clickable { nav.navigate(Routes.LessonDetail(l.id)) },
                            )
                        }
                    }
                }
            }
        }
    }
}

@Composable
fun LessonScreen(nav: NavController, viewModel: LessonViewModel = hiltViewModel()) {
    val s by viewModel.state.collectAsStateWithLifecycle()
    val snackbar = remember { SnackbarHostState() }
    LaunchedEffect(Unit) { viewModel.events.collect { snackbar.showSnackbar(it) } }
    val lesson = s.lesson.dataOrNull
    Scaffold(
        topBar = { DiTopBar(lesson?.moduleTitle ?: "Lesson", onBack = { nav.popBackStack() }) },
        snackbarHost = { SnackbarHost(snackbar) },
        bottomBar = {
            if (lesson != null) BottomAppBar {
                IconButton(onClick = { lesson.previousLessonId?.let(viewModel::open) }, enabled = lesson.previousLessonId != null) { Icon(Icons.AutoMirrored.Filled.ArrowBack, "Previous lesson") }
                Spacer(Modifier.weight(1f))
                if (lesson.status != "Completed") PrimaryButton("Mark complete", onClick = viewModel::complete, loading = s.completing, icon = Icons.Filled.Check, modifier = Modifier.testTag("complete_lesson"))
                else Row(verticalAlignment = Alignment.CenterVertically) { Icon(Icons.Filled.CheckCircle, null, tint = DiTheme.colors.success); Spacer(Modifier.width(4.dp)); Text("Completed") }
                Spacer(Modifier.weight(1f))
                IconButton(onClick = { lesson.nextLessonId?.let(viewModel::open) }, enabled = lesson.nextLessonId != null) { Icon(Icons.AutoMirrored.Filled.ArrowForward, "Next lesson") }
            }
        },
    ) { padding ->
        LoadableContent(s.lesson, onRetry = { viewModel.load() }, modifier = Modifier.padding(padding), onUpgrade = { nav.navigate(Routes.Subscription) }) { l ->
            Column(Modifier.padding(padding).verticalScroll(rememberScrollState()).padding(16.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
                Text(l.pathTitle, style = MaterialTheme.typography.labelMedium, color = MaterialTheme.colorScheme.primary)
                Text(l.title, style = MaterialTheme.typography.headlineSmall)
                Text("${l.type} · ${l.estimatedMinutes} min", style = MaterialTheme.typography.bodySmall)
                MarkdownText(l.content)
                l.exerciseInstructions?.let { DiCard { Text("Exercise", style = MaterialTheme.typography.titleMedium); MarkdownText(it) } }
                l.quizId?.let { quizId ->
                    PrimaryButton("Take the lesson quiz", onClick = { nav.navigate(Routes.QuizPlayer(quizId)) }, icon = Icons.Filled.Quiz, modifier = Modifier.fillMaxWidth())
                }
                if (l.questions.isNotEmpty()) {
                    SectionHeader("Practice questions")
                    l.questions.forEach { q ->
                        DiCard(onClick = { nav.navigate(Routes.QuestionDetail(q.id)) }) {
                            Text(q.title, style = MaterialTheme.typography.titleSmall); Row { DifficultyChip(q.difficulty) }
                        }
                    }
                }
                s.pathProgress?.let { Text("Path progress: $it%", style = MaterialTheme.typography.labelLarge, color = MaterialTheme.colorScheme.primary) }
            }
        }
    }
}

@Composable
fun CompaniesScreen(nav: NavController, viewModel: CompaniesViewModel = hiltViewModel()) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    Scaffold(topBar = { DiTopBar("Company preparation", onBack = { nav.popBackStack() }) }) { padding ->
        LoadableContent(state, onRetry = { viewModel.load() }, modifier = Modifier.padding(padding)) { companies ->
            LazyColumn(Modifier.padding(padding), contentPadding = PaddingValues(16.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
                item {
                    Text("Profiles describe typical interview formats and the skills worth preparing. Practice sets are original material unless marked as verified.",
                        style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                }
                if (companies.isEmpty()) item { EmptyState("No company profiles yet", "Check back soon.") }
                items(companies, key = { it.id }) { c ->
                    DiCard(onClick = { nav.navigate(Routes.CompanyDetail(c.id)) }) {
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            Avatar(c.name, c.logoUrl)
                            Spacer(Modifier.width(12.dp))
                            Column(Modifier.weight(1f)) {
                                Text(c.name, style = MaterialTheme.typography.titleMedium)
                                Text(listOfNotNull(c.industry, "${c.topicCount} topics", "${c.collectionCount} practice sets").joinToString(" · "), style = MaterialTheme.typography.bodySmall)
                            }
                            DifficultyChip(c.difficultyLevel)
                        }
                    }
                }
            }
        }
    }
}

@Composable
private fun SourceChip(source: String) {
    AssistChip(onClick = {}, enabled = false, label = { Text(sourceLabel(source), style = MaterialTheme.typography.labelSmall) },
        leadingIcon = { Icon(if (source == "Verified") Icons.Filled.Verified else Icons.Filled.Info, null, Modifier.size(16.dp)) })
}

@Composable
fun CompanyScreen(nav: NavController, viewModel: CompanyViewModel = hiltViewModel()) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    val context = LocalContext.current
    Scaffold(topBar = { DiTopBar(state.dataOrNull?.name ?: "Company", onBack = { nav.popBackStack() }) }) { padding ->
        LoadableContent(state, onRetry = { viewModel.load() }, modifier = Modifier.padding(padding), onUpgrade = { nav.navigate(Routes.Subscription) }) { c ->
            LazyColumn(Modifier.padding(padding), contentPadding = PaddingValues(16.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
                item {
                    DiCard {
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            Avatar(c.name, null, size = 56.dp); Spacer(Modifier.width(12.dp))
                            Column(Modifier.weight(1f)) { Text(c.name, style = MaterialTheme.typography.titleLarge); c.industry?.let { Text(it, style = MaterialTheme.typography.bodySmall) } }
                            DifficultyChip(c.difficultyLevel)
                        }
                        Spacer(Modifier.height(8.dp))
                        Text(c.overview)
                        SourceChip(c.overviewSource)
                        c.websiteUrl?.let { url -> TextButton(onClick = { context.startActivity(Intent(Intent.ACTION_VIEW, Uri.parse(url))) }) { Text("Careers website") } }
                    }
                }
                if (c.disclaimer.isNotBlank()) item {
                    Text(c.disclaimer, style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                }
                if (c.rounds.isNotEmpty()) {
                    item { SectionHeader("Typical interview process") }
                    items(c.rounds.sortedBy { it.order }, key = { "r${it.order}" }) { r ->
                        DiCard {
                            Text("${r.order + 1}. ${r.name}", style = MaterialTheme.typography.titleSmall)
                            Text(r.description, style = MaterialTheme.typography.bodySmall)
                            SourceChip(r.source)
                        }
                    }
                }
                if (c.topics.isNotEmpty()) {
                    item { SectionHeader("Focus topics") }
                    items(c.topics, key = { it.id }) { t ->
                        ListItem(headlineContent = { Text(t.name) }, supportingContent = { t.description?.let { Text(it) } },
                            modifier = if (t.technology != null) Modifier.clickable { nav.navigate(Routes.Categories(t.technology!!.id, t.technology!!.name)) } else Modifier)
                    }
                }
                if (c.collections.isNotEmpty()) {
                    item { SectionHeader("Practice sets") }
                    items(c.collections, key = { it.id }) { col ->
                        DiCard(onClick = { nav.navigate(Routes.CompanyCollection(col.id)) }) {
                            Text(col.title, style = MaterialTheme.typography.titleMedium)
                            col.description?.let { Text(it, style = MaterialTheme.typography.bodySmall) }
                            Text("${col.kind} · ${col.questionCount} questions · ${col.challengeCount} challenges", style = MaterialTheme.typography.labelSmall)
                            SourceChip(col.source)
                        }
                    }
                }
            }
        }
    }
}

@Composable
fun CompanyCollectionScreen(nav: NavController, viewModel: CompanyCollectionViewModel = hiltViewModel()) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    Scaffold(topBar = { DiTopBar(state.dataOrNull?.collection?.title ?: "Practice set", onBack = { nav.popBackStack() }) }) { padding ->
        LoadableContent(state, onRetry = { viewModel.load() }, modifier = Modifier.padding(padding), onUpgrade = { nav.navigate(Routes.Subscription) }) { d ->
            LazyColumn(Modifier.padding(padding), contentPadding = PaddingValues(16.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
                item {
                    Text(d.companyName, style = MaterialTheme.typography.labelLarge, color = MaterialTheme.colorScheme.primary)
                    d.collection.description?.let { Text(it) }
                    SourceChip(d.collection.source)
                    d.collection.sourceReference?.let { Text("Source: $it", style = MaterialTheme.typography.labelSmall) }
                    if (d.disclaimer.isNotBlank()) Text(d.disclaimer, style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                }
                if (d.questions.isNotEmpty()) item { SectionHeader("Questions") }
                items(d.questions, key = { "q" + it.id }) { q ->
                    DiCard(onClick = { nav.navigate(Routes.QuestionDetail(q.id)) }) {
                        Text(q.title, style = MaterialTheme.typography.titleSmall); Row { DifficultyChip(q.difficulty); Spacer(Modifier.width(6.dp)); TagChip(q.technology.name) }
                    }
                }
                if (d.challenges.isNotEmpty()) item { SectionHeader("Coding challenges") }
                items(d.challenges, key = { "c" + it.id }) { c ->
                    DiCard(onClick = { nav.navigate(Routes.ChallengeDetail(c.id)) }) {
                        Text(c.title, style = MaterialTheme.typography.titleSmall); Row { DifficultyChip(c.difficulty) }
                    }
                }
                if (d.questions.isEmpty() && d.challenges.isEmpty()) item { EmptyState("Nothing here yet", "This practice set has no published items.") }
            }
        }
    }
}
