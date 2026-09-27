package com.sattrakk.app.domain.util

import com.sattrakk.app.domain.model.LatLng
import kotlin.math.asin
import kotlin.math.atan2
import kotlin.math.cos
import kotlin.math.sin
import kotlin.math.sqrt

// Pure spherical geometry for the Map screen — no I/O, no Android dependency. Earth is treated as
// a sphere of mean radius 6371 km, which is plenty accurate for drawing a ~2000 km visibility
// footprint (the ellipsoid error is well under a pixel at map zoom levels where it's visible).
//
// Both formulas are the standard great-circle ones (e.g. as given by Ed Williams' Aviation
// Formulary / the "Movable Type" lat-long reference), not ad hoc approximations.
object GeoUtils {

    const val EARTH_MEAN_RADIUS_KM = 6371.0
    const val DEFAULT_FOOTPRINT_RADIUS_KM = 2000.0
    const val DEFAULT_FOOTPRINT_POINTS = 72

    // A circle of `radiusKm` around `center` on the Earth's surface, as `points` vertices at evenly
    // spaced bearings (0°, 360/points°, ...). For each bearing θ and angular distance δ = r / R:
    //   φ2 = asin( sin φ1 · cos δ + cos φ1 · sin δ · cos θ )
    //   λ2 = λ1 + atan2( sin θ · sin δ · cos φ1, cos δ − sin φ1 · sin φ2 )
    // Longitudes are normalized to [-180, 180). The polygon is open (the first point is not
    // repeated at the end) — closing it is the renderer's concern.
    fun footprintPolygon(
        center: LatLng,
        radiusKm: Double = DEFAULT_FOOTPRINT_RADIUS_KM,
        points: Int = DEFAULT_FOOTPRINT_POINTS
    ): List<LatLng> {
        require(points >= 3) { "A polygon needs at least 3 points, got $points" }
        require(radiusKm > 0) { "radiusKm must be positive, got $radiusKm" }

        return List(points) { i -> destinationPoint(center, 360.0 * i / points, radiusKm) }
    }

    // The point `distanceKm` away from `from` along the great circle leaving it at
    // `bearingDegrees` (clockwise from true north). The formula documented on footprintPolygon;
    // longitude normalized to [-180, 180).
    fun destinationPoint(from: LatLng, bearingDegrees: Double, distanceKm: Double): LatLng {
        val lat1 = Math.toRadians(from.latitude)
        val lon1 = Math.toRadians(from.longitude)
        val bearing = Math.toRadians(bearingDegrees)
        val angularDistance = distanceKm / EARTH_MEAN_RADIUS_KM
        val lat2 = asin(
            sin(lat1) * cos(angularDistance) + cos(lat1) * sin(angularDistance) * cos(bearing)
        )
        val lon2 = lon1 + atan2(
            sin(bearing) * sin(angularDistance) * cos(lat1),
            cos(angularDistance) - sin(lat1) * sin(lat2)
        )
        return LatLng(Math.toDegrees(lat2), normalizeLongitude(Math.toDegrees(lon2)))
    }

    // Initial great-circle bearing from `from` to `to`, in degrees clockwise from true north,
    // normalized to [0, 360):
    //   θ = atan2( sin Δλ · cos φ2, cos φ1 · sin φ2 − sin φ1 · cos φ2 · cos Δλ )
    // Works across the antimeridian as-is (Δλ goes through sin/cos). Meaningless for identical
    // points (returns 0).
    fun initialBearingDegrees(from: LatLng, to: LatLng): Double {
        val lat1 = Math.toRadians(from.latitude)
        val lat2 = Math.toRadians(to.latitude)
        val dLon = Math.toRadians(to.longitude - from.longitude)
        val y = sin(dLon) * cos(lat2)
        val x = cos(lat1) * sin(lat2) - sin(lat1) * cos(lat2) * cos(dLon)
        return (Math.toDegrees(atan2(y, x)) + 360.0) % 360.0
    }

    // Haversine great-circle distance in km. Used by tests to verify footprintPolygon's output, and
    // available to the Map UI (e.g. distance from the observer).
    fun distanceKm(a: LatLng, b: LatLng): Double {
        val lat1 = Math.toRadians(a.latitude)
        val lat2 = Math.toRadians(b.latitude)
        val dLat = lat2 - lat1
        val dLon = Math.toRadians(b.longitude - a.longitude)
        val h = sin(dLat / 2) * sin(dLat / 2) + cos(lat1) * cos(lat2) * sin(dLon / 2) * sin(dLon / 2)
        return 2 * EARTH_MEAN_RADIUS_KM * atan2(sqrt(h), sqrt(1 - h))
    }

    private fun normalizeLongitude(degrees: Double): Double = ((degrees + 540.0) % 360.0) - 180.0
}
