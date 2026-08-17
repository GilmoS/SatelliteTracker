package com.sattrakk.app

import com.sattrakk.app.core.ObserverLocation
import org.junit.Assert.assertEquals
import org.junit.Test

class ObserverLocationTest {
    @Test
    fun `observer location matches Ben Gurion Airport coordinates`() {
        assertEquals(32.0055, ObserverLocation.LATITUDE, 0.0001)
        assertEquals(34.8854, ObserverLocation.LONGITUDE, 0.0001)
        assertEquals(135.0, ObserverLocation.ALTITUDE_METERS, 0.0001)
    }
}
