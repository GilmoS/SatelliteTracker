import org.jetbrains.kotlin.gradle.dsl.JvmTarget
import org.jetbrains.kotlin.gradle.tasks.KotlinCompile

plugins {
    alias(libs.plugins.android.application)
    alias(libs.plugins.kotlin.android)
    alias(libs.plugins.kotlin.compose)
    alias(libs.plugins.kotlin.serialization)
    alias(libs.plugins.ksp)
    alias(libs.plugins.hilt)
    alias(libs.plugins.openapi.generator)
}

android {
    namespace = "com.sattrakk.app"
    compileSdk = 36

    defaultConfig {
        applicationId = "com.sattrakk.app"
        minSdk = 29
        targetSdk = 36
        versionCode = 1
        versionName = "0.1.0"

        testInstrumentationRunner = "androidx.test.runner.AndroidJUnitRunner"
        vectorDrawables { useSupportLibrary = true }

        // Dev backend listens on localhost:5076 (see backend/src/SatelliteTracker.API/Properties/
        // launchSettings.json); 10.0.2.2 is the standard emulator alias for the host machine's
        // localhost. Override per build type below, or via a real staging URL once one exists.
        buildConfigField("String", "API_BASE_URL", "\"http://10.0.2.2:5076/\"")
    }

    buildTypes {
        release {
            isMinifyEnabled = false
            proguardFiles(getDefaultProguardFile("proguard-android-optimize.txt"), "proguard-rules.pro")
        }
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    buildFeatures {
        compose = true
        buildConfig = true
    }

    // Generated OpenAPI models land under build/generated/openapi and are added
    // as an extra Kotlin source dir below — they are never written into src/main,
    // so nothing generated is ever committed to git (see data/remote/dto/README.md).
    sourceSets {
        getByName("main") {
            kotlin.srcDir(layout.buildDirectory.dir("generated/openapi/src/main/kotlin"))
        }
    }
}

kotlin {
    compilerOptions {
        jvmTarget.set(JvmTarget.JVM_17)
    }
}

// Generates ONLY the DTO/model layer from the backend's exported OpenAPI spec
// (openapi/sattrakk-api.json at repo root — see openapi/README.md for how it's
// produced). Retrofit service interfaces stay hand-written in data/remote/ so we
// control endpoint grouping, caching hints, and error handling explicitly;
// codegen only owns the request/response shapes, which is where hand-copying
// drifts from the backend fastest.
openApiGenerate {
    generatorName.set("kotlin")
    inputSpec.set(rootProject.file("../openapi/sattrakk-api.json").path)
    outputDir.set(layout.buildDirectory.dir("generated/openapi").get().asFile.path)
    packageName.set("com.sattrakk.app.data.remote.dto")
    modelPackage.set("com.sattrakk.app.data.remote.dto")

    generateApiTests.set(false)
    generateApiDocumentation.set(false)
    generateModelTests.set(false)
    generateModelDocumentation.set(false)

    // The generator has no generateApis flag — restricting to "models" here is what
    // actually turns off API/supporting-file generation (empty value = all models).
    globalProperties.set(mapOf("models" to ""))

    configOptions.set(
        mapOf(
            "serializationLibrary" to "kotlinx_serialization",
        )
    )
}

// KSP's task type isn't a KotlinCompile, so it needs its own explicit dependency
// on top of the KotlinCompile one below — Gradle won't infer either from the
// manual sourceSets.kotlin.srcDir() call above.
tasks.withType<KotlinCompile>().configureEach {
    dependsOn("openApiGenerate")
}
tasks.matching { it.name.startsWith("ksp") }.configureEach {
    dependsOn("openApiGenerate")
}

dependencies {
    implementation(libs.androidx.core.ktx)
    implementation(libs.androidx.lifecycle.runtime.ktx)
    implementation(libs.androidx.activity.compose)

    implementation(platform(libs.androidx.compose.bom))
    implementation(libs.androidx.ui)
    implementation(libs.androidx.ui.graphics)
    implementation(libs.androidx.ui.tooling.preview)
    implementation(libs.androidx.material3)
    debugImplementation(libs.androidx.ui.tooling)
    debugImplementation(libs.androidx.ui.test.manifest)

    implementation(libs.androidx.navigation.compose)

    implementation(libs.hilt.android)
    implementation(libs.hilt.navigation.compose)
    ksp(libs.hilt.android.compiler)

    implementation(libs.retrofit.core)
    implementation(libs.retrofit.kotlinx.serialization.converter)
    implementation(libs.okhttp.core)
    implementation(libs.kotlinx.serialization.json)
    implementation(libs.kotlinx.coroutines.core)

    implementation(libs.androidx.room.runtime)
    implementation(libs.androidx.room.ktx)
    ksp(libs.androidx.room.compiler)

    implementation(libs.androidx.security.crypto)

    testImplementation(libs.junit)
    testImplementation(libs.kotlinx.coroutines.test)
    testImplementation(libs.okhttp.mockwebserver)
    testImplementation(libs.mockk)
    androidTestImplementation(libs.androidx.test.ext.junit)
    androidTestImplementation(libs.androidx.espresso.core)
    androidTestImplementation(platform(libs.androidx.compose.bom))
    androidTestImplementation(libs.androidx.ui.test.junit4)
}
