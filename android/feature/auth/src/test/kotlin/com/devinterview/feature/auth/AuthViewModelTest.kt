package com.devinterview.feature.auth

import app.cash.turbine.test
import com.devinterview.core.testing.FakeAuthRepository
import com.devinterview.core.testing.FakeQuestionRepository
import com.devinterview.core.testing.FakeSettingsRepository
import com.devinterview.core.testing.MainDispatcherRule
import com.devinterview.core.testing.TestData
import com.devinterview.domain.model.AppError
import com.devinterview.domain.model.AppResult
import com.google.common.truth.Truth.assertThat
import kotlinx.coroutines.flow.filterNotNull
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.test.runTest
import org.junit.Rule
import org.junit.Test

class AuthViewModelTest {
    @get:Rule val main = MainDispatcherRule()

    @Test
    fun `splash routes to onboarding, login or home`() = runTest {
        val auth = FakeAuthRepository()
        assertThat(SplashViewModel(auth, FakeSettingsRepository(onboarded = false)).destination.filterNotNull().first()).isEqualTo(SplashViewModel.Destination.Onboarding)
        assertThat(SplashViewModel(auth, FakeSettingsRepository()).destination.filterNotNull().first()).isEqualTo(SplashViewModel.Destination.Login)
        auth.user.value = TestData.user()
        assertThat(SplashViewModel(auth, FakeSettingsRepository()).destination.filterNotNull().first()).isEqualTo(SplashViewModel.Destination.Home)
    }

    @Test
    fun `login validates input before calling the server`() = runTest {
        val auth = FakeAuthRepository()
        val vm = LoginViewModel(auth)
        vm.onEmail("not-an-email")
        vm.login()
        assertThat(vm.state.value.emailError).isNotNull()
        assertThat(vm.state.value.passwordError).isNotNull()
        assertThat(auth.loginCalls).isEmpty()
    }

    @Test
    fun `successful login emits SignedIn`() = runTest {
        val auth = FakeAuthRepository()
        val vm = LoginViewModel(auth)
        vm.events.test {
            vm.onEmail("dev@example.com"); vm.onPassword("Secret123")
            vm.login()
            assertThat(awaitItem()).isEqualTo(AuthEvent.SignedIn)
        }
        assertThat(auth.loginCalls).containsExactly("dev@example.com" to "Secret123")
        assertThat(vm.state.value.loading).isFalse()
    }

    @Test
    fun `unverified email resends a code and opens verification`() = runTest {
        val auth = FakeAuthRepository().apply { loginResult = AppResult.Failure(AppError.Server("email_not_verified", "Verify first", 403)) }
        val vm = LoginViewModel(auth)
        vm.events.test {
            vm.onEmail(" dev@example.com "); vm.onPassword("Secret123")
            vm.login()
            assertThat(awaitItem()).isEqualTo(AuthEvent.VerifyEmail("dev@example.com"))
        }
        assertThat(auth.resentTo).hasSize(1)
    }

    @Test
    fun `wrong password shows server message`() = runTest {
        val auth = FakeAuthRepository().apply { loginResult = AppResult.Failure(AppError.Server("invalid_credentials", "Invalid email or password.", 401)) }
        val vm = LoginViewModel(auth)
        vm.onEmail("dev@example.com"); vm.onPassword("nope")
        vm.login()
        assertThat(vm.state.value.error).isEqualTo("Invalid email or password.")
    }

    @Test
    fun `registration cannot leave step one with invalid fields`() = runTest {
        val vm = RegisterViewModel(FakeAuthRepository(), FakeQuestionRepository())
        vm.update { it.copy(firstName = "Ada", lastName = "L", email = "ada@example.com", password = "short", confirmPassword = "short") }
        vm.next()
        assertThat(vm.state.value.step).isEqualTo(0)
        assertThat(vm.state.value.errors).containsKey("password")
    }

    @Test
    fun `registration submits selected technologies and asks for verification`() = runTest {
        val auth = FakeAuthRepository()
        val vm = RegisterViewModel(auth, FakeQuestionRepository())
        assertThat(vm.state.value.technologies).isNotEmpty()
        vm.update { it.copy(firstName = "Ada", lastName = "Lovelace", email = "ada@example.com", password = "Secret123", confirmPassword = "Secret123") }
        vm.next()
        assertThat(vm.state.value.step).isEqualTo(1)
        vm.toggleTechnology("t1")
        vm.events.test {
            vm.next()
            assertThat(awaitItem()).isEqualTo(AuthEvent.VerifyEmail("ada@example.com"))
        }
        assertThat(auth.registeredForm?.preferredTechnologyIds).containsExactly("t1")
    }

    @Test
    fun `server field errors on registration return to the first step`() = runTest {
        val auth = FakeAuthRepository().apply { registerResult = AppResult.Failure(AppError.Validation("Invalid", mapOf("email" to listOf("Email already registered.")))) }
        val vm = RegisterViewModel(auth, FakeQuestionRepository())
        vm.update { it.copy(firstName = "Ada", lastName = "Lovelace", email = "ada@example.com", password = "Secret123", confirmPassword = "Secret123") }
        vm.next(); vm.next()
        assertThat(vm.state.value.step).isEqualTo(0)
        assertThat(vm.state.value.errors["email"]).isEqualTo("Email already registered.")
    }

    @Test
    fun `verification code accepts only six digits`() = runTest {
        val vm = VerifyEmailViewModel(FakeAuthRepository())
        vm.onCode("12a34567")
        assertThat(vm.state.value.code).isEqualTo("123456")
    }
}
