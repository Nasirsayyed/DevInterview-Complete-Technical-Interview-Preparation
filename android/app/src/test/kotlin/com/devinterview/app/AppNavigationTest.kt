package com.devinterview.app

import androidx.compose.material3.Text
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.navigation.NavDestination.Companion.hasRoute
import androidx.navigation.compose.ComposeNavigator
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.testing.TestNavHostController
import com.devinterview.app.ui.isAuth
import com.devinterview.app.ui.isTopLevel
import com.devinterview.core.navigation.DeepLinks
import com.devinterview.core.navigation.Routes
import com.devinterview.core.navigation.clearAll
import com.devinterview.core.navigation.navigateTopLevel
import com.google.common.truth.Truth.assertThat
import org.junit.Before
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.annotation.Config

@RunWith(RobolectricTestRunner::class)
// Plain Application: navigation logic is tested without starting the Hilt graph.
@Config(sdk = [35], application = android.app.Application::class)
class AppNavigationTest {
    @get:Rule val compose = createComposeRule()
    private lateinit var nav: TestNavHostController

    @Before
    fun setUp() {
        compose.setContent {
            nav = TestNavHostController(LocalContext.current).apply { navigatorProvider.addNavigator(ComposeNavigator()) }
            NavHost(nav, startDestination = Routes.Login) {
                composable<Routes.Login> { Text("login") }
                composable<Routes.Home> { Text("home") }
                composable<Routes.Learn> { Text("learn") }
                composable<Routes.Practice> { Text("practice") }
                composable<Routes.QuestionDetail> { Text("question") }
                composable<Routes.ChallengeDetail> { Text("challenge") }
            }
        }
    }

    @Test
    fun signInClearsAuthScreensFromTheBackStack() {
        compose.runOnIdle {
            assertThat(nav.currentDestination.isAuth()).isTrue()
            nav.navigate(Routes.Home) { clearAll(nav) }
            assertThat(nav.currentDestination!!.hasRoute(Routes.Home::class)).isTrue()
            assertThat(nav.currentDestination.isTopLevel()).isTrue()
            assertThat(nav.previousBackStackEntry).isNull()
        }
    }

    @Test
    fun detailScreensHideTheBottomBar() {
        compose.runOnIdle {
            nav.navigate(Routes.Home) { clearAll(nav) }
            nav.navigate(Routes.QuestionDetail("q1"))
            assertThat(nav.currentDestination.isTopLevel()).isFalse()
            assertThat(nav.currentDestination.isAuth()).isFalse()
        }
    }

    @Test
    fun switchingTabsDoesNotGrowTheBackStack() {
        compose.runOnIdle {
            nav.navigate(Routes.Home) { clearAll(nav) }
            repeat(3) { nav.navigateTopLevel(Routes.Learn); nav.navigateTopLevel(Routes.Practice) }
            assertThat(nav.currentDestination!!.hasRoute(Routes.Practice::class)).isTrue()
            assertThat(nav.currentBackStack.value.count { it.destination.hasRoute(Routes.Learn::class) || it.destination.hasRoute(Routes.Practice::class) }).isEqualTo(1)
        }
    }

    @Test
    fun notificationDeepLinkOpensTheTarget() {
        compose.runOnIdle {
            nav.navigate(Routes.Home) { clearAll(nav) }
            nav.navigate(DeepLinks.toRoute("devinterview://challenge/abc")!!)
            assertThat(nav.currentDestination!!.hasRoute(Routes.ChallengeDetail::class)).isTrue()
        }
    }
}
