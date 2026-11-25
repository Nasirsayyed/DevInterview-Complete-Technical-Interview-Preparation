plugins {
    alias(libs.plugins.devinterview.android.library)
    alias(libs.plugins.devinterview.android.compose)
}
android { namespace = "com.devinterview.core.designsystem" }
dependencies {
    api(project(":domain"))
    implementation(libs.androidx.core.ktx)
    implementation(libs.coil.compose)
    implementation(libs.coil.network.okhttp)
}
