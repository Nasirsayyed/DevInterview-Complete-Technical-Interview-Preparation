plugins { alias(libs.plugins.devinterview.android.feature) }
android { namespace = "com.devinterview.feature.auth" }
android {
    buildFeatures { buildConfig = true }
    defaultConfig { buildConfigField("String", "GOOGLE_WEB_CLIENT_ID", "\"${providers.gradleProperty("devinterview.googleWebClientId").getOrElse("")}\"") }
}
dependencies {
    implementation(libs.androidx.credentials)
    implementation(libs.androidx.credentials.play.services)
    implementation(libs.googleid)
}
