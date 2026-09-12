package com.sattrakk.app.domain.util

import com.sattrakk.app.domain.model.Pass
import java.time.OffsetDateTime
import org.junit.Assert.assertEquals
import org.junit.Test

// Covers the "exclude past AOS" utility shared by DashboardViewModel and FullPassListViewModel —
// see android/CLAUDE.md and PassFilters.kt's own doc comment for why this exists and why the
// boundary (aos == now) is treated as inclusive/still-upcoming.
class PassFiltersTest {

    private val now: OffsetDateTime = OffsetDateTime.parse("2026-09-12T12:00:00Z")

    private fun pass(id: String, aos: OffsetDateTime) = Pass(
        id = id,
        satelliteId = "sat-1",
        tleId = "tle-$id",
        orbitNumber = 1,
        aos = aos,
        los = aos.plusMinutes(5),
        maxElevation = 45.0,
        aosAzimuth = 10.0,
        losAzimuth = 20.0,
        durationSec = 300,
        notify = true,
        outlookSynced = false,
        calculatedAt = now
    )

    @Test
    fun `a pass with AOS in the past is excluded`() {
        val past = pass("past", now.minusMinutes(1))

        assertEquals(emptyList<Pass>(), listOf(past).excludePastAos(now))
    }

    @Test
    fun `a pass with AOS in the future is included`() {
        val future = pass("future", now.plusMinutes(1))

        assertEquals(listOf(future), listOf(future).excludePastAos(now))
    }

    @Test
    fun `a pass with AOS exactly at now is included (inclusive boundary)`() {
        val atNow = pass("at-now", now)

        assertEquals(listOf(atNow), listOf(atNow).excludePastAos(now))
    }

    @Test
    fun `mixed list keeps only future and exactly-now passes, preserving order`() {
        val past = pass("past", now.minusMinutes(5))
        val atNow = pass("at-now", now)
        val soon = pass("soon", now.plusMinutes(1))
        val later = pass("later", now.plusMinutes(30))

        val result = listOf(past, atNow, soon, later).excludePastAos(now)

        assertEquals(listOf(atNow, soon, later), result)
    }
}
