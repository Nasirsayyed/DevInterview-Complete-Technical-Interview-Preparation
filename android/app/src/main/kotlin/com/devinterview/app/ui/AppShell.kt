package com.devinterview.app.ui

import androidx.compose.animation.AnimatedVisibility
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.HelpOutline
import androidx.compose.material.icons.automirrored.filled.MenuBook
import androidx.compose.material.icons.filled.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.unit.dp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.navigation.NavDestination
import androidx.navigation.NavDestination.Companion.hasRoute
import androidx.navigation.NavDestination.Companion.hierarchy
import androidx.navigation.NavHostController
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.currentBackStackEntryAsState
import androidx.navigation.compose.rememberNavController
import com.devinterview.app.MainViewModel
import com.devinterview.core.designsystem.OfflineBanner
import com.devinterview.core.navigation.Routes
import com.devinterview.core.navigation.clearAll
import com.devinterview.core.navigation.navigateTopLevel
import com.devinterview.feature.analytics.analyticsGraph
import com.devinterview.feature.auth.authGraph
import com.devinterview.feature.coding.codingGraph
import com.devinterview.feature.dashboard.dashboardGraph
import com.devinterview.feature.flashcards.flashcardsGraph
import com.devinterview.feature.learning.learningGraph
import com.devinterview.feature.mockinterview.interviewGraph
import com.devinterview.feature.profile.profileGraph
import com.devinterview.feature.questions.questionsGraph
import com.devinterview.feature.quiz.quizGraph
import com.devinterview.feature.settings.settingsGraph
import kotlinx.coroutines.launch
import kotlin.reflect.KClass

data class TopLevelDestination(val route: Any, val routeClass: KClass<*>, val label: String, val icon: ImageVector)

val TopLevelDestinations = listOf(
    TopLevelDestination(Routes.Home, Routes.Home::class, "Home", Icons.Filled.Home),
    TopLevelDestination(Routes.Learn, Routes.Learn::class, "Learn", Icons.AutoMirrored.Filled.MenuBook),
    TopLevelDestination(Routes.Practice, Routes.Practice::class, "Practice", Icons.Filled.Code),
    TopLevelDestination(Routes.MockInterviewHome, Routes.MockInterviewHome::class, "Interview", Icons.Filled.RecordVoiceOver),
    TopLevelDestination(Routes.Profile, Routes.Profile::class, "Profile", Icons.Filled.Person),
)

private data class DrawerItem(val label: String, val icon: ImageVector, val route: Any)

private val drawerItems = listOf(
    DrawerItem("Question bank", Icons.AutoMirrored.Filled.MenuBook, Routes.TechnologyExplorer),
    DrawerItem("Quizzes", Icons.Filled.Quiz, Routes.QuizHome),
    DrawerItem("Coding challenges", Icons.Filled.Terminal, Routes.ChallengeList()),
    DrawerItem("Flashcards", Icons.Filled.Style, Routes.FlashcardDecks),
    DrawerItem("Companies", Icons.Filled.Business, Routes.Companies),
    DrawerItem("Answer evaluator", Icons.Filled.Grading, Routes.AnswerEvaluator()),
    DrawerItem("Code reviewer", Icons.Filled.Reviews, Routes.CodeReviewer),
    DrawerItem("Analytics", Icons.Filled.Insights, Routes.Analytics),
    DrawerItem("Achievements", Icons.Filled.EmojiEvents, Routes.Achievements),
    DrawerItem("Leaderboard", Icons.Filled.Leaderboard, Routes.Leaderboard),
    DrawerItem("Bookmarks", Icons.Filled.Bookmark, Routes.Bookmarks),
    DrawerItem("Notes", Icons.Filled.EditNote, Routes.Notes),
    DrawerItem("Collections", Icons.Filled.CollectionsBookmark, Routes.Collections),
    DrawerItem("Premium", Icons.Filled.WorkspacePremium, Routes.Subscription),
    DrawerItem("Settings", Icons.Filled.Settings, Routes.Settings),
    DrawerItem("Help & FAQ", Icons.AutoMirrored.Filled.HelpOutline, Routes.Help),
)

fun NavDestination?.isTopLevel() = this?.hierarchy?.any { d -> TopLevelDestinations.any { d.hasRoute(it.routeClass) } } == true

private val authRoutes = listOf(Routes.Splash::class, Routes.Onboarding::class, Routes.Login::class, Routes.Register::class,
    Routes.VerifyEmail::class, Routes.ForgotPassword::class)

fun NavDestination?.isAuth() = this != null && authRoutes.any { hasRoute(it) }

@Composable
fun DevInterviewAppShell(viewModel: MainViewModel, nav: NavHostController = rememberNavController()) {
    val backStack by nav.currentBackStackEntryAsState()
    val destination = backStack?.destination
    val sync by viewModel.syncState.collectAsStateWithLifecycle()
    val drawer = rememberDrawerState(DrawerValue.Closed)
    val scope = rememberCoroutineScope()
    val snackbar = remember { SnackbarHostState() }
    var pendingDeepLink by remember { mutableStateOf<Any?>(null) }

    LaunchedEffect(Unit) {
        viewModel.sessionExpired.collect {
            if (!nav.currentDestination.isAuth()) {
                nav.navigate(Routes.Login) { clearAll(nav) }
                snackbar.showSnackbar("Your session expired. Please sign in again.")
            }
        }
    }
    LaunchedEffect(Unit) { viewModel.deepLinks.collect { pendingDeepLink = it } }
    // Deep links (e.g. from notifications) open once the user is past splash/sign-in.
    LaunchedEffect(pendingDeepLink, destination) {
        val link = pendingDeepLink ?: return@LaunchedEffect
        if (destination != null && !destination.isAuth()) {
            pendingDeepLink = null
            if (TopLevelDestinations.any { it.route == link }) nav.navigateTopLevel(link) else nav.navigate(link)
        }
    }

    val openDrawer: () -> Unit = { scope.launch { drawer.open() } }
    ModalNavigationDrawer(
        drawerState = drawer,
        gesturesEnabled = destination.isTopLevel(),
        drawerContent = {
            ModalDrawerSheet {
                Column(Modifier.verticalScroll(rememberScrollState()).padding(vertical = 12.dp)) {
                    Text("DevInterview", style = MaterialTheme.typography.titleLarge, modifier = Modifier.padding(horizontal = 28.dp, vertical = 16.dp))
                    drawerItems.forEach { item ->
                        NavigationDrawerItem(
                            label = { Text(item.label) }, icon = { Icon(item.icon, null) }, selected = false,
                            onClick = { scope.launch { drawer.close() }; nav.navigate(item.route) { launchSingleTop = true } },
                            modifier = Modifier.padding(horizontal = 12.dp),
                        )
                    }
                }
            }
        },
    ) {
        Scaffold(
            snackbarHost = { SnackbarHost(snackbar) },
            contentWindowInsets = WindowInsets(0),
            bottomBar = {
                AnimatedVisibility(destination.isTopLevel()) {
                    NavigationBar(Modifier.testTag("bottom_bar")) {
                        TopLevelDestinations.forEach { top ->
                            val selected = destination?.hierarchy?.any { it.hasRoute(top.routeClass) } == true
                            NavigationBarItem(selected = selected, onClick = { nav.navigateTopLevel(top.route) },
                                icon = { Icon(top.icon, null) }, label = { Text(top.label) }, modifier = Modifier.testTag("tab_${top.label}"))
                        }
                    }
                }
            },
        ) { padding ->
            Column(Modifier.padding(padding).fillMaxSize()) {
                Box(Modifier.weight(1f)) {
                    NavHost(nav, startDestination = Routes.Splash) {
                        authGraph(nav)
                        dashboardGraph(nav, openDrawer)
                        questionsGraph(nav)
                        quizGraph(nav)
                        codingGraph(nav)
                        interviewGraph(nav, openDrawer)
                        learningGraph(nav, openDrawer)
                        flashcardsGraph(nav)
                        analyticsGraph(nav)
                        profileGraph(nav, openDrawer)
                        settingsGraph(nav)
                    }
                }
                val state = sync
                if (state != null && !destination.isAuth()) {
                    OfflineBanner(state.isOnline, state.pendingChanges, if (destination.isTopLevel()) Modifier else Modifier.navigationBarsPadding())
                }
            }
        }
    }
}
