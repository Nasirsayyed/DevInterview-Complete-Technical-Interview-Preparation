package com.devinterview.data.di

import com.devinterview.data.repository.*
import com.devinterview.data.sync.SyncRepositoryImpl
import com.devinterview.domain.repository.*
import dagger.Binds
import dagger.Module
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent

@Module
@InstallIn(SingletonComponent::class)
abstract class DataModule {
    @Binds abstract fun auth(impl: AuthRepositoryImpl): AuthRepository
    @Binds abstract fun profile(impl: ProfileRepositoryImpl): ProfileRepository
    @Binds abstract fun questions(impl: QuestionRepositoryImpl): QuestionRepository
    @Binds abstract fun library(impl: LibraryRepositoryImpl): LibraryRepository
    @Binds abstract fun quizzes(impl: QuizRepositoryImpl): QuizRepository
    @Binds abstract fun coding(impl: CodingRepositoryImpl): CodingRepository
    @Binds abstract fun ai(impl: AiRepositoryImpl): AiRepository
    @Binds abstract fun interviews(impl: InterviewRepositoryImpl): InterviewRepository
    @Binds abstract fun learning(impl: LearningRepositoryImpl): LearningRepository
    @Binds abstract fun flashcards(impl: FlashcardRepositoryImpl): FlashcardRepository
    @Binds abstract fun dashboard(impl: DashboardRepositoryImpl): DashboardRepository
    @Binds abstract fun analytics(impl: AnalyticsRepositoryImpl): AnalyticsRepository
    @Binds abstract fun notifications(impl: NotificationRepositoryImpl): NotificationRepository
    @Binds abstract fun subscriptions(impl: SubscriptionRepositoryImpl): SubscriptionRepository
    @Binds abstract fun sync(impl: SyncRepositoryImpl): SyncRepository
    @Binds abstract fun settings(impl: SettingsRepositoryImpl): SettingsRepository
}
