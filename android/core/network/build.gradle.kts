plugins {
    alias(libs.plugins.devinterview.android.library)
    alias(libs.plugins.devinterview.android.hilt)
    alias(libs.plugins.kotlin.serialization)
}
android {
    namespace = "com.devinterview.core.network"
    buildFeatures { buildConfig = true }
    defaultConfig {
        val baseUrl = providers.gradleProperty("devinterview.apiBaseUrl").getOrElse("http://10.0.2.2:5080/")
        buildConfigField("String", "API_BASE_URL", "\"$baseUrl\"")
    }
}
dependencies {
    api(project(":core:common"))
    implementation(project(":core:datastore"))
    api(libs.retrofit)
    api(libs.okhttp)
    implementation(libs.retrofit.kotlinx.serialization)
    implementation(libs.okhttp.logging)
    implementation(libs.serialization.json)
    testImplementation(libs.okhttp.mockwebserver)
}
