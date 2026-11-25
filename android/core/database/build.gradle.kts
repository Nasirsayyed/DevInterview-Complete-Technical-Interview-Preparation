plugins {
    alias(libs.plugins.devinterview.android.library)
    alias(libs.plugins.devinterview.android.hilt)
}
android {
    namespace = "com.devinterview.core.database"
    defaultConfig { ksp { arg("room.schemaLocation", "$projectDir/schemas") } }
}
dependencies {
    api(project(":core:common"))
    api(libs.androidx.room.runtime)
    api(libs.androidx.room.ktx)
    ksp(libs.androidx.room.compiler)
    testImplementation(libs.robolectric)
    testImplementation(libs.androidx.test.core)
    testImplementation(libs.androidx.room.testing)
}
