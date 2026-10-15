package com.sattrakk.app.ui.skyview

import com.sattrakk.app.domain.model.ArCoreCompatibility

sealed interface SkyViewUiState {

    object Checking : SkyViewUiState

    data class Result(
        val arCoreStatus: ArCoreCompatibility,
        val hasRotationVectorSensor: Boolean,
    ) : SkyViewUiState {

        // Which message the screen shows, derived here (not in the Composable) so the precedence
        // is unit-tested. A computed property, like SettingsUiState.sendPushEnabled, so it can't
        // drift from the two raw fields.
        val support: SkyViewSupport
            get() = when {
                !hasRotationVectorSensor -> SkyViewSupport.UNSUPPORTED_DEVICE
                arCoreStatus == ArCoreCompatibility.UNSUPPORTED -> SkyViewSupport.UNSUPPORTED_DEVICE
                arCoreStatus == ArCoreCompatibility.SUPPORTED_APK_NOT_INSTALLED -> SkyViewSupport.ARCORE_NOT_INSTALLED
                arCoreStatus == ArCoreCompatibility.SUPPORTED_APK_TOO_OLD -> SkyViewSupport.ARCORE_TOO_OLD
                arCoreStatus == ArCoreCompatibility.UNKNOWN -> SkyViewSupport.UNDETERMINED
                else -> SkyViewSupport.SUPPORTED
            }
    }
}

// A missing sensor is a hardware verdict that no install can fix, so it wins over every ARCore
// state, including UNKNOWN.
enum class SkyViewSupport {
    SUPPORTED,
    ARCORE_NOT_INSTALLED,
    ARCORE_TOO_OLD,
    UNSUPPORTED_DEVICE,
    UNDETERMINED,
}
