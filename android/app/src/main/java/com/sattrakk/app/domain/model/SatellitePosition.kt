package com.sattrakk.app.domain.model

// Live, "now"-anchored data from the real-time endpoints (GET /api/satellites/{id}/position and
// /track, N2YO-backed). Never Room-cached — see MapRepository. Timestamps are converted from the
// backend's Unix seconds to epoch millis at the mapper boundary, matching PassTrackPoint.

data class SatellitePosition(
    val latitude: Double,
    val longitude: Double,
    val altitude: Double,
    val azimuth: Double,
    val elevation: Double,
    val timestampEpochMillis: Long
)

data class TrackPoint(
    val latitude: Double,
    val longitude: Double,
    val altitude: Double,
    val timestampEpochMillis: Long
)

// Plain lat/lng pair for derived geometry (the footprint polygon — see domain/util/GeoUtils.kt).
// Our own type, not a map SDK's, so the domain layer stays free of any map library dependency.
data class LatLng(
    val latitude: Double,
    val longitude: Double
)
