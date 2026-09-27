package com.sattrakk.app.ui.map

import com.sattrakk.app.domain.model.LatLng
import com.sattrakk.app.domain.model.TrackPoint
import com.sattrakk.app.domain.util.GeoUtils
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import kotlin.math.abs

class MapGeometryTest {

    // ---- splitAtAntimeridian ----

    @Test
    fun `track that never crosses the antimeridian stays one segment`() {
        val track = listOf(LatLng(30.0, 30.0), LatLng(31.0, 33.0), LatLng(32.0, 36.0))

        assertEquals(listOf(track), MapGeometry.splitAtAntimeridian(track))
    }

    @Test
    fun `eastward crossing splits into two segments meeting at an interpolated latitude`() {
        val track = listOf(LatLng(10.0, 178.0), LatLng(20.0, -178.0))

        val segments = MapGeometry.splitAtAntimeridian(track)

        assertEquals(2, segments.size)
        // Halfway in longitude (178 -> 182), so halfway in latitude.
        assertEquals(listOf(LatLng(10.0, 178.0), LatLng(15.0, 180.0)), segments[0])
        assertEquals(listOf(LatLng(15.0, -180.0), LatLng(20.0, -178.0)), segments[1])
    }

    @Test
    fun `westward crossing ends the first segment on the -180 edge`() {
        val track = listOf(LatLng(0.0, -179.0), LatLng(-3.0, 179.0))

        val segments = MapGeometry.splitAtAntimeridian(track)

        assertEquals(2, segments.size)
        assertEquals(LatLng(-1.5, -180.0), segments[0].last())
        assertEquals(LatLng(-1.5, 180.0), segments[1].first())
    }

    @Test
    fun `no segment ever contains a jump of more than 180 degrees`() {
        val track = (0..40).map { i -> LatLng(i - 20.0, ((170.0 + i + 180.0) % 360.0) - 180.0) }

        MapGeometry.splitAtAntimeridian(track).forEach { segment ->
            assertTrue(segment.size >= 2)
            segment.zipWithNext().forEach { (a, b) -> assertTrue(abs(b.longitude - a.longitude) <= 180.0) }
        }
    }

    @Test
    fun `fewer than two points yields no segments`() {
        assertTrue(MapGeometry.splitAtAntimeridian(emptyList()).isEmpty())
        assertTrue(MapGeometry.splitAtAntimeridian(listOf(LatLng(1.0, 2.0))).isEmpty())
    }

    // ---- footprintRing ----

    @Test
    fun `ordinary footprint is closed and unchanged apart from closing`() {
        val center = LatLng(31.5, 34.8)
        val ring = GeoUtils.footprintPolygon(center)

        val closed = MapGeometry.footprintRing(ring, center)

        assertEquals(ring.size + 1, closed.size)
        assertEquals(closed.first(), closed.last())
        assertEquals(ring, closed.dropLast(1))
    }

    @Test
    fun `footprint across the antimeridian is continuous around the center`() {
        val center = LatLng(-10.0, 179.5)
        val closed = MapGeometry.footprintRing(GeoUtils.footprintPolygon(center), center)

        assertEquals(closed.first(), closed.last())
        closed.zipWithNext().forEach { (a, b) -> assertTrue(abs(b.longitude - a.longitude) < 180.0) }
        closed.forEach { assertTrue(abs(it.longitude - center.longitude) <= 180.0) }
    }

    @Test
    fun `footprint enclosing a pole is closed along the mercator limit on that side`() {
        val center = LatLng(85.0, 10.0)
        val ring = GeoUtils.footprintPolygon(center)

        val closed = MapGeometry.footprintRing(ring, center)

        // ring + the first point's 360°-shifted copy + 2 cap points + closing point.
        assertEquals(ring.size + 4, closed.size)
        assertEquals(closed.first(), closed.last())
        val circleEnd = closed[ring.size]
        assertEquals(closed.first().latitude, circleEnd.latitude, 0.0)
        assertEquals(360.0, abs(circleEnd.longitude - closed.first().longitude), 1e-6)
        val capPoints = closed.subList(ring.size + 1, ring.size + 3)
        capPoints.forEach { assertEquals(MapGeometry.MAX_MERCATOR_LATITUDE, it.latitude, 0.0) }
        // The cap edge itself deliberately sweeps the full 360° along the top; every edge of the
        // actual footprint circle (including its own closing edge) must still be continuous.
        closed.take(ring.size + 1).zipWithNext().forEach { (a, b) -> assertTrue(abs(b.longitude - a.longitude) < 180.0) }
        assertEquals(360.0, abs(capPoints[1].longitude - capPoints[0].longitude), 1e-6)
    }

    @Test
    fun `southern polar footprint closes toward the south`() {
        val center = LatLng(-86.0, -120.0)
        val closed = MapGeometry.footprintRing(GeoUtils.footprintPolygon(center), center)

        assertTrue(closed.any { it.latitude == -MapGeometry.MAX_MERCATOR_LATITUDE })
    }

    @Test
    fun `degenerate ring yields nothing`() {
        assertTrue(MapGeometry.footprintRing(listOf(LatLng(0.0, 0.0), LatLng(1.0, 1.0)), LatLng(0.0, 0.0)).isEmpty())
    }

    // ---- headingDegrees ----

    private fun point(lat: Double, lon: Double, seconds: Long) = TrackPoint(lat, lon, 500.0, seconds * 1000)

    @Test
    fun `heading follows the segment whose time span contains the timestamp`() {
        // Northbound, then eastbound from t = 60 s.
        val track = listOf(point(0.0, 10.0, 0), point(1.0, 10.0, 30), point(2.0, 10.0, 60), point(2.0, 11.0, 90))

        assertEquals(0.0, MapGeometry.headingDegrees(45_000, track)!!, 1e-6)
        assertEquals(0.0, MapGeometry.headingDegrees(30_000, track)!!, 1e-6)
        assertEquals(90.0, MapGeometry.headingDegrees(75_000, track)!!, 0.1)
    }

    @Test
    fun `heading matches by time, not by nearest point, where the orbits cross`() {
        // Past segment runs north through (0, 0); the future segment later runs east through the
        // same spot. At t = 100 s the satellite is on the eastbound segment, even though a
        // northbound point sits exactly at the crossing too.
        val track = listOf(
            point(-1.0, 0.0, 0), point(0.0, 0.0, 30), point(1.0, 0.0, 60),
            point(0.0, -1.0, 90), point(0.0, 1.0, 120),
        )

        assertEquals(90.0, MapGeometry.headingDegrees(100_000, track)!!, 1e-6)
    }

    @Test
    fun `heading outside the track uses the first or last segment`() {
        // Westbound track.
        val track = listOf(point(0.0, 12.0, 0), point(0.0, 11.0, 30), point(0.0, 10.0, 60))

        assertEquals(270.0, MapGeometry.headingDegrees(-10_000, track)!!, 1e-6)
        assertEquals(270.0, MapGeometry.headingDegrees(90_000, track)!!, 1e-6)
    }

    @Test
    fun `heading is null without a usable segment`() {
        assertNull(MapGeometry.headingDegrees(0, listOf(point(0.0, 0.0, 0))))
        assertNull(MapGeometry.headingDegrees(0, listOf(point(1.0, 1.0, 0), point(1.0, 1.0, 30))))
    }

    // ---- splitAtTime ----

    @Test
    fun `split joins the flown and upcoming parts at the marker`() {
        val track = listOf(point(0.0, 0.0, 0), point(1.0, 0.0, 30), point(2.0, 0.0, 60), point(3.0, 0.0, 90))
        val marker = LatLng(1.5, 0.0)

        val (past, future) = MapGeometry.splitAtTime(track, 45_000, marker, pastTailMillis = 600_000)

        assertEquals(listOf(LatLng(0.0, 0.0), LatLng(1.0, 0.0), marker), past)
        assertEquals(listOf(marker, LatLng(2.0, 0.0), LatLng(3.0, 0.0)), future)
    }

    @Test
    fun `split counts a point at exactly the timestamp as flown`() {
        val track = listOf(point(0.0, 0.0, 0), point(1.0, 0.0, 30))

        val (past, future) = MapGeometry.splitAtTime(track, 30_000, LatLng(1.0, 0.0), pastTailMillis = 600_000)

        assertEquals(3, past.size)
        assertEquals(listOf(LatLng(1.0, 0.0)), future)
    }

    @Test
    fun `split keeps only the last tail of the flown part and all of the part ahead`() {
        // Points every 30 s from t = 0 to t = 300 s; "now" is t = 200 s, tail is 60 s.
        val track = (0..10).map { point(it.toDouble(), 0.0, it * 30L) }
        val marker = LatLng(6.5, 0.0)

        val (past, future) = MapGeometry.splitAtTime(track, 200_000, marker, pastTailMillis = 60_000)

        // Only t = 150 s and 180 s fall in [140 s, 200 s]; the tail ends at the marker.
        assertEquals(listOf(LatLng(5.0, 0.0), LatLng(6.0, 0.0), marker), past)
        assertEquals(listOf(marker) + (7..10).map { LatLng(it.toDouble(), 0.0) }, future)
    }
}
