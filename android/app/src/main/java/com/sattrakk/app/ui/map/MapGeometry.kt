package com.sattrakk.app.ui.map

import com.sattrakk.app.domain.model.LatLng
import kotlin.math.abs
import kotlin.math.sign

// Pure lat/lng -> renderable-geometry conversions for MapScreen. No map-SDK types here (MapScreen
// wraps the results in MapLibre/spatialk GeoJSON types itself), so this stays JVM-unit-testable.
//
// GeoUtils (domain layer) deliberately produces plain, normalized [-180, 180) coordinates and
// leaves antimeridian handling to the renderer — this is that renderer-side half.
internal object MapGeometry {

    // Web Mercator can't represent the poles; MapLibre's own latitude clamp.
    const val MAX_MERCATOR_LATITUDE = 85.051129

    /**
     * Splits a ground track into line segments at every antimeridian crossing, so a track running
     * from +179° to -179° is drawn as two short pieces meeting at the ±180° edge instead of one
     * line streaking the whole way back across the map.
     *
     * A crossing is any consecutive pair whose longitude jumps by more than 180° (the short way
     * round is always the real path between two ground-track samples seconds apart). The crossing
     * latitude is linearly interpolated, and each side gets an explicit endpoint on the ±180° line
     * so the two pieces meet exactly. Every returned segment has at least 2 points; a track of
     * fewer than 2 points yields no segments.
     */
    fun splitAtAntimeridian(points: List<LatLng>): List<List<LatLng>> {
        if (points.size < 2) return emptyList()
        val segments = mutableListOf<List<LatLng>>()
        var current = mutableListOf(points.first())
        for (i in 1 until points.size) {
            val prev = points[i - 1]
            val next = points[i]
            val delta = next.longitude - prev.longitude
            if (abs(delta) > 180.0) {
                // Eastward over +180 if prev is in the east, westward over -180 otherwise.
                val edge = if (prev.longitude > 0) 180.0 else -180.0
                val unwrappedNextLon = next.longitude + if (edge > 0) 360.0 else -360.0
                val t = (edge - prev.longitude) / (unwrappedNextLon - prev.longitude)
                val crossingLat = prev.latitude + t * (next.latitude - prev.latitude)
                current.add(LatLng(crossingLat, edge))
                segments.add(current)
                current = mutableListOf(LatLng(crossingLat, -edge))
            }
            current.add(next)
        }
        segments.add(current)
        return segments.filter { it.size >= 2 }
    }

    /**
     * Turns an open footprint ring (GeoUtils.footprintPolygon's output: evenly spaced, first point
     * not repeated, longitudes normalized to [-180, 180)) into one closed polygon ring MapLibre can
     * fill correctly:
     *
     * - Longitudes are unwrapped to be continuous (each within 180° of the previous one), starting
     *   within 180° of [center]. Near the antimeridian this lets a ring run past ±180° (e.g. up to
     *   185°) instead of splitting into two polygons — MapLibre renders out-of-range longitudes on
     *   the adjacent world copy, so it still draws as one intact circle.
     * - If the ring encloses a pole (possible for a satellite above roughly ±72° latitude with the
     *   2000 km radius), the unwrapped longitudes sweep a full 360° and can't close on their own.
     *   The ring is then closed along the Mercator latitude limit on that pole's side, which fills
     *   the polar cap as the true footprint does.
     *
     * The returned ring is closed (last point equals first) and has at least 4 points whenever the
     * input has at least 3 (fewer yields an empty list).
     */
    fun footprintRing(ring: List<LatLng>, center: LatLng): List<LatLng> {
        if (ring.size < 3) return emptyList()
        val unwrapped = ArrayList<LatLng>(ring.size + 3)
        var previousLon = nearest(ring.first().longitude, center.longitude)
        unwrapped.add(LatLng(ring.first().latitude, previousLon))
        for (i in 1 until ring.size) {
            val lon = nearest(ring[i].longitude, previousLon)
            unwrapped.add(LatLng(ring[i].latitude, lon))
            previousLon = lon
        }

        val first = unwrapped.first()
        val last = unwrapped.last()
        // Net longitude swept going all the way round, including the closing edge last -> first.
        val closingLon = nearest(first.longitude, last.longitude)
        val sweep = closingLon - first.longitude
        if (abs(sweep) > 180.0) {
            // Finish the circle's own last edge (to the first point's 360°-shifted copy), then run
            // up to the pole side, straight back across the full 360°, and down to the start.
            val poleLat = (if (center.latitude == 0.0) 1.0 else sign(center.latitude)) * MAX_MERCATOR_LATITUDE
            unwrapped.add(LatLng(first.latitude, closingLon))
            unwrapped.add(LatLng(poleLat, closingLon))
            unwrapped.add(LatLng(poleLat, first.longitude))
        }
        unwrapped.add(first)
        return unwrapped
    }

    /** [lon] shifted by a multiple of 360° to lie within 180° of [reference]. */
    private fun nearest(lon: Double, reference: Double): Double {
        var result = lon
        while (result - reference > 180.0) result -= 360.0
        while (result - reference < -180.0) result += 360.0
        return result
    }
}
