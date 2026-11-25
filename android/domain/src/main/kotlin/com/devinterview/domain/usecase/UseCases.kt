package com.devinterview.domain.usecase

import com.devinterview.domain.model.AppError
import com.devinterview.domain.model.AppResult
import com.devinterview.domain.model.QuizAnswer
import com.devinterview.domain.model.QuizAttempt
import com.devinterview.domain.model.QuizResult
import com.devinterview.domain.model.RegistrationForm
import com.devinterview.domain.repository.QuizRepository
import java.time.Duration
import java.time.Instant
import javax.inject.Inject

/**
 * Client-side form validation for immediate feedback. The backend applies the same rules authoritatively;
 * these checks only prevent pointless round-trips.
 */
object FormValidator {
    private val EMAIL = Regex("^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$")
    val EXPERIENCE_LEVELS = listOf("Beginner", "Intermediate", "Advanced", "Expert")

    fun email(value: String): String? = when {
        value.isBlank() -> "Email is required."
        !EMAIL.matches(value.trim()) -> "Enter a valid email address."
        else -> null
    }

    fun password(value: String): String? = when {
        value.length < 8 -> "Password must be at least 8 characters."
        value.none { it.isUpperCase() } -> "Password must contain an uppercase letter."
        value.none { it.isLowerCase() } -> "Password must contain a lowercase letter."
        value.none { it.isDigit() } -> "Password must contain a digit."
        else -> null
    }

    fun code(value: String): String? = if (Regex("^\\d{6}$").matches(value.trim())) null else "Enter the 6-digit code from the email."

    fun registration(form: RegistrationForm): Map<String, String> = buildMap {
        if (form.firstName.isBlank()) put("firstName", "First name is required.")
        if (form.lastName.isBlank()) put("lastName", "Last name is required.")
        email(form.email)?.let { put("email", it) }
        password(form.password)?.let { put("password", it) }
        if (form.confirmPassword != form.password) put("confirmPassword", "Passwords do not match.")
        if (form.experienceLevel !in EXPERIENCE_LEVELS) put("experienceLevel", "Choose your experience level.")
        if (form.preferredTechnologyIds.size > 20) put("preferredTechnologyIds", "Select at most 20 technologies.")
    }
}

/**
 * Computes the remaining quiz time from the **server** deadline, compensating for device clock skew using the
 * server time returned when the attempt was started/resumed.
 */
class QuizTimer(private val deadline: Instant?, serverNow: Instant, deviceNowAtSync: Instant) {
    private val skew: Duration = Duration.between(deviceNowAtSync, serverNow)

    val isTimed: Boolean get() = deadline != null

    fun remainingSeconds(deviceNow: Instant): Long? {
        val end = deadline ?: return null
        val serverEstimate = deviceNow.plus(skew)
        return Duration.between(serverEstimate, end).seconds.coerceAtLeast(0)
    }

    fun isExpired(deviceNow: Instant): Boolean = remainingSeconds(deviceNow) == 0L

    companion object {
        fun from(attempt: QuizAttempt, deviceNow: Instant = Instant.now()) =
            QuizTimer(attempt.expiresAt?.let(Instant::parse), Instant.parse(attempt.serverNow), deviceNow)
    }
}

/** Submits a quiz with all locally held answers in one call (the backend ignores answers that arrive after the deadline). */
class SubmitQuizUseCase @Inject constructor(private val quizzes: QuizRepository) {
    suspend operator fun invoke(attempt: QuizAttempt, answers: Map<String, QuizAnswer>): AppResult<QuizResult> {
        if (attempt.questions.isEmpty()) return AppResult.Failure(AppError.Validation("This quiz has no questions."))
        val ordered = attempt.questions.mapNotNull { answers[it.questionId] }
        return quizzes.submit(attempt.quizId, attempt.attemptId, ordered)
    }
}

/** Summary shown on the submit confirmation dialog. */
data class QuizProgress(val answered: Int, val markedForReview: Int, val unanswered: Int)

fun quizProgress(attempt: QuizAttempt, answers: Map<String, QuizAnswer>): QuizProgress {
    val answered = attempt.questions.count { answers[it.questionId]?.selectedOptionIds?.isNotEmpty() == true }
    val marked = answers.values.count { it.isMarkedForReview }
    return QuizProgress(answered, marked, attempt.questions.size - answered)
}
