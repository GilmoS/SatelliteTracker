package com.sattrakk.app.core

/**
 * Fixed ground-station location, mirrored from the backend's `Observer` config
 * (see `SatelliteTracker.API` appsettings `Observer` section). Used by Map and
 * Sky View to render the observer's position without a location permission —
 * this app tracks passes over one fixed site, not the device's own location.
 */
object ObserverLocation {
    const val NAME: String = "Ben Gurion Airport"
    const val LATITUDE: Double = 32.0055
    const val LONGITUDE: Double = 34.8854
    const val ALTITUDE_METERS: Double = 135.0
}
