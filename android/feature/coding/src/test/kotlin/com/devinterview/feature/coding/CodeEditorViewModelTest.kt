package com.devinterview.feature.coding

import androidx.lifecycle.SavedStateHandle
import com.devinterview.core.testing.FakeCodingRepository
import com.devinterview.core.testing.MainDispatcherRule
import com.devinterview.core.testing.TestData
import com.devinterview.domain.model.AppError
import com.devinterview.domain.model.AppResult
import com.google.common.truth.Truth.assertThat
import kotlinx.coroutines.test.advanceTimeBy
import kotlinx.coroutines.test.runTest
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner

@RunWith(RobolectricTestRunner::class)
class CodeEditorViewModelTest {
    @get:Rule val main = MainDispatcherRule()

    private fun vm(repo: FakeCodingRepository) = CodeEditorViewModel(repo, SavedStateHandle(mapOf("challengeId" to "ch1")))

    @Test
    fun `editor starts with starter code for the first allowed language`() = runTest {
        val vm = vm(FakeCodingRepository())
        assertThat(vm.state.value.language).isEqualTo("python")
        assertThat(vm.state.value.code).isEqualTo(TestData.challenge().starterCode["python"])
    }

    @Test
    fun `saved draft wins over starter code`() = runTest {
        val repo = FakeCodingRepository().apply { drafts["ch1/python"] = "print(42)" }
        assertThat(vm(repo).state.value.code).isEqualTo("print(42)")
    }

    @Test
    fun `switching language saves the draft and loads that language`() = runTest {
        val repo = FakeCodingRepository()
        val vm = vm(repo)
        vm.onCode("print('mine')")
        vm.selectLanguage("csharp")
        assertThat(repo.drafts["ch1/python"]).isEqualTo("print('mine')")
        assertThat(vm.state.value.code).isEqualTo("class Solution {}")
    }

    @Test
    fun `retrying a failed submit reuses the idempotency key`() = runTest(main.dispatcher) {
        val repo = FakeCodingRepository().apply { submitResults.add(AppResult.Failure(AppError.Timeout())) }
        val vm = vm(repo)
        vm.onCode("print(1)")
        vm.submit()
        assertThat(vm.state.value.error).isInstanceOf(AppError.Timeout::class.java)
        vm.submit()
        assertThat(repo.submitKeys).hasSize(2)
        assertThat(repo.submitKeys[0]).isEqualTo(repo.submitKeys[1])
        assertThat(vm.state.value.submission?.status).isEqualTo("Accepted")
        // A later, different submission gets a fresh key.
        vm.onCode("print(2)")
        vm.submit()
        assertThat(repo.submitKeys[2]).isNotEqualTo(repo.submitKeys[0])
        advanceTimeBy(1000)
    }

    @Test
    fun `run shows sandbox results without creating a submission`() = runTest {
        val repo = FakeCodingRepository()
        val vm = vm(repo)
        vm.run()
        assertThat(vm.state.value.runResult?.passedCount).isEqualTo(2)
        assertThat(vm.state.value.submission).isNull()
        assertThat(repo.submitKeys).isEmpty()
    }

    @Test
    fun `status labels are human readable`() {
        assertThat(submissionStatusLabel("TimeLimitExceeded")).isEqualTo("Time limit exceeded")
        assertThat(submissionStatusLabel("WrongAnswer")).isEqualTo("Wrong answer")
        assertThat("DynamicProgramming".humanize()).isEqualTo("Dynamic Programming")
    }
}
