package com.devinterview.core.common

import android.content.Context
import android.net.ConnectivityManager
import android.net.Network
import android.net.NetworkCapabilities
import android.net.NetworkRequest
import dagger.Binds
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.android.qualifiers.ApplicationContext
import dagger.hilt.components.SingletonComponent
import kotlinx.coroutines.CoroutineDispatcher
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.channels.awaitClose
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.callbackFlow
import kotlinx.coroutines.flow.conflate
import kotlinx.coroutines.flow.distinctUntilChanged
import java.time.Duration
import java.time.Instant
import javax.inject.Inject
import javax.inject.Qualifier
import javax.inject.Singleton

@Qualifier @Retention(AnnotationRetention.BINARY) annotation class IoDispatcher
@Qualifier @Retention(AnnotationRetention.BINARY) annotation class DefaultDispatcher
@Qualifier @Retention(AnnotationRetention.BINARY) annotation class ApplicationScope

@Module
@InstallIn(SingletonComponent::class)
object DispatchersModule {
    @Provides @IoDispatcher fun ioDispatcher(): CoroutineDispatcher = Dispatchers.IO
    @Provides @DefaultDispatcher fun defaultDispatcher(): CoroutineDispatcher = Dispatchers.Default
    @Provides @Singleton @ApplicationScope
    fun appScope(@DefaultDispatcher dispatcher: CoroutineDispatcher): CoroutineScope = CoroutineScope(SupervisorJob() + dispatcher)
}

interface NetworkMonitor {
    val isOnline: Flow<Boolean>
}

class ConnectivityNetworkMonitor @Inject constructor(@ApplicationContext private val context: Context) : NetworkMonitor {
    override val isOnline: Flow<Boolean> = callbackFlow {
        val manager = context.getSystemService(ConnectivityManager::class.java)
        if (manager == null) {
            trySend(false); close(); return@callbackFlow
        }
        val networks = mutableSetOf<Network>()
        val callback = object : ConnectivityManager.NetworkCallback() {
            override fun onAvailable(network: Network) { networks += network; trySend(true) }
            override fun onLost(network: Network) { networks -= network; trySend(networks.isNotEmpty()) }
        }
        manager.registerNetworkCallback(NetworkRequest.Builder().addCapability(NetworkCapabilities.NET_CAPABILITY_INTERNET).build(), callback)
        val active = manager.getNetworkCapabilities(manager.activeNetwork)
        trySend(active?.hasCapability(NetworkCapabilities.NET_CAPABILITY_VALIDATED) == true)
        awaitClose { manager.unregisterNetworkCallback(callback) }
    }.distinctUntilChanged().conflate()
}

@Module
@InstallIn(SingletonComponent::class)
abstract class NetworkMonitorModule {
    @Binds abstract fun bind(impl: ConnectivityNetworkMonitor): NetworkMonitor
}

object TimeFormat {
    fun relative(iso: String, now: Instant = Instant.now()): String = runCatching {
        val then = Instant.parse(if (iso.endsWith("Z") || iso.contains("+")) iso else "${iso}Z")
        val d = Duration.between(then, now)
        when {
            d.isNegative -> inFuture(Duration.between(now, then))
            d.toMinutes() < 1 -> "just now"
            d.toHours() < 1 -> "${d.toMinutes()} min ago"
            d.toDays() < 1 -> "${d.toHours()} h ago"
            d.toDays() < 7 -> "${d.toDays()} d ago"
            else -> "${d.toDays() / 7} w ago"
        }
    }.getOrDefault("")

    private fun inFuture(d: Duration) = when {
        d.toHours() < 1 -> "in ${d.toMinutes().coerceAtLeast(1)} min"
        d.toDays() < 1 -> "in ${d.toHours()} h"
        else -> "in ${d.toDays()} d"
    }

    fun clock(totalSeconds: Long): String = "%02d:%02d".format(totalSeconds / 60, totalSeconds % 60)

    fun minutes(minutes: Int): String = if (minutes < 60) "${minutes}m" else "${minutes / 60}h ${minutes % 60}m"

    fun parse(iso: String): Instant? = runCatching { Instant.parse(if (iso.endsWith("Z") || iso.contains("+")) iso else "${iso}Z") }.getOrNull()
}
