package com.sattrakk.app.ui.skyview

import android.content.Context
import android.hardware.Sensor
import android.hardware.SensorManager
import com.google.ar.core.ArCoreApk
import com.google.ar.core.ArCoreApk.Availability
import com.sattrakk.app.MainDispatcherRule
import com.sattrakk.app.data.device.AndroidArCompatibilityChecker
import com.sattrakk.app.domain.model.ArCoreCompatibility
import io.mockk.every
import io.mockk.mockk
import io.mockk.verify
import kotlinx.coroutines.ExperimentalCoroutinesApi
import org.junit.Assert.assertEquals
import org.junit.Rule
import org.junit.Test
import java.util.function.Consumer

// Drives SkyViewViewModel over the real AndroidArCompatibilityChecker, with ArCoreApk
// (checkAvailabilityAsync) and SensorManager mocked, so each device combination is exercised from
// the SDK/system boundary up to the SkyViewUiState.Result the screen renders. No long-lived
// coroutine here, but the scheduler is still driven directly (runCurrent) per the project's
// ViewModel test convention (see DashboardViewModelTest).
@OptIn(ExperimentalCoroutinesApi::class)
class SkyViewViewModelTest {

    @get:Rule
    val mainDispatcherRule = MainDispatcherRule()

    private val context = mockk<Context>()
    private val arCoreApk = mockk<ArCoreApk>()
    private val sensorManager = mockk<SensorManager>()

    private fun runCurrent() = mainDispatcherRule.testDispatcher.scheduler.runCurrent()

    private fun givenArCore(availability: Availability) {
        every { arCoreApk.checkAvailabilityAsync(any(), any()) } answers {
            secondArg<Consumer<Availability>>().accept(availability)
        }
    }

    private fun givenRotationVectorSensor(present: Boolean) {
        every { sensorManager.getDefaultSensor(Sensor.TYPE_ROTATION_VECTOR) } returns
            if (present) mockk<Sensor>() else null
    }

    private fun createViewModel() =
        SkyViewViewModel(AndroidArCompatibilityChecker(context, arCoreApk, sensorManager))

    private fun checkedResult(): SkyViewUiState.Result {
        val viewModel = createViewModel()
        viewModel.checkCompatibility()
        runCurrent()
        return viewModel.uiState.value as SkyViewUiState.Result
    }

    @Test
    fun `initial state is Checking and nothing is queried until the screen asks`() {
        givenArCore(Availability.SUPPORTED_INSTALLED)
        givenRotationVectorSensor(present = true)

        val viewModel = createViewModel()
        runCurrent()

        assertEquals(SkyViewUiState.Checking, viewModel.uiState.value)
        verify(exactly = 0) { arCoreApk.checkAvailabilityAsync(any(), any()) }
    }

    @Test
    fun `ARCore installed and rotation vector present - fully supported`() {
        givenArCore(Availability.SUPPORTED_INSTALLED)
        givenRotationVectorSensor(present = true)

        val result = checkedResult()

        assertEquals(SkyViewUiState.Result(ArCoreCompatibility.SUPPORTED, hasRotationVectorSensor = true), result)
        assertEquals(SkyViewSupport.SUPPORTED, result.support)
    }

    @Test
    fun `ARCore APK missing on a supported device - install message`() {
        givenArCore(Availability.SUPPORTED_NOT_INSTALLED)
        givenRotationVectorSensor(present = true)

        val result = checkedResult()

        assertEquals(ArCoreCompatibility.SUPPORTED_APK_NOT_INSTALLED, result.arCoreStatus)
        assertEquals(SkyViewSupport.ARCORE_NOT_INSTALLED, result.support)
    }

    @Test
    fun `ARCore APK too old on a supported device - update message`() {
        givenArCore(Availability.SUPPORTED_APK_TOO_OLD)
        givenRotationVectorSensor(present = true)

        val result = checkedResult()

        assertEquals(ArCoreCompatibility.SUPPORTED_APK_TOO_OLD, result.arCoreStatus)
        assertEquals(SkyViewSupport.ARCORE_TOO_OLD, result.support)
    }

    @Test
    fun `rotation vector sensor absent - unsupported even though ARCore is installed`() {
        givenArCore(Availability.SUPPORTED_INSTALLED)
        givenRotationVectorSensor(present = false)

        val result = checkedResult()

        assertEquals(SkyViewUiState.Result(ArCoreCompatibility.SUPPORTED, hasRotationVectorSensor = false), result)
        assertEquals(SkyViewSupport.UNSUPPORTED_DEVICE, result.support)
    }

    @Test
    fun `sensor absent wins over an ARCore APK that could be installed`() {
        givenArCore(Availability.SUPPORTED_NOT_INSTALLED)
        givenRotationVectorSensor(present = false)

        assertEquals(SkyViewSupport.UNSUPPORTED_DEVICE, checkedResult().support)
    }

    @Test
    fun `ARCore device not capable - unsupported even with the sensor`() {
        givenArCore(Availability.UNSUPPORTED_DEVICE_NOT_CAPABLE)
        givenRotationVectorSensor(present = true)

        val result = checkedResult()

        assertEquals(ArCoreCompatibility.UNSUPPORTED, result.arCoreStatus)
        assertEquals(SkyViewSupport.UNSUPPORTED_DEVICE, result.support)
    }

    @Test
    fun `device fully unsupported - neither ARCore nor the sensor`() {
        givenArCore(Availability.UNSUPPORTED_DEVICE_NOT_CAPABLE)
        givenRotationVectorSensor(present = false)

        val result = checkedResult()

        assertEquals(SkyViewUiState.Result(ArCoreCompatibility.UNSUPPORTED, hasRotationVectorSensor = false), result)
        assertEquals(SkyViewSupport.UNSUPPORTED_DEVICE, result.support)
    }

    @Test
    fun `no SensorManager at all counts as no rotation vector sensor`() {
        givenArCore(Availability.SUPPORTED_INSTALLED)
        val viewModel = SkyViewViewModel(AndroidArCompatibilityChecker(context, arCoreApk, sensorManager = null))

        viewModel.checkCompatibility()
        runCurrent()

        val result = viewModel.uiState.value as SkyViewUiState.Result
        assertEquals(false, result.hasRotationVectorSensor)
        assertEquals(SkyViewSupport.UNSUPPORTED_DEVICE, result.support)
    }

    @Test
    fun `ARCore check error - undetermined, not unsupported`() {
        givenArCore(Availability.UNKNOWN_ERROR)
        givenRotationVectorSensor(present = true)

        val result = checkedResult()

        assertEquals(ArCoreCompatibility.UNKNOWN, result.arCoreStatus)
        assertEquals(SkyViewSupport.UNDETERMINED, result.support)
    }

    @Test
    fun `each check re-queries the device, so an ARCore install between visits is picked up`() {
        givenArCore(Availability.SUPPORTED_NOT_INSTALLED)
        givenRotationVectorSensor(present = true)
        val viewModel = createViewModel()

        viewModel.checkCompatibility()
        runCurrent()
        assertEquals(SkyViewSupport.ARCORE_NOT_INSTALLED, (viewModel.uiState.value as SkyViewUiState.Result).support)

        givenArCore(Availability.SUPPORTED_INSTALLED)
        viewModel.checkCompatibility()
        runCurrent()

        assertEquals(SkyViewSupport.SUPPORTED, (viewModel.uiState.value as SkyViewUiState.Result).support)
        verify(exactly = 2) { arCoreApk.checkAvailabilityAsync(any(), any()) }
        verify(exactly = 2) { sensorManager.getDefaultSensor(Sensor.TYPE_ROTATION_VECTOR) }
    }
}
