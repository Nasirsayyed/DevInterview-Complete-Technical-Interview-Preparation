import org.gradle.api.Plugin
import org.gradle.api.Project
import org.gradle.kotlin.dsl.dependencies

/** Feature modules: Android library + Compose + Hilt + the core modules every screen needs. */
class AndroidFeatureConventionPlugin : Plugin<Project> {
    override fun apply(target: Project) = with(target) {
        pluginManager.apply("devinterview.android.library")
        pluginManager.apply("devinterview.android.compose")
        pluginManager.apply("devinterview.android.hilt")
        pluginManager.apply("org.jetbrains.kotlin.plugin.serialization")
        dependencies {
            add("implementation", project(":core:common"))
            add("implementation", project(":core:designsystem"))
            add("implementation", project(":core:navigation"))
            add("implementation", project(":domain"))
            add("implementation", catalog.lib("androidx-lifecycle-runtime-compose"))
            add("implementation", catalog.lib("androidx-lifecycle-viewmodel-compose"))
            add("implementation", catalog.lib("androidx-navigation-compose"))
            add("implementation", catalog.lib("androidx-hilt-navigation-compose"))
            add("implementation", catalog.lib("serialization-json"))
            add("testImplementation", project(":core:testing"))
        }
    }
}
