package com.sattrakk.app.ui.map

import com.sattrakk.app.domain.model.LatLng
import com.sattrakk.app.domain.util.GeoUtils
import org.junit.Assert.assertEquals
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
}
