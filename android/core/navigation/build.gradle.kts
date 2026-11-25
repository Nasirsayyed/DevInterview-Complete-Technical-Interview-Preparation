plugins {
    alias(libs.plugins.devinterview.android.library)
    alias(libs.plugins.kotlin.serialization)
}
android { namespace = "com.devinterview.core.navigation" }
dependencies {
    api(libs.serialization.json)
    api(libs.androidx.navigation.compose)
}
