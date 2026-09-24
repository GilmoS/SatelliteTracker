package com.sattrakk.app.ui.map

import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider
import androidx.lifecycle.ViewModelStore
import com.sattrakk.app.MainDispatcherRule
import com.sattrakk.app.data.repository.MapRepository
import com.sattrakk.app.data.repository.PassRepository
import com.sattrakk.app.data.repository.SatelliteRepository
import com.sattrakk.app.domain.model.ApiResult
import com.sattrakk.app.domain.model.LatLng
import com.sattrakk.app.domain.model.Pass
import com.sattrakk.app.domain.model.PassTrack
import com.sattrakk.app.domain.model.PassTrackPoint
import com.sattrakk.app.domain.model.Satellite
import com.sattrakk.app.domain.model.SatellitePosition
import com.sattrakk.app.domain.model.TrackPoint
import com.sattrakk.app.domain.util.GeoUtils
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.mockk
import java.time.OffsetDateTime
import java.time.ZoneOffset
import kotlinx.coroutines.ExperimentalCoroutinesApi
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test

// Same structure as DashboardViewModelTest (see its header comment): the polling loops never finish
// on their own, so there's no runTest — each test drives the Main TestDispatcher's scheduler
// directly. Unlike that test, polling cancellation IS covered here: the ViewModel is obtained
// through a real ViewModelStore, whose public clear() runs ViewModel.clear()/onCleared() and
// cancels viewModelScope.
@OptIn(ExperimentalCoroutinesApi::class)
class MapViewModelTest {

    @get:Rule
    val mainDispatcherRule = MainDispatcherRule()

    private val mapRepository = mockk<MapRepository>()
    private val passRepository = mockk<PassRepository>()
    private val satelliteRepository = mockk<SatelliteRepository>()
    private val store = ViewModelStore()

    private val satelliteId = "sat-1"
    private val passId = "pass-1"
    private val epoch = OffsetDateTime.of(2026, 9, 24, 12, 0, 0, 0, ZoneOffset.UTC)

    private fun runCurrent() = mainDispatcherRule.testDispatcher.scheduler.runCurrent()
    private fun advanceTimeBy(millis: Long) {
        mainDispatcherRule.testDispatcher.scheduler.advanceTimeBy(millis)
        runCurrent()
    }

    private fun createViewModel(args: Map<String, Any?>): MapViewModel {
        val factory = object : ViewModelProvider.Factory {
            @Suppress("UNCHECKED_CAST")
            override fun <T : ViewModel> create(modelClass: Class<T>): T =
                MapViewModel(mapRepository, passRepository, satelliteRepository, SavedStateHandle(args)) as T
        }
        val vm = ViewModelProvider(store, factory)[MapViewModel::class.java]
        runCurrent()
        return vm
    }

    private fun position(lat: Double) = SatellitePosition(lat, 34.8, 500.0, 100.0, 20.0, 0L)

    private fun pass(id: String) = Pass(
        id = id, satelliteId = satelliteId, tleId = "tle", orbitNumber = 1,
        aos = epoch, los = epoch.plusMinutes(10), maxElevation = 45.0, aosAzimuth = 10.0,
        losAzimuth = 200.0, durationSec = 600, notify = true, outlookSynced = false, calculatedAt = epoch
    )

    private val drawer = listOf(pass("drawer-1"), pass("drawer-2"))

    private val otherSatelliteId = "sat-2"
    private val catalog = listOf(
        Satellite(satelliteId, "EROS C3", 1, null, true, true, epoch),
        Satellite(otherSatelliteId, "RUNNER 1", 2, null, true, false, epoch),
    )
    private val catalogNames = mapOf(satelliteId to "EROS C3", otherSatelliteId to "RUNNER 1")

    init {
        // Both flows look up the catalog (Flow 2 only for the drawer's names); individual tests
        // override this where they need a failure or an empty catalog.
        coEvery { satelliteRepository.getSatellites() } returns ApiResult.Success(catalog)
    }

    private fun stubLiveSuccess() {
        coEvery { satelliteRepository.getSatellites() } returns ApiResult.Success(catalog)
        coEvery { mapRepository.getPosition(satelliteId) } returns ApiResult.Success(position(31.5))
        coEvery { mapRepository.getLiveTrack(satelliteId) } returns
            ApiResult.Success(listOf(TrackPoint(31.5, 34.8, 500.0, 0L)))
        coEvery { mapRepository.getNotifyEnabledPasses() } returns drawer
    }

    // --- Flow 2: passId present ---

    @Test
    fun `passId present loads StaticPassTrack with the drawer`() {
        val points = listOf(PassTrackPoint(1.0, 2.0, 500.0, 0L))
        coEvery { passRepository.getPassTrack(passId) } returns ApiResult.Success(PassTrack(passId, points))
        coEvery { passRepository.getPassById(passId) } returns ApiResult.Success(pass(passId))
        coEvery { mapRepository.getNotifyEnabledPasses() } returns drawer

        val vm = createViewModel(mapOf("passId" to passId, "satelliteId" to satelliteId))

        val state = vm.uiState.value as MapUiState.StaticPassTrack
        assertEquals(passId, state.pass.id)
        assertEquals(points, state.trackPoints)
        assertEquals(drawer, state.notifyEnabledPasses)
        assertEquals(catalogNames, state.satelliteNames)
    }

    @Test
    fun `passId present resolves drawer names for every satellite, not just the pass's own`() {
        val otherPass = pass("drawer-other").copy(satelliteId = otherSatelliteId)
        coEvery { passRepository.getPassTrack(passId) } returns ApiResult.Success(PassTrack(passId, emptyList()))
        coEvery { passRepository.getPassById(passId) } returns ApiResult.Success(pass(passId))
        coEvery { mapRepository.getNotifyEnabledPasses() } returns drawer + otherPass

        val vm = createViewModel(mapOf("passId" to passId))

        val state = vm.uiState.value as MapUiState.StaticPassTrack
        assertEquals("RUNNER 1", state.satelliteNames[otherPass.satelliteId])
        assertEquals("EROS C3", state.satelliteNames[state.pass.satelliteId])
    }

    @Test
    fun `passId present with a failed catalog lookup still shows the track, with no names`() {
        val points = listOf(PassTrackPoint(1.0, 2.0, 500.0, 0L))
        coEvery { passRepository.getPassTrack(passId) } returns ApiResult.Success(PassTrack(passId, points))
        coEvery { passRepository.getPassById(passId) } returns ApiResult.Success(pass(passId))
        coEvery { mapRepository.getNotifyEnabledPasses() } returns drawer
        coEvery { satelliteRepository.getSatellites() } returns ApiResult.NetworkError

        val vm = createViewModel(mapOf("passId" to passId))

        val state = vm.uiState.value as MapUiState.StaticPassTrack
        assertEquals(points, state.trackPoints)
        assertEquals(drawer, state.notifyEnabledPasses)
        assertTrue(state.satelliteNames.isEmpty())
    }

    @Test
    fun `passId present never polls or touches live endpoints over time`() {
        coEvery { passRepository.getPassTrack(passId) } returns ApiResult.Success(PassTrack(passId, emptyList()))
        coEvery { passRepository.getPassById(passId) } returns ApiResult.Success(pass(passId))
        coEvery { mapRepository.getNotifyEnabledPasses() } returns drawer

        createViewModel(mapOf("passId" to passId))
        advanceTimeBy(MapViewModel.TRACK_POLL_INTERVAL_MILLIS * 3)

        coVerify(exactly = 1) { passRepository.getPassTrack(passId) }
        coVerify(exactly = 0) { mapRepository.getPosition(any()) }
        coVerify(exactly = 0) { mapRepository.getLiveTrack(any()) }
    }

    @Test
    fun `passId present with a failed track is an Error`() {
        coEvery { passRepository.getPassTrack(passId) } returns ApiResult.NetworkError
        coEvery { passRepository.getPassById(passId) } returns ApiResult.Success(pass(passId))
        coEvery { mapRepository.getNotifyEnabledPasses() } returns drawer

        val vm = createViewModel(mapOf("passId" to passId))

        assertEquals(MapUiState.Error("No network connection."), vm.uiState.value)
    }

    @Test
    fun `passId present with a failed pass lookup is an Error`() {
        coEvery { passRepository.getPassTrack(passId) } returns ApiResult.Success(PassTrack(passId, emptyList()))
        coEvery { passRepository.getPassById(passId) } returns ApiResult.Error(404, "Pass not found")
        coEvery { mapRepository.getNotifyEnabledPasses() } returns drawer

        val vm = createViewModel(mapOf("passId" to passId))

        assertEquals(MapUiState.Error("Pass not found"), vm.uiState.value)
    }

    // --- Flow 1: passId absent ---

    @Test
    fun `passId absent loads LiveTrack with footprint and the drawer`() {
        stubLiveSuccess()

        val vm = createViewModel(mapOf("satelliteId" to satelliteId))

        val state = vm.uiState.value as MapUiState.LiveTrack
        assertEquals("EROS C3", state.satelliteName)
        assertEquals(31.5, state.currentPosition.latitude, 0.0)
        assertEquals(1, state.trackPoints.size)
        assertEquals(drawer, state.notifyEnabledPasses)
        assertEquals(GeoUtils.footprintPolygon(LatLng(31.5, 34.8)), state.footprintPolygon)
        // Whole catalog, from the same single getSatellites() call used for satelliteName.
        assertEquals(catalogNames, state.satelliteNames)
        coVerify(exactly = 1) { satelliteRepository.getSatellites() }
    }

    @Test
    fun `position polls every 15s and recomputes the footprint`() {
        stubLiveSuccess()
        val vm = createViewModel(mapOf("satelliteId" to satelliteId))
        coVerify(exactly = 1) { mapRepository.getPosition(satelliteId) }

        coEvery { mapRepository.getPosition(satelliteId) } returns ApiResult.Success(position(40.0))
        advanceTimeBy(MapViewModel.POSITION_POLL_INTERVAL_MILLIS - 1)
        coVerify(exactly = 1) { mapRepository.getPosition(satelliteId) }

        advanceTimeBy(1)
        coVerify(exactly = 2) { mapRepository.getPosition(satelliteId) }
        val state = vm.uiState.value as MapUiState.LiveTrack
        assertEquals(40.0, state.currentPosition.latitude, 0.0)
        assertEquals(GeoUtils.footprintPolygon(LatLng(40.0, 34.8)), state.footprintPolygon)

        advanceTimeBy(MapViewModel.POSITION_POLL_INTERVAL_MILLIS * 3)
        coVerify(exactly = 5) { mapRepository.getPosition(satelliteId) }
    }

    @Test
    fun `live track refreshes only every 5 minutes, not on the 15s cadence`() {
        stubLiveSuccess()
        createViewModel(mapOf("satelliteId" to satelliteId))

        advanceTimeBy(MapViewModel.TRACK_POLL_INTERVAL_MILLIS - 1)
        coVerify(exactly = 1) { mapRepository.getLiveTrack(satelliteId) }

        advanceTimeBy(1)
        coVerify(exactly = 2) { mapRepository.getLiveTrack(satelliteId) }
    }

    @Test
    fun `a failed position poll keeps the last good LiveTrack and keeps polling`() {
        stubLiveSuccess()
        val vm = createViewModel(mapOf("satelliteId" to satelliteId))

        coEvery { mapRepository.getPosition(satelliteId) } returns ApiResult.NetworkError
        advanceTimeBy(MapViewModel.POSITION_POLL_INTERVAL_MILLIS)
        assertEquals(31.5, (vm.uiState.value as MapUiState.LiveTrack).currentPosition.latitude, 0.0)

        coEvery { mapRepository.getPosition(satelliteId) } returns ApiResult.Success(position(35.0))
        advanceTimeBy(MapViewModel.POSITION_POLL_INTERVAL_MILLIS)
        assertEquals(35.0, (vm.uiState.value as MapUiState.LiveTrack).currentPosition.latitude, 0.0)
    }

    @Test
    fun `polling stops once the ViewModel is cleared`() {
        stubLiveSuccess()
        createViewModel(mapOf("satelliteId" to satelliteId))
        advanceTimeBy(MapViewModel.POSITION_POLL_INTERVAL_MILLIS)
        coVerify(exactly = 2) { mapRepository.getPosition(satelliteId) }

        store.clear()
        advanceTimeBy(MapViewModel.TRACK_POLL_INTERVAL_MILLIS * 2)

        coVerify(exactly = 2) { mapRepository.getPosition(satelliteId) }
        coVerify(exactly = 1) { mapRepository.getLiveTrack(satelliteId) }
    }

    @Test
    fun `initial live failure is an Error and nothing polls`() {
        stubLiveSuccess()
        coEvery { mapRepository.getPosition(satelliteId) } returns ApiResult.Error(500, "N2YO unavailable")

        val vm = createViewModel(mapOf("satelliteId" to satelliteId))
        advanceTimeBy(MapViewModel.TRACK_POLL_INTERVAL_MILLIS)

        assertEquals(MapUiState.Error("N2YO unavailable"), vm.uiState.value)
        coVerify(exactly = 1) { mapRepository.getPosition(satelliteId) }
    }

    @Test
    fun `missing satelliteId in the live flow is an Error without any calls`() {
        coEvery { mapRepository.getNotifyEnabledPasses() } returns drawer

        val vm = createViewModel(emptyMap())

        assertTrue(vm.uiState.value is MapUiState.Error)
        coVerify(exactly = 0) { mapRepository.getPosition(any()) }
    }

    @Test
    fun `satellite missing from the catalog is an Error`() {
        stubLiveSuccess()
        coEvery { satelliteRepository.getSatellites() } returns ApiResult.Success(emptyList())

        val vm = createViewModel(mapOf("satelliteId" to satelliteId))

        assertEquals(MapUiState.Error("Unknown satellite."), vm.uiState.value)
    }
}
