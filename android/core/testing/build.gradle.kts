plugins { alias(libs.plugins.devinterview.android.library) }
android { namespace = "com.devinterview.core.testing" }
dependencies {
    api(project(":domain"))
    api(libs.junit)
    api(libs.coroutines.test)
    api(libs.turbine)
    api(libs.truth)
}
