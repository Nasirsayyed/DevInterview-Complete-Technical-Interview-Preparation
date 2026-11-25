import com.android.build.api.dsl.CommonExtension
import org.gradle.api.Plugin
import org.gradle.api.Project
import org.gradle.kotlin.dsl.dependencies

class AndroidComposeConventionPlugin : Plugin<Project> {
    override fun apply(target: Project) = with(target) {
        pluginManager.apply("org.jetbrains.kotlin.plugin.compose")
        (extensions.getByName("android") as CommonExtension<*, *, *, *, *, *>).buildFeatures.compose = true
        dependencies {
            val bom = catalog.lib("androidx-compose-bom")
            add("implementation", platform(bom))
            add("testImplementation", platform(bom))
            add("implementation", catalog.lib("androidx-compose-ui"))
            add("implementation", catalog.lib("androidx-compose-ui-graphics"))
            add("implementation", catalog.lib("androidx-compose-foundation"))
            add("implementation", catalog.lib("androidx-compose-material3"))
            add("implementation", catalog.lib("androidx-compose-material-icons"))
            add("implementation", catalog.lib("androidx-compose-animation"))
            add("implementation", catalog.lib("androidx-compose-ui-tooling-preview"))
            add("debugImplementation", catalog.lib("androidx-compose-ui-tooling"))
            add("debugImplementation", catalog.lib("androidx-compose-ui-test-manifest"))
            add("testImplementation", catalog.lib("androidx-compose-ui-test-junit4"))
            add("testImplementation", catalog.lib("robolectric"))
            add("testImplementation", catalog.lib("androidx-test-core"))
            add("testImplementation", catalog.lib("androidx-test-ext-junit"))
        }
    }
}
