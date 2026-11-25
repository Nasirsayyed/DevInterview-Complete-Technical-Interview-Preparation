plugins {
    alias(libs.plugins.devinterview.android.library)
    alias(libs.plugins.devinterview.android.hilt)
}
android { namespace = "com.devinterview.core.common" }
dependencies {
    api(project(":domain"))
    api(libs.coroutines.android)
    implementation(libs.androidx.core.ktx)
}
