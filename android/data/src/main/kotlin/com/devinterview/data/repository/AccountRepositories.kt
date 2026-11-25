package com.devinterview.data.repository

import android.os.Build
import com.devinterview.core.datastore.SessionStore
import com.devinterview.core.datastore.SettingsStore
import com.devinterview.core.network.*
import com.devinterview.domain.model.*
import com.devinterview.domain.repository.*
import com.devinterview.core.common.IoDispatcher
import com.devinterview.core.database.DevInterviewDatabase
import kotlinx.coroutines.CoroutineDispatcher
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.withContext
import kotlinx.serialization.json.JsonElement
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.MultipartBody
import okhttp3.RequestBody.Companion.toRequestBody
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class AuthRepositoryImpl @Inject constructor(
    private val api: AuthApi,
    private val session: SessionStore,
    private val users: UserApi,
    private val engagement: EngagementApi,
    private val settings: SettingsStore,
    private val database: DevInterviewDatabase,
    @IoDispatcher private val io: CoroutineDispatcher,
) : AuthRepository {
    private val device = "${Build.MANUFACTURER} ${Build.MODEL}".take(100)

    override val currentUser: Flow<UserSummary?> = session.user

    private suspend fun store(result: AppResult<AuthSession>): AppResult<UserSummary> = when (result) {
        is AppResult.Success -> { onSignedIn(result.data); AppResult.Success(result.data.user) }
        is AppResult.Failure -> result
    }

    /** Offline data belongs to one account: wipe it if a different account signs in on this device. */
    private suspend fun onSignedIn(auth: AuthSession) {
        val previous = settings.cachedUserId.first()
        if (previous != null && previous != auth.user.id) clearLocalData()
        session.save(auth)
        settings.setCachedUserId(auth.user.id)
    }

    private suspend fun clearLocalData() {
        withContext(io) { database.clearAllTables() }
        settings.clearSync()
    }

    override suspend fun login(email: String, password: String) = store(apiCall { api.login(LoginBody(email.trim(), password, device)) })
    override suspend fun loginWithGoogle(idToken: String) = store(apiCall { api.google(GoogleBody(idToken, device)) })

    override suspend fun register(form: RegistrationForm): AppResult<RegisterResult> {
        val result = apiCall { api.register(form.copy(email = form.email.trim())) }
        if (result is AppResult.Success) result.data.tokens?.let { onSignedIn(it) }
        return result
    }

    override suspend fun verifyEmail(email: String, code: String) = store(apiCall { api.verifyEmail(CodeBody(email.trim(), code.trim())) })
    override suspend fun resendVerification(email: String) = apiUnit { api.resend(EmailBody(email.trim())) }
    override suspend fun forgotPassword(email: String) = apiUnit { api.forgot(EmailBody(email.trim())) }
    override suspend fun resetPassword(email: String, code: String, newPassword: String) =
        apiUnit { api.reset(ResetBody(email.trim(), code.trim(), newPassword, newPassword)) }

    override suspend fun changePassword(current: String, newPassword: String): AppResult<Unit> =
        when (val r = store(apiCall { api.changePassword(ChangePasswordBody(current, newPassword, newPassword)) })) {
            is AppResult.Success -> AppResult.Success(Unit)
            is AppResult.Failure -> r
        }

    override suspend fun logout(allDevices: Boolean) {
        session.current()?.let { apiUnit { api.logout(LogoutBody(it.refreshToken, allDevices)) } }
        session.clear()
        clearLocalData()
        settings.setCachedUserId(null)
    }

    override suspend fun refreshUser(): AppResult<UserSummary> {
        val current = session.current() ?: return AppResult.Failure(AppError.Unauthorized())
        return when (val profile = apiCall { users.me() }) {
            is AppResult.Success -> {
                val tier = apiCall { engagement.subscriptionStatus() }.getOrNull()?.tier ?: current.user.tier
                val updated = current.user.copy(
                    firstName = profile.data.firstName, lastName = profile.data.lastName, profilePhotoUrl = profile.data.profilePhotoUrl,
                    emailConfirmed = profile.data.emailConfirmed, roles = profile.data.roles, tier = tier,
                )
                session.updateUser(updated)
                AppResult.Success(updated)
            }
            is AppResult.Failure -> profile
        }
    }
}

@Singleton
class ProfileRepositoryImpl @Inject constructor(
    private val api: UserApi,
    private val engagement: EngagementApi,
    private val session: SessionStore,
) : ProfileRepository {
    override suspend fun getProfile() = apiCall { api.me() }

    override suspend fun updateProfile(firstName: String, lastName: String, experienceLevel: String, currentRole: String?, targetRole: String?, bio: String?, technologyIds: List<String>) =
        apiCall { api.update(ProfileBody(firstName, lastName, experienceLevel, currentRole, targetRole, bio, technologyIds)) }.also { r ->
            if (r is AppResult.Success) session.current()?.let { session.updateUser(it.user.copy(firstName = r.data.firstName, lastName = r.data.lastName)) }
        }

    override suspend fun updatePreferences(timeZoneId: String, dailyGoal: Int, weeklyGoalMinutes: Int, showOnLeaderboard: Boolean) =
        apiCall { api.preferences(PreferencesBody(timeZoneId, dailyGoal, weeklyGoalMinutes, showOnLeaderboard)) }

    override suspend fun uploadPhoto(bytes: ByteArray, mimeType: String): AppResult<String> {
        val part = MultipartBody.Part.createFormData("file", "avatar", bytes.toRequestBody(mimeType.toMediaType()))
        return when (val r = apiCall { api.photo(part) }) {
            is AppResult.Success -> {
                session.current()?.let { session.updateUser(it.user.copy(profilePhotoUrl = r.data.profilePhotoUrl)) }
                AppResult.Success(r.data.profilePhotoUrl)
            }
            is AppResult.Failure -> r
        }
    }

    override suspend fun deleteAccount(password: String?): AppResult<Unit> {
        val r = apiUnit { api.delete(DeleteAccountBody(password)) }
        if (r is AppResult.Success) session.clear()
        return r
    }

    override suspend fun exportData(): AppResult<String> = apiCall { api.export() }.map { NetworkJson.encodeToString(JsonElement.serializer(), it) }

    override suspend fun registerDevice(token: String) = apiUnit { api.registerDevice(DeviceBody(token)) }
}

@Singleton
class SubscriptionRepositoryImpl @Inject constructor(private val api: EngagementApi, private val auth: AuthRepository) : SubscriptionRepository {
    override suspend fun plans() = apiCall { api.plans() }
    override suspend fun status() = apiCall { api.subscriptionStatus() }
    override suspend fun history() = apiCall { api.purchaseHistory() }
    override suspend fun verifyGooglePlayPurchase(productId: String, purchaseToken: String): AppResult<SubscriptionStatus> {
        val result = apiCall { api.verifyPurchase(VerifyPurchaseBody(productId, purchaseToken)) }
        if (result is AppResult.Success) auth.refreshUser()
        return result
    }
}

@Singleton
class SettingsRepositoryImpl @Inject constructor(private val store: SettingsStore) : SettingsRepository {
    override val themeMode = store.themeMode
    override val onboardingCompleted = store.onboardingCompleted
    override val notificationsEnabled = store.notificationsEnabled
    override suspend fun setThemeMode(mode: String) { store.setTheme(mode) }
    override suspend fun setOnboardingCompleted() { store.setOnboardingCompleted() }
    override suspend fun setNotificationsEnabled(enabled: Boolean) { store.setNotificationsEnabled(enabled) }
}

@Singleton
class NotificationRepositoryImpl @Inject constructor(private val api: EngagementApi) : NotificationRepository {
    override suspend fun notifications(page: Int) = apiCall { api.notifications(page) }
    override suspend fun markRead(id: String) = apiUnit { api.read(id) }
    override suspend fun markAllRead() = apiUnit { api.readAll() }
    override suspend fun preferences() = apiCall { api.notificationPreferences() }
    override suspend fun updatePreferences(preferences: List<NotificationPreference>) = apiCall { api.updateNotificationPreferences(NotificationPrefsBody(preferences)) }
    override suspend fun reminders() = apiCall { api.reminders() }
    override suspend fun createReminder(title: String, company: String?, scheduledAtIso: String, notes: String?) =
        apiCall { api.createReminder(ReminderBody(title, company, scheduledAtIso, notes)) }
    override suspend fun deleteReminder(id: String) = apiUnit { api.deleteReminder(id) }
}

@Singleton
class DashboardRepositoryImpl @Inject constructor(private val api: EngagementApi, private val content: ContentApi) : DashboardRepository {
    override suspend fun summary() = apiCall { api.dashboard() }
    override suspend fun activity(days: Int) = apiCall { api.activity(days) }
    override suspend fun skills() = apiCall { api.skills() }
    override suspend fun search(query: String, type: String?, page: Int) = apiCall { content.search(query, type, page) }
}

@Singleton
class AnalyticsRepositoryImpl @Inject constructor(private val api: EngagementApi) : AnalyticsRepository {
    override suspend fun overview() = apiCall { api.overview() }
    override suspend fun technologies() = apiCall { api.technologyAnalytics() }
    override suspend fun weekly(weeks: Int) = apiCall { api.weekly(weeks) }
    override suspend fun monthly(months: Int) = apiCall { api.monthly(months) }
    override suspend fun gamification() = apiCall { api.gamification() }
    override suspend fun leaderboard(period: String) = apiCall { api.leaderboard(period) }
}

@Singleton
class AiRepositoryImpl @Inject constructor(private val api: AiApi) : AiRepository {
    override suspend fun status() = apiCall { api.status() }
    override suspend fun evaluateAnswer(questionId: String?, questionText: String?, answer: String) = apiCall { api.evaluate(EvaluateBody(questionId, questionText, answer)) }
    override suspend fun evaluations(page: Int) = apiCall { api.evaluations(page) }
    override suspend fun evaluation(id: String) = apiCall { api.evaluation(id) }
    override suspend fun reviewCode(language: String, code: String, context: String?) = apiCall { api.review(ReviewBody(language, code, context)) }
}

@Singleton
class InterviewRepositoryImpl @Inject constructor(private val api: AiApi) : InterviewRepository {
    override suspend fun start(request: StartInterview) = apiCall { api.startInterview(request) }
    override suspend fun get(id: String) = apiCall { api.interview(id) }
    override suspend fun list(page: Int) = apiCall { api.interviews(page) }
    override suspend fun answer(id: String, questionId: String, answer: String, responseSeconds: Int, usedVoice: Boolean) =
        apiCall { api.answer(id, InterviewAnswerBody(questionId, answer, responseSeconds, usedVoice)) }
    override suspend fun skip(id: String) = apiCall { api.next(id) }
    override suspend fun complete(id: String) = apiCall { api.complete(id) }
    override suspend fun report(id: String) = apiCall { api.report(id) }
}

@Singleton
class QuizRepositoryImpl @Inject constructor(private val api: PracticeApi) : QuizRepository {
    override suspend fun quizzes(mode: String?, technologyId: String?, page: Int) = apiCall { api.quizzes(mode, technologyId, page) }
    override suspend fun daily() = apiCall { api.daily() }
    override suspend fun create(request: CreateQuiz) = apiCall { api.create(request) }
    override suspend fun start(quizId: String) = apiCall { api.start(quizId) }
    override suspend fun saveAnswer(attemptId: String, answer: QuizAnswer) = apiUnit { api.saveAnswer(attemptId, answer) }
    override suspend fun submit(quizId: String, attemptId: String, answers: List<QuizAnswer>) = apiCall { api.submit(quizId, SubmitQuizBody(attemptId, answers)) }
    override suspend fun result(attemptId: String) = apiCall { api.result(attemptId) }
    override suspend fun history(page: Int) = apiCall { api.history(page) }
}
