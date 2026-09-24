package com.sattrakk.app.domain.util

import com.sattrakk.app.domain.model.LatLng
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class GeoUtilsTest {

    // Roughly central Israel — the app's observer region.
    private val israel = LatLng(31.5, 34.8)

    @Test
    fun `footprint has the requested number of points`() {
        assertEquals(72, GeoUtils.footprintPolygon(israel).size)
        assertEquals(64, GeoUtils.footprintPolygon(israel, points = 64).size)
    }

    @Test
    fun `every footprint point lies at the requested radius from the center`() {
        GeoUtils.footprintPolygon(israel, radiusKm = 2000.0).forEach { point ->
            assertEquals(2000.0, GeoUtils.distanceKm(israel, point), 0.01)
        }
    }

    @Test
    fun `radius holds near a pole and across the antimeridian`() {
        listOf(LatLng(80.0, 0.0), LatLng(-10.0, 179.5), LatLng(0.0, -179.9)).forEach { center ->
            GeoUtils.footprintPolygon(center, radiusKm = 1500.0).forEach { point ->
                assertEquals(1500.0, GeoUtils.distanceKm(center, point), 0.01)
            }
        }
    }

    @Test
    fun `first point is due north of the center`() {
        val north = GeoUtils.footprintPolygon(israel, radiusKm = 2000.0).first()

        assertEquals(israel.longitude, north.longitude, 1e-9)
        // 2000 km / 6371 km rad ≈ 17.986° of latitude along a meridian.
        assertEquals(israel.latitude + Math.toDegrees(2000.0 / GeoUtils.EARTH_MEAN_RADIUS_KM), north.latitude, 1e-9)
    }

    @Test
    fun `longitudes are normalized to the -180 to 180 range`() {
        GeoUtils.footprintPolygon(LatLng(0.0, 179.0), radiusKm = 2000.0).forEach { point ->
            assertTrue(point.longitude >= -180.0 && point.longitude < 180.0)
        }
    }

    @Test
    fun `distanceKm between identical points is zero and is symmetric`() {
        val other = LatLng(40.7, -74.0)

        assertEquals(0.0, GeoUtils.distanceKm(israel, israel), 1e-9)
        assertEquals(GeoUtils.distanceKm(israel, other), GeoUtils.distanceKm(other, israel), 1e-9)
    }
}
