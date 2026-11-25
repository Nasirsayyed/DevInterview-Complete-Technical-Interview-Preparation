plugins {
    alias(libs.plugins.devinterview.jvm.library)
    alias(libs.plugins.kotlin.serialization)
}

dependencies {
    api(libs.coroutines.core)
    api(libs.serialization.json)
    implementation("javax.inject:javax.inject:1")
}
