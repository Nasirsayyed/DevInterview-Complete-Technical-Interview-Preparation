package com.devinterview.feature.questions

import androidx.compose.ui.test.*
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.lifecycle.SavedStateHandle
import com.devinterview.core.designsystem.DevInterviewTheme
import com.devinterview.core.testing.FakeLibraryRepository
import com.devinterview.core.testing.FakeQuestionRepository
import com.google.common.truth.Truth.assertThat
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.annotation.Config

@RunWith(RobolectricTestRunner::class)
@Config(sdk = [35])
class QuestionDetailScreenTest {
    @get:Rule val compose = createComposeRule()

    @Test
    fun answeringShowsResultAndExplanation() {
        val repo = FakeQuestionRepository()
        val vm = QuestionDetailViewModel(repo, FakeLibraryRepository(), SavedStateHandle(mapOf("questionId" to "q1")))
        compose.setContent { DevInterviewTheme { QuestionDetailScreen(onBack = {}, onUpgrade = {}, onEvaluate = { _, _ -> }, viewModel = vm) } }
        compose.onNodeWithTag("question_title").assertIsDisplayed()
        compose.onNodeWithTag("check_answer").assertIsNotEnabled()
        compose.onNodeWithTag("option_0").performScrollTo().performClick()
        compose.onNodeWithTag("check_answer").performScrollTo().assertIsEnabled().performClick()
        compose.onNodeWithTag("attempt_result").assertTextEquals("Correct!")
        compose.onNodeWithText("Explanation").assertExists()
        assertThat(repo.attempts.single().second).containsExactly("o1")
    }

    @Test
    fun bookmarkToggles() {
        val library = FakeLibraryRepository()
        val vm = QuestionDetailViewModel(FakeQuestionRepository(), library, SavedStateHandle(mapOf("questionId" to "q1")))
        compose.setContent { DevInterviewTheme { QuestionDetailScreen(onBack = {}, onUpgrade = {}, onEvaluate = { _, _ -> }, viewModel = vm) } }
        compose.onNodeWithContentDescription("Bookmark").performClick()
        compose.onNodeWithContentDescription("Remove bookmark").assertExists()
        assertThat(library.bookmarks.value).containsKey("q1")
    }
}
