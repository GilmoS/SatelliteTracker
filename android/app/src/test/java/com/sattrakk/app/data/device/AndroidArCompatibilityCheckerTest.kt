package com.sattrakk.app.data.device

import android.content.Context
import com.google.ar.core.ArCoreApk
import com.google.ar.core.ArCoreApk.Availability
import com.sattrakk.app.domain.model.ArCoreCompatibility
import io.mockk.every
import io.mockk.mockk
import io.mockk.verify
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.test.currentTime
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Test
import java.util.function.Consumer

@OptIn(ExperimentalCoroutinesApi::class)
class AndroidArCompatibilityCheckerTest {

    private val context = mockk<Context>()
    private val arCoreApk = mockk<ArCoreApk>()
    private val checker = AndroidArCompatibilityChecker(context, arCoreApk, sensorManager = null)

    private fun givenArCoreSequence(vararg results: Availability) {
        val remaining = ArrayDeque(results.toList())
        every { arCoreApk.checkAvailabilityAsync(any(), any()) } answers {
            val next = if (remaining.size > 1) remaining.removeFirst() else remaining.first()
            secondArg<Consumer<Availability>>().accept(next)
        }
    }

    @Test
    fun `every ArCoreApk Availability value maps to a compatibility case`() {
        val expected = mapOf(
            Availability.SUPPORTED_INSTALLED to ArCoreCompatibility.SUPPORTED,
            Availability.SUPPORTED_NOT_INSTALLED to ArCoreCompatibility.SUPPORTED_APK_NOT_INSTALLED,
            Availability.SUPPORTED_APK_TOO_OLD to ArCoreCompatibility.SUPPORTED_APK_TOO_OLD,
            Availability.UNSUPPORTED_DEVICE_NOT_CAPABLE to ArCoreCompatibility.UNSUPPORTED,
            Availability.UNKNOWN_ERROR to ArCoreCompatibility.UNKNOWN,
            Availability.UNKNOWN_TIMED_OUT to ArCoreCompatibility.UNKNOWN,
            Availability.UNKNOWN_CHECKING to ArCoreCompatibility.UNKNOWN,
        )
        // Guards against a future SDK adding a value this mapping silently doesn't cover.
        assertEquals(Availability.values().toSet(), expected.keys)
        expected.forEach { (availability, compatibility) ->
            assertEquals(availability.name, compatibility, availability.toCompatibility())
        }
    }

    @Test
    fun `UNKNOWN_CHECKING is re-queried until a final answer arrives`() = runTest {
        givenArCoreSequence(Availability.UNKNOWN_CHECKING, Availability.UNKNOWN_CHECKING, Availability.SUPPORTED_INSTALLED)

        assertEquals(ArCoreCompatibility.SUPPORTED, checker.checkArCore())
        verify(exactly = 3) { arCoreApk.checkAvailabilityAsync(context, any()) }
        assertEquals(2 * AndroidArCompatibilityChecker.TRANSIENT_RETRY_DELAY_MILLIS, currentTime)
    }

    @Test
    fun `a check stuck on UNKNOWN_CHECKING gives up as UNKNOWN`() = runTest {
        givenArCoreSequence(Availability.UNKNOWN_CHECKING)

        assertEquals(ArCoreCompatibility.UNKNOWN, checker.checkArCore())
        verify(exactly = AndroidArCompatibilityChecker.MAX_TRANSIENT_RETRIES) {
            arCoreApk.checkAvailabilityAsync(any(), any())
        }
    }

    @Test
    fun `a non-transient answer is returned without retrying`() = runTest {
        givenArCoreSequence(Availability.UNKNOWN_TIMED_OUT)

        assertEquals(ArCoreCompatibility.UNKNOWN, checker.checkArCore())
        verify(exactly = 1) { arCoreApk.checkAvailabilityAsync(any(), any()) }
    }
}
