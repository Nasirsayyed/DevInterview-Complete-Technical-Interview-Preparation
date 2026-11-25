package com.devinterview.domain

import com.devinterview.domain.model.*
import com.devinterview.domain.repository.QuizRepository
import com.devinterview.domain.usecase.FormValidator
import com.devinterview.domain.usecase.QuizTimer
import com.devinterview.domain.usecase.SubmitQuizUseCase
import com.devinterview.domain.usecase.quizProgress
import com.google.common.truth.Truth.assertThat
import kotlinx.coroutines.test.runTest
import org.junit.Test
import java.time.Instant

class FormValidatorTest {
    @Test fun `password policy matches backend rules`() {
        assertThat(FormValidator.password("short1A")).isNotNull()
        assertThat(FormValidator.password("alllowercase1")).contains("uppercase")
        assertThat(FormValidator.password("NoDigitsHere")).contains("digit")
        assertThat(FormValidator.password("Valid1Password")).isNull()
    }

    @Test fun `registration reports every invalid field`() {
        val errors = FormValidator.registration(RegistrationForm("", "L", "bad", "weak", "other", "Guru", null, null, emptyList()))
        assertThat(errors.keys).containsExactly("firstName", "email", "password", "confirmPassword", "experienceLevel")
    }

    @Test fun `verification codes must be six digits`() {
        assertThat(FormValidator.code("12345")).isNotNull()
        assertThat(FormValidator.code(" 123456 ")).isNull()
    }
}

class QuizTimerTest {
    @Test fun `remaining time uses server clock despite device skew`() {
        val serverNow = Instant.parse("2026-01-01T10:00:00Z")
        val deviceNow = Instant.parse("2026-01-01T09:55:00Z") // device clock is 5 minutes behind
        val timer = QuizTimer(serverNow.plusSeconds(600), serverNow, deviceNow)
        assertThat(timer.remainingSeconds(deviceNow)).isEqualTo(600)
        assertThat(timer.remainingSeconds(deviceNow.plusSeconds(590))).isEqualTo(10)
        assertThat(timer.isExpired(deviceNow.plusSeconds(601))).isTrue()
    }

    @Test fun `untimed quizzes have no remaining time`() {
        val now = Instant.now()
        val timer = QuizTimer(null, now, now)
        assertThat(timer.isTimed).isFalse()
        assertThat(timer.remainingSeconds(now)).isNull()
    }
}

class SubmitQuizUseCaseTest {
    private val attempt = QuizAttempt(
        attemptId = "a1", quizId = "q1", title = "Quiz", status = "InProgress", startedAt = "2026-01-01T10:00:00Z", serverNow = "2026-01-01T10:00:00Z",
        questions = listOf(
            QuizQuestion("x", 1, "X", "", type = "MultipleChoice", options = listOf(QuestionOption("o1", "A"))),
            QuizQuestion("y", 2, "Y", "", type = "MultipleChoice", options = listOf(QuestionOption("o2", "B"))),
        ),
    )

    @Test fun `submits answers in question order`() = runTest {
        val repo = RecordingQuizRepository()
        val answers = mapOf("y" to QuizAnswer("y", listOf("o2"), false, 5), "x" to QuizAnswer("x", emptyList(), true, 3))
        SubmitQuizUseCase(repo)(attempt, answers)
        assertThat(repo.submitted.map { it.questionId }).containsExactly("x", "y").inOrder()
        val progress = quizProgress(attempt, answers)
        assertThat(progress.answered).isEqualTo(1)
        assertThat(progress.markedForReview).isEqualTo(1)
        assertThat(progress.unanswered).isEqualTo(1)
    }
}

private class RecordingQuizRepository : QuizRepository {
    var submitted: List<QuizAnswer> = emptyList()
    override suspend fun submit(quizId: String, attemptId: String, answers: List<QuizAnswer>): AppResult<QuizResult> {
        submitted = answers
        return AppResult.Failure(AppError.Unknown())
    }
    override suspend fun quizzes(mode: String?, technologyId: String?, page: Int) = error("unused")
    override suspend fun daily() = error("unused")
    override suspend fun create(request: CreateQuiz) = error("unused")
    override suspend fun start(quizId: String) = error("unused")
    override suspend fun saveAnswer(attemptId: String, answer: QuizAnswer) = error("unused")
    override suspend fun result(attemptId: String) = error("unused")
    override suspend fun history(page: Int) = error("unused")
}
