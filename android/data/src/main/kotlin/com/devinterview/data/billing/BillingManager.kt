package com.devinterview.data.billing

import android.app.Activity
import android.content.Context
import com.android.billingclient.api.*
import com.devinterview.domain.model.AppError
import com.devinterview.domain.model.AppResult
import com.devinterview.domain.model.SubscriptionPlan
import com.devinterview.domain.model.SubscriptionStatus
import com.devinterview.domain.repository.SubscriptionRepository
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.launch
import javax.inject.Inject
import javax.inject.Singleton

/** A purchasable offer for a plan, priced by Google Play in the user's currency. */
data class PlayOffer(val plan: SubscriptionPlan, val formattedPrice: String, val details: ProductDetails, val offerToken: String)

sealed interface PurchaseState {
    data object Idle : PurchaseState
    data object Verifying : PurchaseState
    data class Completed(val status: SubscriptionStatus) : PurchaseState
    data class Failed(val message: String) : PurchaseState
}

/**
 * Google Play Billing wrapper. The app never decides entitlements: every purchase token is sent to the backend,
 * which verifies it with the Google Play Developer API (and acknowledges it) before premium access is granted.
 */
@Singleton
class BillingManager @Inject constructor(
    @ApplicationContext context: Context,
    private val subscriptions: SubscriptionRepository,
    @com.devinterview.core.common.ApplicationScope private val scope: kotlinx.coroutines.CoroutineScope,
) : PurchasesUpdatedListener {
    private val client = BillingClient.newBuilder(context)
        .setListener(this)
        .enablePendingPurchases(PendingPurchasesParams.newBuilder().enableOneTimeProducts().build())
        .enableAutoServiceReconnection()
        .build()
    private val _state = MutableStateFlow<PurchaseState>(PurchaseState.Idle)
    val state: StateFlow<PurchaseState> = _state
    private var pendingPlans: List<SubscriptionPlan> = emptyList()

    private suspend fun connect(): Boolean {
        if (client.isReady) return true
        val done = CompletableDeferred<Boolean>()
        client.startConnection(object : BillingClientStateListener {
            override fun onBillingSetupFinished(result: BillingResult) { done.complete(result.responseCode == BillingClient.BillingResponseCode.OK) }
            override fun onBillingServiceDisconnected() { if (!done.isCompleted) done.complete(false) }
        })
        return done.await()
    }

    /** Matches backend plans (with Google Play product/base plan ids) to Play offers. */
    suspend fun offers(plans: List<SubscriptionPlan>): AppResult<List<PlayOffer>> {
        pendingPlans = plans
        val productIds = plans.mapNotNull { it.googlePlayProductId }.distinct()
        if (productIds.isEmpty()) return AppResult.Success(emptyList())
        if (!connect()) return AppResult.Failure(AppError.Unavailable("billing_unavailable", "Google Play Billing is not available on this device."))
        val params = QueryProductDetailsParams.newBuilder().setProductList(productIds.map {
            QueryProductDetailsParams.Product.newBuilder().setProductId(it).setProductType(BillingClient.ProductType.SUBS).build()
        }).build()
        val result = client.queryProductDetails(params)
        if (result.billingResult.responseCode != BillingClient.BillingResponseCode.OK)
            return AppResult.Failure(AppError.Unavailable("billing_unavailable", result.billingResult.debugMessage.ifBlank { "Could not load plans from Google Play." }))
        val details = result.productDetailsList.orEmpty()
        val offers = plans.mapNotNull { plan ->
            val product = details.firstOrNull { it.productId == plan.googlePlayProductId } ?: return@mapNotNull null
            val offer = product.subscriptionOfferDetails?.firstOrNull { plan.googlePlayBasePlanId == null || it.basePlanId == plan.googlePlayBasePlanId } ?: return@mapNotNull null
            val price = offer.pricingPhases.pricingPhaseList.lastOrNull()?.formattedPrice ?: return@mapNotNull null
            PlayOffer(plan, price, product, offer.offerToken)
        }
        return AppResult.Success(offers)
    }

    fun launch(activity: Activity, offer: PlayOffer, obfuscatedAccountId: String): Boolean {
        val params = BillingFlowParams.newBuilder()
            .setProductDetailsParamsList(listOf(BillingFlowParams.ProductDetailsParams.newBuilder().setProductDetails(offer.details).setOfferToken(offer.offerToken).build()))
            .setObfuscatedAccountId(obfuscatedAccountId)
            .build()
        return client.launchBillingFlow(activity, params).responseCode == BillingClient.BillingResponseCode.OK
    }

    override fun onPurchasesUpdated(result: BillingResult, purchases: MutableList<Purchase>?) {
        when (result.responseCode) {
            BillingClient.BillingResponseCode.OK -> purchases?.forEach { verifyAsync(it) }
            BillingClient.BillingResponseCode.USER_CANCELED -> _state.value = PurchaseState.Idle
            else -> _state.value = PurchaseState.Failed(result.debugMessage.ifBlank { "The purchase could not be completed." })
        }
    }

    private fun verifyAsync(purchase: Purchase) {
        if (purchase.purchaseState != Purchase.PurchaseState.PURCHASED) return
        _state.value = PurchaseState.Verifying
        scope.launch {
            val productId = purchase.products.firstOrNull() ?: return@launch
        _state.value = when (val r = subscriptions.verifyGooglePlayPurchase(productId, purchase.purchaseToken)) {
            is AppResult.Success -> PurchaseState.Completed(r.data)
            is AppResult.Failure -> PurchaseState.Failed(r.error.message)
            }
        }
    }

    /** Re-sends existing purchases to the backend (restore purchases / new device). */
    suspend fun restore(): AppResult<Int> {
        if (!connect()) return AppResult.Failure(AppError.Unavailable("billing_unavailable", "Google Play Billing is not available on this device."))
        val result = client.queryPurchasesAsync(QueryPurchasesParams.newBuilder().setProductType(BillingClient.ProductType.SUBS).build())
        val active = result.purchasesList.filter { it.purchaseState == Purchase.PurchaseState.PURCHASED }
        active.forEach { p -> p.products.firstOrNull()?.let { subscriptions.verifyGooglePlayPurchase(it, p.purchaseToken) } }
        return AppResult.Success(active.size)
    }

    fun reset() { _state.value = PurchaseState.Idle }
}
