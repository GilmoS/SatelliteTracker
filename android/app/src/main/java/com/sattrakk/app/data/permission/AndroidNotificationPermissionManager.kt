package com.sattrakk.app.data.permission

import android.Manifest
import android.app.Activity
import android.content.Context
import android.content.pm.PackageManager
import android.os.Build
import androidx.core.app.ActivityCompat
import androidx.core.content.ContextCompat
import dagger.hilt.android.qualifiers.ApplicationContext
import javax.inject.Inject
import javax.inject.Singleton

// Below API 33 (TIRAMISU), POST_NOTIFICATIONS doesn't exist as a runtime permission — it's granted
// at install time — so both methods short-circuit to the "always allowed, never worth asking again"
// answer without touching ContextCompat/ActivityCompat at all.
@Singleton
class AndroidNotificationPermissionManager @Inject constructor(
    @ApplicationContext private val context: Context
) : NotificationPermissionManager {

    // Overridden only by tests. Hilt always constructs this via the @Inject constructor above, so
    // production code always falls through to the real Build.VERSION.SDK_INT here — this avoids a
    // reflection hack to fake a final static Build.VERSION.SDK_INT field, and avoids adding an
    // extra constructor parameter that Dagger would need its own (nonsensical) binding for.
    internal var sdkIntOverrideForTests: Int? = null
    private val sdkInt: Int get() = sdkIntOverrideForTests ?: Build.VERSION.SDK_INT

    override fun isGranted(): Boolean {
        if (sdkInt < Build.VERSION_CODES.TIRAMISU) return true
        return ContextCompat.checkSelfPermission(
            context,
            Manifest.permission.POST_NOTIFICATIONS
        ) == PackageManager.PERMISSION_GRANTED
    }

    override fun shouldShowRationale(activity: Activity): Boolean {
        if (sdkInt < Build.VERSION_CODES.TIRAMISU) return false
        return ActivityCompat.shouldShowRequestPermissionRationale(
            activity,
            Manifest.permission.POST_NOTIFICATIONS
        )
    }
}
