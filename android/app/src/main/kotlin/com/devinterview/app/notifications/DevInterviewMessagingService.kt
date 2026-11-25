package com.devinterview.app.notifications

import android.Manifest
import android.app.PendingIntent
import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.net.Uri
import androidx.core.app.NotificationCompat
import androidx.core.app.NotificationManagerCompat
import androidx.core.content.ContextCompat
import com.devinterview.app.DevInterviewApp
import com.devinterview.app.MainActivity
import com.devinterview.app.R
import com.devinterview.core.common.ApplicationScope
import com.devinterview.domain.repository.AuthRepository
import com.devinterview.domain.repository.ProfileRepository
import com.devinterview.domain.repository.SettingsRepository
import com.google.firebase.FirebaseApp
import com.google.firebase.messaging.FirebaseMessaging
import com.google.firebase.messaging.FirebaseMessagingService
import com.google.firebase.messaging.RemoteMessage
import dagger.hilt.android.AndroidEntryPoint
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.launch
import kotlinx.coroutines.suspendCancellableCoroutine
import javax.inject.Inject
import kotlin.coroutines.resume

/** Receives FCM messages; active only in builds configured with a google-services.json. */
@AndroidEntryPoint
class DevInterviewMessagingService : FirebaseMessagingService() {
    @Inject lateinit var profiles: ProfileRepository
    @Inject lateinit var auth: AuthRepository
    @Inject lateinit var settings: SettingsRepository
    @Inject @ApplicationScope lateinit var scope: CoroutineScope

    override fun onNewToken(token: String) {
        scope.launch { if (auth.currentUser.first() != null) profiles.registerDevice(token) }
    }

    override fun onMessageReceived(message: RemoteMessage) {
        scope.launch {
            if (!settings.notificationsEnabled.first()) return@launch
            val title = message.notification?.title ?: message.data["title"] ?: return@launch
            val body = message.notification?.body ?: message.data["body"].orEmpty()
            Notifications.show(this@DevInterviewMessagingService, title, body, message.data["deepLink"], message.data["notificationId"]?.hashCode() ?: title.hashCode())
        }
    }
}

object Notifications {
    fun show(context: Context, title: String, body: String, deepLink: String?, id: Int) {
        if (ContextCompat.checkSelfPermission(context, Manifest.permission.POST_NOTIFICATIONS) != PackageManager.PERMISSION_GRANTED &&
            android.os.Build.VERSION.SDK_INT >= 33) return
        val intent = Intent(context, MainActivity::class.java).apply {
            action = Intent.ACTION_VIEW
            deepLink?.let { data = Uri.parse(it) }
            flags = Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_CLEAR_TOP
        }
        val pending = PendingIntent.getActivity(context, id, intent, PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT)
        val notification = NotificationCompat.Builder(context, DevInterviewApp.REMINDER_CHANNEL)
            .setSmallIcon(R.drawable.ic_notification)
            .setContentTitle(title)
            .setContentText(body)
            .setStyle(NotificationCompat.BigTextStyle().bigText(body))
            .setAutoCancel(true)
            .setContentIntent(pending)
            .build()
        NotificationManagerCompat.from(context).notify(id, notification)
    }

    /** Fetches the FCM token when Firebase is configured for this build; null otherwise. */
    suspend fun fcmToken(context: Context): String? {
        if (FirebaseApp.getApps(context).isEmpty()) return null
        return suspendCancellableCoroutine { cont ->
            FirebaseMessaging.getInstance().token
                .addOnSuccessListener { cont.resume(it) }
                .addOnFailureListener { cont.resume(null) }
        }
    }
}
