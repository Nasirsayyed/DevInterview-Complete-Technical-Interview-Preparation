plugins { alias(libs.plugins.devinterview.android.feature) }
android { namespace = "com.devinterview.feature.questions" }
dependencies {
    implementation(libs.androidx.paging.runtime)
    implementation(libs.androidx.paging.compose)
    testImplementation(libs.androidx.paging.testing)
}
