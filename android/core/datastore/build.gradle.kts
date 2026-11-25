plugins {
    alias(libs.plugins.devinterview.android.library)
    alias(libs.plugins.devinterview.android.hilt)
    alias(libs.plugins.kotlin.serialization)
}
android { namespace = "com.devinterview.core.datastore" }
dependencies {
    api(project(":core:common"))
    api(libs.androidx.datastore.preferences)
    implementation(libs.serialization.json)
    testImplementation(libs.robolectric)
    testImplementation(libs.androidx.test.core)
}
