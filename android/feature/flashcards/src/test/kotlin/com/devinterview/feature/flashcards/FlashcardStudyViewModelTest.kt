package com.devinterview.feature.flashcards

import androidx.lifecycle.SavedStateHandle
import com.devinterview.core.designsystem.Loadable
import com.devinterview.core.testing.FakeFlashcardRepository
import com.devinterview.core.testing.MainDispatcherRule
import com.devinterview.core.testing.TestData
import com.devinterview.domain.model.FlashcardGrade
import com.google.common.truth.Truth.assertThat
import kotlinx.coroutines.test.runTest
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner

@RunWith(RobolectricTestRunner::class)
class FlashcardStudyViewModelTest {
    @get:Rule val main = MainDispatcherRule()

    private fun vm(repo: FakeFlashcardRepository) =
        FlashcardStudyViewModel(repo, SavedStateHandle(mapOf("deckName" to "C#", "bookmarkedOnly" to false)))

    @Test
    fun `grading records the review and moves to the next card`() = runTest {
        val repo = FakeFlashcardRepository(listOf(TestData.flashcard("f1"), TestData.flashcard("f2")))
        val vm = vm(repo)
        vm.flip()
        assertThat(vm.state.value.flipped).isTrue()
        vm.grade(FlashcardGrade.Good)
        assertThat(vm.state.value.current?.id).isEqualTo("f2")
        assertThat(vm.state.value.flipped).isFalse()
        vm.grade(FlashcardGrade.Again)
        assertThat(vm.state.value.finished).isTrue()
        assertThat(repo.reviews).containsExactly("f1" to FlashcardGrade.Good, "f2" to FlashcardGrade.Again).inOrder()
        assertThat(vm.state.value.grades[FlashcardGrade.Good]).isEqualTo(1)
    }

    @Test
    fun `nothing due offers studying every card`() = runTest {
        val vm = vm(FakeFlashcardRepository(emptyList()))
        assertThat((vm.state.value.cards as Loadable.Ready).data).isEmpty()
        vm.load(dueOnly = false)
        assertThat((vm.state.value.cards as Loadable.Ready).data.map { it.id }).containsExactly("extra")
    }

    @Test
    fun `bookmarking updates the card optimistically`() = runTest {
        val repo = FakeFlashcardRepository(listOf(TestData.flashcard("f1")))
        val vm = vm(repo)
        vm.toggleBookmark()
        assertThat(vm.state.value.current?.isBookmarked).isTrue()
        assertThat(repo.bookmarks["f1"]).isTrue()
    }
}
