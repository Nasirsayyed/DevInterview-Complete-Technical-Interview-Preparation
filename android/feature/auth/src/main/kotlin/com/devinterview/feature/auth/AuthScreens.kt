package com.devinterview.feature.auth

import android.content.Context
import androidx.compose.animation.AnimatedContent
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.pager.HorizontalPager
import androidx.compose.foundation.pager.rememberPagerState
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.credentials.CredentialManager
import androidx.credentials.GetCredentialRequest
import androidx.credentials.exceptions.GetCredentialCancellationException
import androidx.credentials.exceptions.GetCredentialException
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.navigation.NavController
import androidx.navigation.NavGraphBuilder
import androidx.navigation.compose.composable
import androidx.navigation.toRoute
import com.devinterview.core.designsystem.*
import com.devinterview.core.navigation.Routes
import com.devinterview.core.navigation.clearAll
import com.devinterview.domain.usecase.FormValidator
import com.google.android.libraries.identity.googleid.GetGoogleIdOption
import com.google.android.libraries.identity.googleid.GoogleIdTokenCredential
import kotlinx.coroutines.launch

fun NavGraphBuilder.authGraph(nav: NavController) {
    composable<Routes.Splash> {
        SplashScreen { destination ->
            val route: Any = when (destination) {
                SplashViewModel.Destination.Onboarding -> Routes.Onboarding
                SplashViewModel.Destination.Login -> Routes.Login
                SplashViewModel.Destination.Home -> Routes.Home
            }
            nav.navigate(route) { clearAll(nav) }
        }
    }
    composable<Routes.Onboarding> { OnboardingScreen(onFinished = { nav.navigate(Routes.Login) { clearAll(nav) } }) }
    composable<Routes.Login> {
        LoginScreen(
            onSignedIn = { nav.navigate(Routes.Home) { clearAll(nav) } },
            onRegister = { nav.navigate(Routes.Register) },
            onForgot = { nav.navigate(Routes.ForgotPassword) },
            onVerify = { nav.navigate(Routes.VerifyEmail(it)) },
        )
    }
    composable<Routes.Register> {
        RegisterScreen(
            onBack = { nav.popBackStack() },
            onVerify = { nav.navigate(Routes.VerifyEmail(it)) },
            onSignedIn = { nav.navigate(Routes.Home) { clearAll(nav) } },
        )
    }
    composable<Routes.VerifyEmail> { entry ->
        VerifyEmailScreen(entry.toRoute<Routes.VerifyEmail>().email, onBack = { nav.popBackStack() }, onVerified = { nav.navigate(Routes.Home) { clearAll(nav) } })
    }
    composable<Routes.ForgotPassword> { ForgotPasswordScreen(onBack = { nav.popBackStack() }, onDone = { nav.popBackStack(Routes.Login, inclusive = false) }) }
}

@Composable
fun SplashScreen(viewModel: SplashViewModel = hiltViewModel(), onDestination: (SplashViewModel.Destination) -> Unit) {
    val destination by viewModel.destination.collectAsStateWithLifecycle()
    LaunchedEffect(destination) { destination?.let(onDestination) }
    Box(Modifier.fillMaxSize().background(DiTheme.colors.heroGradient), contentAlignment = Alignment.Center) {
        Column(horizontalAlignment = Alignment.CenterHorizontally) {
            Icon(Icons.Filled.Code, contentDescription = null, tint = Color.White, modifier = Modifier.size(72.dp))
            Spacer(Modifier.height(12.dp))
            Text("DevInterview", style = MaterialTheme.typography.displaySmall, color = Color.White)
            Text("Prepare Smart. Practice Hard. Get Hired.", style = MaterialTheme.typography.bodyMedium, color = Color.White.copy(alpha = 0.85f))
        }
    }
}

private data class OnboardingPage(val icon: androidx.compose.ui.graphics.vector.ImageVector, val title: String, val body: String)

@Composable
fun OnboardingScreen(viewModel: OnboardingViewModel = hiltViewModel(), onFinished: () -> Unit) {
    val pages = listOf(
        OnboardingPage(Icons.Filled.MenuBook, "Learn with structure", "Roadmaps for C#, .NET, SQL, React, DSA and system design — with lessons, questions and quizzes."),
        OnboardingPage(Icons.Filled.Terminal, "Practise for real", "Hundreds of questions, timed quizzes and coding challenges executed in a secure sandbox."),
        OnboardingPage(Icons.Filled.RecordVoiceOver, "Interview with AI", "Adaptive mock interviews, answer evaluation and code review — with honest, detailed feedback."),
        OnboardingPage(Icons.Filled.Insights, "Track your growth", "Analytics, streaks and achievements show exactly where to focus next."),
    )
    val pager = rememberPagerState { pages.size }
    val scope = rememberCoroutineScope()
    Column(Modifier.fillMaxSize().background(MaterialTheme.colorScheme.background).systemBarsPadding().padding(24.dp)) {
        Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.End) { TextButton(onClick = { viewModel.complete(onFinished) }) { Text("Skip") } }
        HorizontalPager(pager, Modifier.weight(1f)) { index ->
            val page = pages[index]
            Column(Modifier.fillMaxSize(), horizontalAlignment = Alignment.CenterHorizontally, verticalArrangement = Arrangement.Center) {
                Box(Modifier.size(140.dp).clip(CircleShape).background(DiTheme.colors.heroGradient), contentAlignment = Alignment.Center) {
                    Icon(page.icon, contentDescription = null, tint = Color.White, modifier = Modifier.size(64.dp))
                }
                Spacer(Modifier.height(32.dp))
                Text(page.title, style = MaterialTheme.typography.headlineMedium, textAlign = TextAlign.Center)
                Spacer(Modifier.height(12.dp))
                Text(page.body, style = MaterialTheme.typography.bodyLarge, textAlign = TextAlign.Center, color = MaterialTheme.colorScheme.onSurfaceVariant)
            }
        }
        Row(Modifier.fillMaxWidth().padding(vertical = 16.dp), horizontalArrangement = Arrangement.Center) {
            repeat(pages.size) { i ->
                Box(Modifier.padding(4.dp).size(if (i == pager.currentPage) 24.dp else 8.dp, 8.dp).clip(CircleShape)
                    .background(if (i == pager.currentPage) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.outlineVariant))
            }
        }
        val last = pager.currentPage == pages.lastIndex
        PrimaryButton(if (last) "Get started" else "Next", onClick = {
            if (last) viewModel.complete(onFinished) else scope.launch { pager.animateScrollToPage(pager.currentPage + 1) }
        }, modifier = Modifier.fillMaxWidth())
    }
}

/** Requests a Google ID token through Credential Manager. Returns null when the user cancels. */
private suspend fun requestGoogleIdToken(context: Context, serverClientId: String): Result<String?> = runCatching {
    val option = GetGoogleIdOption.Builder().setServerClientId(serverClientId).setFilterByAuthorizedAccounts(false).build()
    val response = CredentialManager.create(context).getCredential(context, GetCredentialRequest.Builder().addCredentialOption(option).build())
    GoogleIdTokenCredential.createFrom(response.credential.data).idToken
}.recoverCatching { if (it is GetCredentialCancellationException) null else throw it }

@Composable
fun LoginScreen(viewModel: LoginViewModel = hiltViewModel(), onSignedIn: () -> Unit, onRegister: () -> Unit, onForgot: () -> Unit, onVerify: (String) -> Unit) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    val context = LocalContext.current
    val scope = rememberCoroutineScope()
    LaunchedEffect(Unit) {
        viewModel.events.collect { e -> when (e) { AuthEvent.SignedIn -> onSignedIn(); is AuthEvent.VerifyEmail -> onVerify(e.email); else -> Unit } }
    }
    Column(Modifier.fillMaxSize().background(MaterialTheme.colorScheme.background).systemBarsPadding().imePadding().verticalScroll(rememberScrollState()).padding(24.dp)) {
        Spacer(Modifier.height(32.dp))
        Text("Welcome back", style = MaterialTheme.typography.headlineMedium)
        Text("Sign in to continue your preparation.", style = MaterialTheme.typography.bodyLarge, color = MaterialTheme.colorScheme.onSurfaceVariant)
        Spacer(Modifier.height(28.dp))
        DiTextField(state.email, viewModel::onEmail, "Email", error = state.emailError, keyboardType = KeyboardType.Email, leadingIcon = Icons.Filled.Email,
            modifier = Modifier.testTag("login_email"))
        Spacer(Modifier.height(12.dp))
        DiTextField(state.password, viewModel::onPassword, "Password", error = state.passwordError, isPassword = true, imeAction = ImeAction.Done,
            leadingIcon = Icons.Filled.Lock, onImeAction = viewModel::login, modifier = Modifier.testTag("login_password"))
        TextButton(onClick = onForgot, modifier = Modifier.align(Alignment.End)) { Text("Forgot password?") }
        state.error?.let { Text(it, color = MaterialTheme.colorScheme.error, style = MaterialTheme.typography.bodyMedium, modifier = Modifier.padding(bottom = 8.dp).testTag("login_error")) }
        PrimaryButton("Sign in", onClick = viewModel::login, loading = state.loading, modifier = Modifier.fillMaxWidth().testTag("login_submit"))
        if (BuildConfig.GOOGLE_WEB_CLIENT_ID.isNotBlank()) {
            Spacer(Modifier.height(12.dp))
            SecondaryButton("Continue with Google", icon = Icons.Filled.AccountCircle, modifier = Modifier.fillMaxWidth(), onClick = {
                scope.launch {
                    requestGoogleIdToken(context, BuildConfig.GOOGLE_WEB_CLIENT_ID)
                        .onSuccess { token -> token?.let(viewModel::googleSignIn) }
                        .onFailure { viewModel.googleFailed(if (it is GetCredentialException) "Google sign-in is not available on this device." else "Google sign-in failed.") }
                }
            })
        }
        Spacer(Modifier.height(24.dp))
        Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.Center, verticalAlignment = Alignment.CenterVertically) {
            Text("New to DevInterview?", style = MaterialTheme.typography.bodyMedium)
            TextButton(onClick = onRegister) { Text("Create an account") }
        }
    }
}

@OptIn(ExperimentalLayoutApi::class)
@Composable
fun RegisterScreen(viewModel: RegisterViewModel = hiltViewModel(), onBack: () -> Unit, onVerify: (String) -> Unit, onSignedIn: () -> Unit) {
    val s by viewModel.state.collectAsStateWithLifecycle()
    LaunchedEffect(Unit) { viewModel.events.collect { e -> when (e) { is AuthEvent.VerifyEmail -> onVerify(e.email); AuthEvent.SignedIn -> onSignedIn(); else -> Unit } } }
    Scaffold(topBar = { DiTopBar(if (s.step == 0) "Create account" else "Your goals", onBack = if (s.step == 0) onBack else viewModel::back) }) { padding ->
        Column(Modifier.padding(padding).imePadding().verticalScroll(rememberScrollState()).padding(horizontal = 24.dp, vertical = 8.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
            AnimatedProgressBar((s.step + 1) / 2f)
            AnimatedContent(s.step, label = "step") { step ->
                Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
                    if (step == 0) {
                        Row(horizontalArrangement = Arrangement.spacedBy(12.dp)) {
                            DiTextField(s.firstName, { v -> viewModel.update { it.copy(firstName = v) } }, "First name", error = s.errors["firstName"], modifier = Modifier.weight(1f))
                            DiTextField(s.lastName, { v -> viewModel.update { it.copy(lastName = v) } }, "Last name", error = s.errors["lastName"], modifier = Modifier.weight(1f))
                        }
                        DiTextField(s.email, { v -> viewModel.update { it.copy(email = v) } }, "Email", error = s.errors["email"], keyboardType = KeyboardType.Email)
                        DiTextField(s.password, { v -> viewModel.update { it.copy(password = v) } }, "Password", error = s.errors["password"], isPassword = true,
                            supportingText = "8+ characters with upper and lower case letters and a digit.")
                        DiTextField(s.confirmPassword, { v -> viewModel.update { it.copy(confirmPassword = v) } }, "Confirm password", error = s.errors["confirmPassword"], isPassword = true, imeAction = ImeAction.Done)
                    } else {
                        DropdownField("Experience level", FormValidator.EXPERIENCE_LEVELS, s.experienceLevel, { v -> viewModel.update { it.copy(experienceLevel = v) } }, Modifier.fillMaxWidth())
                        DiTextField(s.currentRole, { v -> viewModel.update { it.copy(currentRole = v) } }, "Current job role (optional)")
                        DiTextField(s.targetRole, { v -> viewModel.update { it.copy(targetRole = v) } }, "Target job role (optional)", imeAction = ImeAction.Done)
                        if (s.technologies.isNotEmpty()) {
                            Text("Preferred technologies", style = MaterialTheme.typography.titleSmall)
                            FlowRow(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                                s.technologies.forEach { t ->
                                    FilterChip(selected = t.id in s.selectedTechnologies, onClick = { viewModel.toggleTechnology(t.id) }, label = { Text(t.name) })
                                }
                            }
                        } else {
                            Text("You can choose preferred technologies later in your profile.", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                        }
                    }
                }
            }
            s.error?.let { Text(it, color = MaterialTheme.colorScheme.error) }
            PrimaryButton(if (s.step == 0) "Continue" else "Create account", onClick = viewModel::next, loading = s.loading, modifier = Modifier.fillMaxWidth())
        }
    }
}

@Composable
fun VerifyEmailScreen(email: String, viewModel: VerifyEmailViewModel = hiltViewModel(), onBack: () -> Unit, onVerified: () -> Unit) {
    val s by viewModel.state.collectAsStateWithLifecycle()
    LaunchedEffect(Unit) { viewModel.events.collect { if (it == AuthEvent.SignedIn) onVerified() } }
    Scaffold(topBar = { DiTopBar("Verify your email", onBack = onBack) }) { padding ->
        Column(Modifier.padding(padding).padding(24.dp), verticalArrangement = Arrangement.spacedBy(16.dp)) {
            Icon(Icons.Filled.MarkEmailRead, contentDescription = null, modifier = Modifier.size(56.dp), tint = MaterialTheme.colorScheme.primary)
            Text("We sent a 6-digit code to $email. Enter it below to activate your account.", style = MaterialTheme.typography.bodyLarge)
            DiTextField(s.code, viewModel::onCode, "Verification code", error = s.error, keyboardType = KeyboardType.NumberPassword, imeAction = ImeAction.Done,
                onImeAction = { viewModel.verify(email) })
            s.info?.let { Text(it, color = DiTheme.colors.success) }
            PrimaryButton("Verify", onClick = { viewModel.verify(email) }, loading = s.loading, modifier = Modifier.fillMaxWidth())
            TextButton(onClick = { viewModel.resend(email) }) { Text("Resend code") }
        }
    }
}

@Composable
fun ForgotPasswordScreen(viewModel: ForgotPasswordViewModel = hiltViewModel(), onBack: () -> Unit, onDone: () -> Unit) {
    val s by viewModel.state.collectAsStateWithLifecycle()
    val snackbar = remember { SnackbarHostState() }
    LaunchedEffect(Unit) { viewModel.events.collect { if (it is AuthEvent.Message) { snackbar.showSnackbar(it.text); onDone() } } }
    Scaffold(topBar = { DiTopBar("Reset password", onBack = onBack) }, snackbarHost = { SnackbarHost(snackbar) }) { padding ->
        Column(Modifier.padding(padding).imePadding().verticalScroll(rememberScrollState()).padding(24.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
            Text(if (!s.codeSent) "Enter your account email and we'll send you a reset code." else "Enter the code from your email and choose a new password.",
                style = MaterialTheme.typography.bodyLarge)
            DiTextField(s.email, { v -> viewModel.update { it.copy(email = v) } }, "Email", error = s.errors["email"], keyboardType = KeyboardType.Email, enabled = !s.codeSent)
            if (s.codeSent) {
                DiTextField(s.code, { v -> viewModel.update { it.copy(code = v.filter(Char::isDigit).take(6)) } }, "6-digit code", error = s.errors["code"], keyboardType = KeyboardType.NumberPassword)
                DiTextField(s.password, { v -> viewModel.update { it.copy(password = v) } }, "New password", error = s.errors["password"], isPassword = true)
                DiTextField(s.confirm, { v -> viewModel.update { it.copy(confirm = v) } }, "Confirm new password", error = s.errors["confirm"], isPassword = true, imeAction = ImeAction.Done)
            }
            s.error?.let { Text(it, color = MaterialTheme.colorScheme.error) }
            PrimaryButton(if (s.codeSent) "Reset password" else "Send code", onClick = if (s.codeSent) viewModel::reset else viewModel::sendCode,
                loading = s.loading, modifier = Modifier.fillMaxWidth())
            if (s.codeSent) TextButton(onClick = viewModel::sendCode) { Text("Send a new code") }
        }
    }
}
