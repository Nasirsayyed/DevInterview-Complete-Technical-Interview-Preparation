package com.devinterview.feature.auth

import androidx.compose.ui.test.*
import androidx.compose.ui.test.junit4.createComposeRule
import com.devinterview.core.designsystem.DevInterviewTheme
import com.devinterview.core.testing.FakeAuthRepository
import com.devinterview.domain.model.AppError
import com.devinterview.domain.model.AppResult
import com.google.common.truth.Truth.assertThat
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.annotation.Config

@RunWith(RobolectricTestRunner::class)
@Config(sdk = [35])
class LoginScreenTest {
    @get:Rule val compose = createComposeRule()

    private fun setContent(auth: FakeAuthRepository, onSignedIn: () -> Unit = {}, onVerify: (String) -> Unit = {}) {
        val vm = LoginViewModel(auth)
        compose.setContent {
            DevInterviewTheme { LoginScreen(vm, onSignedIn = onSignedIn, onRegister = {}, onForgot = {}, onVerify = onVerify) }
        }
    }

    @Test
    fun emptySubmitShowsValidationErrors() {
        val auth = FakeAuthRepository()
        setContent(auth)
        compose.onNodeWithTag("login_submit").performClick()
        compose.onNodeWithText("Email is required.").assertIsDisplayed()
        compose.onNodeWithText("Password is required.").assertIsDisplayed()
        assertThat(auth.loginCalls).isEmpty()
    }

    @Test
    fun validCredentialsSignIn() {
        var signedIn = false
        setContent(FakeAuthRepository(), onSignedIn = { signedIn = true })
        compose.onNodeWithTag("login_email").performTextInput("dev@example.com")
        compose.onNodeWithTag("login_password").performTextInput("Secret123")
        compose.onNodeWithTag("login_submit").performClick()
        compose.waitUntil(3_000) { signedIn }
    }

    @Test
    fun serverErrorIsShown() {
        val auth = FakeAuthRepository().apply { loginResult = AppResult.Failure(AppError.Server("invalid_credentials", "Invalid email or password.", 401)) }
        setContent(auth)
        compose.onNodeWithTag("login_email").performTextInput("dev@example.com")
        compose.onNodeWithTag("login_password").performTextInput("wrong")
        compose.onNodeWithTag("login_submit").performClick()
        compose.onNodeWithTag("login_error").assertTextEquals("Invalid email or password.")
    }
}
