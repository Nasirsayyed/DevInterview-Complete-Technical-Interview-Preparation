pluginManagement {
    includeBuild("build-logic")
    repositories {
        google()
        providers.gradleProperty("devinterview.mavenCentralMirror").orNull?.let { maven(it) }
        mavenCentral()
        gradlePluginPortal()
    }
}

dependencyResolutionManagement {
    repositoriesMode.set(RepositoriesMode.FAIL_ON_PROJECT_REPOS)
    repositories {
        google()
        // Optional mirror (e.g. in CI sandboxes where Maven Central is rate limited); set in ~/.gradle/gradle.properties.
        providers.gradleProperty("devinterview.mavenCentralMirror").orNull?.let { maven(it) }
        mavenCentral()
    }
}

rootProject.name = "DevInterview"
enableFeaturePreview("TYPESAFE_PROJECT_ACCESSORS")

include(":app")
include(":core:common", ":core:designsystem", ":core:network", ":core:database", ":core:datastore", ":core:navigation")
include(":domain", ":data", ":core:testing")
include(
    ":feature:auth", ":feature:dashboard", ":feature:questions", ":feature:quiz", ":feature:coding", ":feature:mockinterview",
    ":feature:learning", ":feature:flashcards", ":feature:analytics", ":feature:profile", ":feature:settings"
)
