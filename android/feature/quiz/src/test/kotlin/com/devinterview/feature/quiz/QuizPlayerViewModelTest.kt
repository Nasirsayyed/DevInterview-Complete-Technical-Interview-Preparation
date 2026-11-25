package com.devinterview.feature.quiz

import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.viewModelScope
import kotlinx.coroutines.cancel
import app.cash.turbine.test
import com.devinterview.core.designsystem.Loadable
import com.devinterview.core.testing.FakeQuizRepository
import com.devinterview.core.testing.MainDispatcherRule
import com.devinterview.core.testing.TestData
import com.devinterview.domain.model.AppError
import com.devinterview.domain.model.AppResult
import com.devinterview.domain.usecase.SubmitQuizUseCase
import com.google.common.truth.Truth.assertThat
import com.google.common.collect.Range
import kotlinx.coroutines.test.runTest
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import java.time.Instant

@RunWith(RobolectricTestRunner::class)
class QuizPlayerViewModelTest {
    @get:Rule val main = MainDispatcherRule()

    private fun vm(repo: FakeQuizRepository) = QuizPlayerViewModel(repo, SubmitQuizUseCase(repo), SavedStateHandle(mapOf("quizId" to "quiz1")))

    @Test
    fun `selecting an option records and persists the answer`() = runTest {
        val repo = FakeQuizRepository()
        val vm = vm(repo)
        val q = (vm.state.value.attempt as Loadable.Ready).data.questions[0]
        vm.toggleOption(q, "q0-b")
        assertThat(vm.state.value.answers["q0"]?.selectedOptionIds).containsExactly("q0-b")
        assertThat(repo.saved.last().selectedOptionIds).containsExactly("q0-b")
        // Single-choice: a new selection replaces the previous one.
        vm.toggleOption(q, "q0-a")
        assertThat(vm.state.value.answers["q0"]?.selectedOptionIds).containsExactly("q0-a")
    }

    @Test
    fun `progress counts answered and marked questions`() = runTest {
        val vm = vm(FakeQuizRepository())
        val questions = (vm.state.value.attempt as Loadable.Ready).data.questions
        vm.toggleOption(questions[0], "q0-a")
        vm.toggleMark(questions[1])
        val p = vm.state.value.progress!!
        assertThat(p.answered).isEqualTo(1)
        assertThat(p.markedForReview).isEqualTo(1)
        assertThat(p.unanswered).isEqualTo(2)
    }

    @Test
    fun `navigation stays within bounds`() = runTest {
        val vm = vm(FakeQuizRepository())
        vm.goTo(-1); assertThat(vm.state.value.index).isEqualTo(0)
        vm.goTo(2); assertThat(vm.state.value.index).isEqualTo(2)
        vm.goTo(3); assertThat(vm.state.value.index).isEqualTo(2)
    }

    @Test
    fun `submit sends all answers and opens the result`() = runTest {
        val repo = FakeQuizRepository()
        val vm = vm(repo)
        val questions = (vm.state.value.attempt as Loadable.Ready).data.questions
        vm.toggleOption(questions[0], "q0-a")
        vm.events.test {
            vm.requestSubmit()
            assertThat(vm.state.value.showSubmitDialog).isTrue()
            vm.submit()
            assertThat(awaitItem()).isEqualTo(QuizEvent.OpenResult("a1"))
        }
        assertThat(repo.submitted.single().map { it.questionId }).containsExactly("q0", "q1", "q2").inOrder()
    }

    @Test
    fun `failed manual submit keeps the player open with an error`() = runTest {
        val repo = FakeQuizRepository().apply { submitResult = AppResult.Failure(AppError.Network()) }
        val vm = vm(repo)
        vm.submit()
        assertThat(vm.state.value.error).isNotNull()
        assertThat(vm.state.value.submitting).isFalse()
    }

    @Test
    fun `quiz is auto-submitted when the server deadline has passed`() = runTest {
        val now = Instant.now().toString()
        val repo = FakeQuizRepository().apply { startResult = AppResult.Success(TestData.quizAttempt(expiresAt = now, serverNow = now)) }
        val vm = vm(repo)
        vm.events.test {
            assertThat(awaitItem()).isEqualTo(QuizEvent.OpenResult("a1"))
        }
        assertThat(repo.submitted).hasSize(1)
        assertThat(vm.state.value.remainingSeconds).isEqualTo(0)
    }

    @Test
    fun `timer uses the server clock, not the device clock`() = runTest {
        // Server says it is 10:00 and the attempt ends at 10:10; the device clock is irrelevant.
        val serverNow = Instant.now()
        val repo = FakeQuizRepository().apply {
            startResult = AppResult.Success(TestData.quizAttempt(expiresAt = serverNow.plusSeconds(600).toString(), serverNow = serverNow.toString()))
        }
        val vm = vm(repo)
        assertThat(vm.state.value.remainingSeconds).isIn(Range.closed(598L, 600L))
        vm.viewModelScope.cancel() // stop the countdown so runTest can finish
    }
}
