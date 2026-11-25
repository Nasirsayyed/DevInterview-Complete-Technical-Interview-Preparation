package com.devinterview.feature.questions

import androidx.lifecycle.SavedStateHandle
import androidx.paging.testing.asSnapshot
import com.devinterview.core.designsystem.Loadable
import com.devinterview.core.testing.FakeLibraryRepository
import com.devinterview.core.testing.FakeQuestionRepository
import com.devinterview.core.testing.MainDispatcherRule
import com.devinterview.core.testing.TestData
import com.devinterview.domain.model.AppResult
import com.devinterview.domain.model.Page
import com.devinterview.domain.model.QuestionSummary
import com.google.common.truth.Truth.assertThat
import kotlinx.coroutines.test.runTest
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner

@RunWith(RobolectricTestRunner::class)
class QuestionDetailViewModelTest {
    @get:Rule val main = MainDispatcherRule()

    private fun vm(repo: FakeQuestionRepository = FakeQuestionRepository(), library: FakeLibraryRepository = FakeLibraryRepository()) =
        QuestionDetailViewModel(repo, library, SavedStateHandle(mapOf("questionId" to "q1")))

    @Test
    fun `correct answer is graded by the repository and reveals the explanation`() = runTest {
        val repo = FakeQuestionRepository()
        val vm = vm(repo)
        vm.toggleOption("o1")
        vm.submitAnswer()
        assertThat(repo.attempts.single().second).containsExactly("o1")
        assertThat(vm.state.value.result?.isCorrect).isTrue()
        assertThat(vm.state.value.answer?.shortAnswer).isNotEmpty()
        // Once answered, selections are locked.
        vm.toggleOption("o2")
        assertThat(vm.state.value.selected).containsExactly("o1")
    }

    @Test
    fun `single choice keeps one selection, multiple choice toggles`() = runTest {
        val single = vm()
        single.toggleOption("o1"); single.toggleOption("o2")
        assertThat(single.state.value.selected).containsExactly("o2")

        val repo = FakeQuestionRepository().apply { questionResults["q1"] = AppResult.Success(TestData.question(multiple = true)) }
        val multi = vm(repo)
        multi.toggleOption("o1"); multi.toggleOption("o2"); multi.toggleOption("o1")
        assertThat(multi.state.value.selected).containsExactly("o2")
    }

    @Test
    fun `open-ended questions use self assessment`() = runTest {
        val repo = FakeQuestionRepository().apply { questionResults["q1"] = AppResult.Success(TestData.question(objective = false)) }
        val vm = vm(repo)
        vm.revealAnswer()
        assertThat(vm.state.value.answer).isNotNull()
        vm.selfAssess(knewIt = true)
        assertThat(repo.attempts.single().third).isTrue()
    }

    @Test
    fun `bookmark and note are stored in the offline library`() = runTest {
        val library = FakeLibraryRepository()
        val vm = vm(library = library)
        vm.toggleBookmark()
        assertThat(library.bookmarks.value).containsKey("q1")
        assertThat(vm.isBookmarked.value).isTrue()
        vm.saveNote("  remember: sealed prevents inheritance ")
        assertThat(library.notes.value["q1"]?.content).isEqualTo("remember: sealed prevents inheritance")
        vm.saveNote("")
        assertThat(library.notes.value).doesNotContainKey("q1")
    }

    @Test
    fun `next question replaces the current one`() = runTest {
        val repo = FakeQuestionRepository().apply { questionResults["q2"] = AppResult.Success(TestData.question(id = "q2", next = null)) }
        val vm = vm(repo)
        vm.toggleOption("o1"); vm.submitAnswer()
        vm.goTo("q2")
        assertThat((vm.state.value.question as Loadable.Ready).data.id).isEqualTo("q2")
        assertThat(vm.state.value.result).isNull()
        assertThat(vm.state.value.selected).isEmpty()
    }

    @Test
    fun `question list pages through results with the current filter`() = runTest {
        fun summary(i: Int) = QuestionSummary("q$i", "Question $i", TestData.tech, TestData.category, "Beginner", "SingleChoice")
        val repo = FakeQuestionRepository().apply {
            pages[1] = AppResult.Success(Page((1..20).map(::summary), page = 1, hasNext = true))
            pages[2] = AppResult.Success(Page((21..25).map(::summary), page = 2, hasNext = false))
        }
        val vm = QuestionListViewModel(repo, SavedStateHandle(mapOf("technologyId" to "t1", "title" to "C#", "bookmarkedOnly" to false)))
        val items = vm.questions.asSnapshot { scrollTo(24) }
        assertThat(items).hasSize(25)
        assertThat(repo.requestedFilters.first().technologyId).isEqualTo("t1")
    }
}
