pluginManagement {
    repositories {
        providers.gradleProperty("devinterview.mavenCentralMirror").orNull?.let { maven(it) }
        gradlePluginPortal()
        google()
    }
}

dependencyResolutionManagement {
    repositories {
        google()
        providers.gradleProperty("devinterview.mavenCentralMirror").orNull?.let { maven(it) }
        mavenCentral()
        gradlePluginPortal()
    }
    versionCatalogs { create("libs") { from(files("../gradle/libs.versions.toml")) } }
}
rootProject.name = "build-logic"
include(":convention")
