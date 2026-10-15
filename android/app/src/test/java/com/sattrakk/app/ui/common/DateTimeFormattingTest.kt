package com.sattrakk.app.ui.common

import java.time.OffsetDateTime
import java.time.ZoneId
import org.junit.Assert.assertEquals
import org.junit.Test

// Uses a fixed test zone (America/New_York, UTC-4 in September under DST) rather than the test
// runner's actual system timezone, so this stays deterministic regardless of where it runs — see
// android/CLAUDE.md's "Timezone conversion" section.
class DateTimeFormattingTest {

    private val testZone: ZoneId = ZoneId.of("America/New_York")
    private val utc: OffsetDateTime = OffsetDateTime.parse("2026-09-12T14:30:00Z")

    @Test
    fun `formatTimeLocal converts a UTC instant to the given zone's wall-clock time`() {
        // 14:30 UTC - 4h (America/New_York, EDT in September) = 10:30
        assertEquals("10:30", formatTimeLocal(utc, testZone))
    }

    @Test
    fun `formatTimeUtc always renders the UTC wall-clock time regardless of the source offset`() {
        val sameInstantInTestZone = utc.atZoneSameInstant(testZone).toOffsetDateTime()

        assertEquals("14:30", formatTimeUtc(sameInstantInTestZone))
    }

    @Test
    fun `formatDateLocal converts to the given zone's calendar date`() {
        // Just after UTC midnight, still the previous calendar day in America/New_York.
        val nearMidnightUtc = OffsetDateTime.parse("2026-09-12T02:00:00Z")

        assertEquals("Fri, Sep 11 2026", formatDateLocal(nearMidnightUtc, testZone))
    }
}
