package com.devinterview.app

import android.app.Application
import android.app.NotificationChannel
import android.app.NotificationManager
import androidx.hilt.work.HiltWorkerFactory
import androidx.work.Configuration
import coil3.ImageLoader
import coil3.PlatformContext
import coil3.SingletonImageLoader
import coil3.network.okhttp.OkHttpNetworkFetcherFactory
import com.devinterview.data.sync.SyncRepositoryImpl
import dagger.hilt.android.HiltAndroidApp
import javax.inject.Inject

@HiltAndroidApp
class DevInterviewApp : Application(), Configuration.Provider, SingletonImageLoader.Factory {
    @Inject lateinit var workerFactory: HiltWorkerFactory
    @Inject lateinit var sync: SyncRepositoryImpl

    override val workManagerConfiguration: Configuration
        get() = Configuration.Builder().setWorkerFactory(workerFactory).setMinimumLoggingLevel(android.util.Log.WARN).build()

    override fun onCreate() {
        super.onCreate()
        createNotificationChannels()
        sync.schedulePeriodicSync()
    }

    private fun createNotificationChannels() {
        val channel = NotificationChannel(REMINDER_CHANNEL, getString(R.string.notification_channel_reminders), NotificationManager.IMPORTANCE_DEFAULT)
            .apply { description = getString(R.string.notification_channel_reminders_description) }
        getSystemService(NotificationManager::class.java).createNotificationChannel(channel)
    }

    override fun newImageLoader(context: PlatformContext): ImageLoader =
        ImageLoader.Builder(context).components { add(OkHttpNetworkFetcherFactory()) }.build()

    companion object {
        /** Must match the channel id the backend sets in FCM messages. */
        const val REMINDER_CHANNEL = "devinterview_reminders"
    }
}
