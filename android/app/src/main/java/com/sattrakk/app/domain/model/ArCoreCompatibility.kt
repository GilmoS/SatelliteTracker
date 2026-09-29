package com.sattrakk.app.domain.model

// ARCore support on this device, as a UI-switchable enum. Wraps ArCoreApk.Availability's seven
// values (see ArCompatibilityChecker's mapping) so nothing above the data layer depends on the
// ARCore SDK's own types.
enum class ArCoreCompatibility {
    // SUPPORTED_INSTALLED: the device is ARCore-certified and a recent-enough ARCore APK is installed.
    SUPPORTED,

    // SUPPORTED_NOT_INSTALLED: certified device, but "Google Play Services for AR" isn't installed.
    SUPPORTED_APK_NOT_INSTALLED,

    // SUPPORTED_APK_TOO_OLD: certified device, installed ARCore APK is older than this SDK needs.
    SUPPORTED_APK_TOO_OLD,

    // UNSUPPORTED_DEVICE_NOT_CAPABLE: not on Google's ARCore supported-devices list.
    UNSUPPORTED,

    // UNKNOWN_ERROR / UNKNOWN_TIMED_OUT, or still UNKNOWN_CHECKING after the retry budget. Usually
    // a first check with no network (ARCore fetches the device profile). Not a verdict on the device.
    UNKNOWN,
}
