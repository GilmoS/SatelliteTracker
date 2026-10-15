package com.sattrakk.app.data.device

import android.content.Context
import android.hardware.Sensor
import android.hardware.SensorManager
import com.google.ar.core.ArCoreApk
import com.sattrakk.app.domain.model.ArCoreCompatibility
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.delay
import kotlinx.coroutines.suspendCancellableCoroutine
import javax.inject.Inject
import kotlin.coroutines.resume

// Single point where Sky View's device capabilities are read, so SkyViewViewModel never touches
// Context, SensorManager or the ARCore SDK directly (same reasoning as
// NotificationPermissionManager). Neither check needs a runtime permission: motion sensors never
// require one, and ArCoreApk.checkAvailability* only queries the ARCore APK / device profile, it
// doesn't open the camera. Nothing here creates an ARCore Session or starts the camera.
interface ArCompatibilityChecker {

    suspend fun checkArCore(): ArCoreCompatibility

    fun hasRotationVectorSensor(): Boolean
}

class AndroidArCompatibilityChecker internal constructor(
    private val context: Context,
    private val arCoreApk: ArCoreApk,
    private val sensorManager: SensorManager?,
) : ArCompatibilityChecker {

    // The ARCore SDK's own singleton and the system SensorManager are resolved here rather than
    // bound in Hilt; the internal constructor above lets tests pass mocks of both instead.
    @Inject
    constructor(@ApplicationContext context: Context) : this(
        context,
        ArCoreApk.getInstance(),
        context.getSystemService(SensorManager::class.java),
    )

    override suspend fun checkArCore(): ArCoreCompatibility {
        // checkAvailabilityAsync is documented to report UNKNOWN_CHECKING while ARCore is still
        // querying its device profile; Google's own sample re-queries after ~200 ms. Bounded so a
        // stuck check ends as UNKNOWN instead of spinning forever.
        repeat(MAX_TRANSIENT_RETRIES) {
            val availability = queryAvailability()
            if (!availability.isTransient) return availability.toCompatibility()
            delay(TRANSIENT_RETRY_DELAY_MILLIS)
        }
        return ArCoreCompatibility.UNKNOWN
    }

    override fun hasRotationVectorSensor(): Boolean =
        sensorManager?.getDefaultSensor(Sensor.TYPE_ROTATION_VECTOR) != null

    private suspend fun queryAvailability(): ArCoreApk.Availability =
        suspendCancellableCoroutine { continuation ->
            arCoreApk.checkAvailabilityAsync(context) { availability ->
                if (continuation.isActive) continuation.resume(availability)
            }
        }

    internal companion object {
        const val MAX_TRANSIENT_RETRIES = 10
        const val TRANSIENT_RETRY_DELAY_MILLIS = 200L
    }
}

internal fun ArCoreApk.Availability.toCompatibility(): ArCoreCompatibility = when (this) {
    ArCoreApk.Availability.SUPPORTED_INSTALLED -> ArCoreCompatibility.SUPPORTED
    ArCoreApk.Availability.SUPPORTED_NOT_INSTALLED -> ArCoreCompatibility.SUPPORTED_APK_NOT_INSTALLED
    ArCoreApk.Availability.SUPPORTED_APK_TOO_OLD -> ArCoreCompatibility.SUPPORTED_APK_TOO_OLD
    ArCoreApk.Availability.UNSUPPORTED_DEVICE_NOT_CAPABLE -> ArCoreCompatibility.UNSUPPORTED
    ArCoreApk.Availability.UNKNOWN_ERROR,
    ArCoreApk.Availability.UNKNOWN_TIMED_OUT,
    ArCoreApk.Availability.UNKNOWN_CHECKING -> ArCoreCompatibility.UNKNOWN
}
