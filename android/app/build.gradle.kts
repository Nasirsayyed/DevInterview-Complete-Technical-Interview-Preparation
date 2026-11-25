plugins {
    alias(libs.plugins.devinterview.android.application)
    alias(libs.plugins.devinterview.android.compose)
    alias(libs.plugins.devinterview.android.hilt)
    alias(libs.plugins.kotlin.serialization)
}

// Firebase (FCM + Crashlytics) is enabled only when a google-services.json is supplied; it is never committed.
val firebaseConfigured = file("google-services.json").exists()
if (firebaseConfigured) {
    apply(plugin = libs.plugins.google.services.get().pluginId)
    apply(plugin = libs.plugins.firebase.crashlytics.get().pluginId)
}

android {
    namespace = "com.devinterview.app"
    defaultConfig {
        applicationId = "com.devinterview.app"
        versionCode = providers.gradleProperty("devinterview.versionCode").map(String::toInt).getOrElse(1)
        versionName = providers.gradleProperty("devinterview.versionName").getOrElse("1.0.0")
        testInstrumentationRunner = "androidx.test.runner.AndroidJUnitRunner"
        buildConfigField("String", "API_BASE_URL", "\"${providers.gradleProperty("devinterview.apiBaseUrl").getOrElse("http://10.0.2.2:5080/")}\"")
        buildConfigField("boolean", "FIREBASE_CONFIGURED", firebaseConfigured.toString())
    }
    buildFeatures { buildConfig = true }

    // Release signing comes from environment variables (CI secrets) — keystores and passwords are never committed.
    signingConfigs {
        create("release") {
            val storePath = System.getenv("DEVINTERVIEW_KEYSTORE_PATH")
            if (storePath != null) {
                storeFile = file(storePath)
                storePassword = System.getenv("DEVINTERVIEW_KEYSTORE_PASSWORD")
                keyAlias = System.getenv("DEVINTERVIEW_KEY_ALIAS")
                keyPassword = System.getenv("DEVINTERVIEW_KEY_PASSWORD")
            }
        }
    }
    buildTypes {
        debug {
            applicationIdSuffix = ".debug"
            manifestPlaceholders["usesCleartextTraffic"] = "true"
        }
        release {
            isMinifyEnabled = true
            isShrinkResources = true
            proguardFiles(getDefaultProguardFile("proguard-android-optimize.txt"), "proguard-rules.pro")
            manifestPlaceholders["usesCleartextTraffic"] = "false"
            if (System.getenv("DEVINTERVIEW_KEYSTORE_PATH") != null) signingConfig = signingConfigs.getByName("release")
        }
    }
    packaging { resources.excludes += setOf("/META-INF/{AL2.0,LGPL2.1}", "META-INF/LICENSE*", "META-INF/NOTICE*") }
}

dependencies {
    implementation(projects.core.common)
    implementation(projects.core.designsystem)
    implementation(projects.core.navigation)
    implementation(projects.core.network)
    implementation(projects.core.database)
    implementation(projects.core.datastore)
    implementation(projects.domain)
    implementation(projects.data)
    implementation(projects.feature.auth)
    implementation(projects.feature.dashboard)
    implementation(projects.feature.questions)
    implementation(projects.feature.quiz)
    implementation(projects.feature.coding)
    implementation(projects.feature.mockinterview)
    implementation(projects.feature.learning)
    implementation(projects.feature.flashcards)
    implementation(projects.feature.analytics)
    implementation(projects.feature.profile)
    implementation(projects.feature.settings)

    implementation(libs.androidx.core.ktx)
    implementation(libs.androidx.core.splashscreen)
    implementation(libs.androidx.activity.compose)
    implementation(libs.androidx.lifecycle.runtime.compose)
    implementation(libs.androidx.lifecycle.process)
    implementation(libs.androidx.navigation.compose)
    implementation(libs.androidx.hilt.navigation.compose)
    implementation(libs.androidx.work.runtime)
    implementation(libs.androidx.hilt.work)
    ksp(libs.androidx.hilt.compiler)
    implementation(libs.serialization.json)
    implementation(libs.coil.compose)
    implementation(libs.coil.network.okhttp)
    implementation(platform(libs.firebase.bom))
    implementation(libs.firebase.messaging)
    implementation(libs.firebase.crashlytics)

    testImplementation(projects.core.testing)
    testImplementation(libs.androidx.navigation.testing)
    testImplementation(libs.androidx.work.testing)
}
