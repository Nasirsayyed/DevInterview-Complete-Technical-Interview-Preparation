package com.devinterview.feature.dashboard

import androidx.lifecycle.SavedStateHandle
import com.devinterview.core.testing.FakeDashboardRepository
import com.devinterview.core.testing.MainDispatcherRule
import com.google.common.truth.Truth.assertThat
import kotlinx.coroutines.test.advanceTimeBy
import kotlinx.coroutines.test.runCurrent
import kotlinx.coroutines.test.runTest
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner

@RunWith(RobolectricTestRunner::class)
class SearchViewModelTest {
    @get:Rule val main = MainDispatcherRule()

    @Test
    fun `search is debounced and ignores very short queries`() = runTest(main.dispatcher) {
        val repo = FakeDashboardRepository()
        val vm = SearchViewModel(repo, SavedStateHandle(mapOf("query" to "")))
        vm.onQuery("s")
        advanceTimeBy(500); runCurrent()
        assertThat(repo.searches).isEmpty()

        vm.onQuery("sq"); vm.onQuery("sql")
        advanceTimeBy(100); runCurrent()
        assertThat(repo.searches).isEmpty()
        advanceTimeBy(400); runCurrent()
        assertThat(repo.searches).containsExactly("sql")
        assertThat(vm.state.value.results?.hits?.items?.single()?.title).isEqualTo("Result for sql")
    }

    @Test
    fun `changing the type filter searches again`() = runTest(main.dispatcher) {
        val repo = FakeDashboardRepository()
        val vm = SearchViewModel(repo, SavedStateHandle(mapOf("query" to "linq")))
        advanceTimeBy(400); runCurrent()
        vm.onType("question")
        advanceTimeBy(400); runCurrent()
        assertThat(repo.searches).containsExactly("linq", "linq")
    }
}
