plugins { `kotlin-dsl` }

group = "com.devinterview.buildlogic"

java { sourceCompatibility = JavaVersion.VERSION_17; targetCompatibility = JavaVersion.VERSION_17 }
kotlin { compilerOptions { jvmTarget.set(org.jetbrains.kotlin.gradle.dsl.JvmTarget.JVM_17) } }

dependencies {
    compileOnly(libs.android.gradle.plugin)
    compileOnly(libs.kotlin.gradle.plugin)
    compileOnly(libs.compose.gradle.plugin)
    compileOnly(libs.ksp.gradle.plugin)
}

gradlePlugin {
    plugins {
        register("androidApplication") { id = "devinterview.android.application"; implementationClass = "AndroidApplicationConventionPlugin" }
        register("androidLibrary") { id = "devinterview.android.library"; implementationClass = "AndroidLibraryConventionPlugin" }
        register("androidCompose") { id = "devinterview.android.compose"; implementationClass = "AndroidComposeConventionPlugin" }
        register("androidFeature") { id = "devinterview.android.feature"; implementationClass = "AndroidFeatureConventionPlugin" }
        register("androidHilt") { id = "devinterview.android.hilt"; implementationClass = "AndroidHiltConventionPlugin" }
        register("jvmLibrary") { id = "devinterview.jvm.library"; implementationClass = "JvmLibraryConventionPlugin" }
    }
}
