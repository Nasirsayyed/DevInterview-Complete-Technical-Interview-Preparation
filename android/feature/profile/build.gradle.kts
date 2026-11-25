plugins { alias(libs.plugins.devinterview.android.feature) }
android { namespace = "com.devinterview.feature.profile" }
dependencies {
    // BillingManager wraps Google Play Billing; purchases are verified by the backend.
    implementation(project(":data"))
    implementation(libs.billing.ktx)
}
