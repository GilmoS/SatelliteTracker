package com.sattrakk.app.ui.dashboard

import com.sattrakk.app.MainDispatcherRule
import com.sattrakk.app.data.repository.PassRepository
import com.sattrakk.app.data.repository.SatelliteRepository
import com.sattrakk.app.domain.model.ApiResult
import com.sattrakk.app.domain.model.Pass
import com.sattrakk.app.domain.model.Satellite
import io.mockk.clearMocks
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.mockk
import java.time.Clock
import java.time.Duration
import java.time.Instant
import java.time.OffsetDateTime
import java.time.ZoneId
import java.time.ZoneOffset
import java.util.concurrent.TimeUnit
import kotlinx.coroutines.ExperimentalCoroutinesApi
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Rule
import org.junit.Test

// DashboardViewModel's polling/countdown coroutines run for the ViewModel's whole lifetime —
// viewModelScope is only ever cancelled by ViewModel.onCleared()/clear(), both internal/protected
// and unreachable from a plain unit test. That means these background `while (isActive) { ...;
// delay(x) }` loops never finish on their own. kotlinx-coroutines-test's `runTest { }` builder
// runs an implicit "drain to idle" pass at the end of the test body — even with no TestDispatcher
// explicitly passed to it — and once Dispatchers.Main has been redirected to a TestDispatcher via
// Dispatchers.setMain(...), that drain wound up processing Main's queue too, so it never reached
// idle (confirmed twice via jstack thread dumps: the test thread pegged at ~100% CPU forever
// inside TestCoroutineScheduler.advanceUntilIdleOr / TestMainDispatcher machinery). Since nothing
// in these test bodies is itself a suspend call — the ViewModel's own coroutines do the
// suspending, and MockK's coEvery/coVerify and reading uiState.value are plain synchronous calls
// — these tests don't use runTest at all. Instead they drive
// mainDispatcherRule.testDispatcher.scheduler directly via its plain (non-suspend) runCurrent()/
// advanceTimeBy() methods, so there is no runTest drain to ever get stuck.
@OptIn(ExperimentalCoroutinesApi::class)
class DashboardViewModelTest {

    @get:Rule
    val mainDispatcherRule = MainDispatcherRule()

    private val satelliteRepository = mockk<SatelliteRepository>()
    private val passRepository = mockk<PassRepository>()
    private lateinit var clock: Clock
    private lateinit var viewModel: DashboardViewModel

    private val baseInstant: Instant = Instant.parse("2026-08-28T00:00:00Z")
    private val POLL_INTERVAL_MILLIS = TimeUnit.MINUTES.toMillis(5)

    private fun runCurrent() = mainDispatcherRule.testDispatcher.scheduler.runCurrent()
    private fun advanceTimeBy(millis: Long) = mainDispatcherRule.testDispatcher.scheduler.advanceTimeBy(millis)

    @Before
    fun setUp() {
        // Backed by the same scheduler Dispatchers.Main (and therefore viewModelScope) runs on,
        // so "now" inside the countdown ticker advances in lockstep with the virtual time this
        // test drives via runCurrent()/advanceTimeBy() above, not the real wall clock.
        clock = object : Clock() {
            override fun getZone(): ZoneId = ZoneOffset.UTC
            override fun withZone(zone: ZoneId?): Clock = this
            override fun instant(): Instant = baseInstant.plusMillis(mainDispatcherRule.testDispatcher.scheduler.currentTime)
        }
    }

    private fun satellite(id: String, isDefault: Boolean = false) = Satellite(
        id = id,
        name = "Satellite-$id",
        noradId = 1,
        description = null,
        isActive = true,
        isDefault = isDefault,
        createdAt = OffsetDateTime.ofInstant(baseInstant, ZoneOffset.UTC)
    )

    private fun pass(id: String, satelliteId: String, aosOffsetSeconds: Long) = Pass(
        id = id,
        satelliteId = satelliteId,
        tleId = "tle-$id",
        orbitNumber = 1,
        aos = OffsetDateTime.ofInstant(baseInstant.plusSeconds(aosOffsetSeconds), ZoneOffset.UTC),
        los = OffsetDateTime.ofInstant(baseInstant.plusSeconds(aosOffsetSeconds + 300), ZoneOffset.UTC),
        maxElevation = 45.0,
        aosAzimuth = 10.0,
        losAzimuth = 20.0,
        durationSec = 300,
        notify = true,
        outlookSynced = false,
        calculatedAt = OffsetDateTime.ofInstant(baseInstant, ZoneOffset.UTC)
    )

    @Test
    fun `successful load builds Content with correct tabs and default selected satellite`() {
        val sat1 = satellite(id = "sat-1", isDefault = false)
        val sat2 = satellite(id = "sat-2", isDefault = true)
        val pass1 = pass(id = "pass-1", satelliteId = "sat-1", aosOffsetSeconds = 600)
        val pass2 = pass(id = "pass-2", satelliteId = "sat-2", aosOffsetSeconds = 300)
        coEvery { satelliteRepository.getSatellites() } returns ApiResult.Success(listOf(sat1, sat2))
        coEvery { passRepository.getPasses("sat-1", false) } returns ApiResult.Success(listOf(pass1))
        coEvery { passRepository.getPasses("sat-2", false) } returns ApiResult.Success(listOf(pass2))

        viewModel = DashboardViewModel(satelliteRepository, passRepository, clock)
        runCurrent()

        val state = viewModel.uiState.value
        assertTrue(state is DashboardUiState.Content)
        state as DashboardUiState.Content
        assertEquals("sat-2", state.selectedSatelliteId) // sat2.isDefault, not first-in-list
        assertEquals(2, state.tabs.size)
        val tab1 = state.tabs.first { it.satelliteId == "sat-1" }
        assertEquals(listOf(pass1), tab1.passes)
        assertNull(tab1.loadError)
    }

    @Test
    fun `satellites call failure produces Error state`() {
        coEvery { satelliteRepository.getSatellites() } returns ApiResult.Error(500, "boom")

        viewModel = DashboardViewModel(satelliteRepository, passRepository, clock)
        runCurrent()

        val state = viewModel.uiState.value
        assertTrue(state is DashboardUiState.Error)
        assertEquals("boom", (state as DashboardUiState.Error).message)
    }

    @Test
    fun `one satellite's passes call failing surfaces a per-tab error, not a whole-screen error`() {
        val sat1 = satellite(id = "sat-1", isDefault = true)
        val sat2 = satellite(id = "sat-2")
        val pass2 = pass(id = "pass-2", satelliteId = "sat-2", aosOffsetSeconds = 300)
        coEvery { satelliteRepository.getSatellites() } returns ApiResult.Success(listOf(sat1, sat2))
        coEvery { passRepository.getPasses("sat-1", false) } returns ApiResult.NetworkError
        coEvery { passRepository.getPasses("sat-2", false) } returns ApiResult.Success(listOf(pass2))

        viewModel = DashboardViewModel(satelliteRepository, passRepository, clock)
        runCurrent()

        val state = viewModel.uiState.value as DashboardUiState.Content
        val tab1 = state.tabs.first { it.satelliteId == "sat-1" }
        val tab2 = state.tabs.first { it.satelliteId == "sat-2" }
        assertTrue(tab1.passes.isEmpty())
        assertEquals("No network connection.", tab1.loadError)
        assertTrue(tab2.passes.isNotEmpty())
        assertNull(tab2.loadError)
    }

    @Test
    fun `selectTab updates selectedSatelliteId without any additional repository call`() {
        val sat1 = satellite(id = "sat-1", isDefault = true)
        val sat2 = satellite(id = "sat-2")
        coEvery { satelliteRepository.getSatellites() } returns ApiResult.Success(listOf(sat1, sat2))
        coEvery { passRepository.getPasses("sat-1", false) } returns ApiResult.Success(emptyList())
        coEvery { passRepository.getPasses("sat-2", false) } returns ApiResult.Success(emptyList())
        viewModel = DashboardViewModel(satelliteRepository, passRepository, clock)
        runCurrent()
        clearMocks(passRepository, answers = false)

        viewModel.selectTab("sat-2")
        runCurrent()

        val state = viewModel.uiState.value as DashboardUiState.Content
        assertEquals("sat-2", state.selectedSatelliteId)
        coVerify(exactly = 0) { passRepository.getPasses(any(), any()) }
    }

    @Test
    fun `polling refetches every tab with forceRefresh false on the timer interval`() {
        val sat1 = satellite(id = "sat-1", isDefault = true)
        val sat2 = satellite(id = "sat-2")
        coEvery { satelliteRepository.getSatellites() } returns ApiResult.Success(listOf(sat1, sat2))
        coEvery { passRepository.getPasses("sat-1", false) } returns ApiResult.Success(emptyList())
        coEvery { passRepository.getPasses("sat-2", false) } returns ApiResult.Success(emptyList())
        viewModel = DashboardViewModel(satelliteRepository, passRepository, clock)
        runCurrent()

        advanceTimeBy(POLL_INTERVAL_MILLIS)
        runCurrent()

        // 1 call from the initial load + 1 from the poll tick, for each satellite.
        coVerify(exactly = 2) { passRepository.getPasses("sat-1", false) }
        coVerify(exactly = 2) { passRepository.getPasses("sat-2", false) }
        coVerify(exactly = 0) { passRepository.getPasses("sat-1", true) }
    }

    @Test
    fun `refresh force-refreshes only the currently selected tab`() {
        val sat1 = satellite(id = "sat-1", isDefault = true)
        val sat2 = satellite(id = "sat-2")
        coEvery { satelliteRepository.getSatellites() } returns ApiResult.Success(listOf(sat1, sat2))
        coEvery { passRepository.getPasses("sat-1", false) } returns ApiResult.Success(emptyList())
        coEvery { passRepository.getPasses("sat-2", false) } returns ApiResult.Success(emptyList())
        coEvery { passRepository.getPasses("sat-1", true) } returns ApiResult.Success(emptyList())
        viewModel = DashboardViewModel(satelliteRepository, passRepository, clock)
        runCurrent()

        viewModel.refresh()
        runCurrent()

        coVerify(exactly = 1) { passRepository.getPasses("sat-1", true) }
        coVerify(exactly = 0) { passRepository.getPasses("sat-2", true) }
    }

    @Test
    fun `countdown ticker computes the initial value and ticks down as virtual time advances`() {
        val sat1 = satellite(id = "sat-1", isDefault = true)
        val pass1 = pass(id = "pass-1", satelliteId = "sat-1", aosOffsetSeconds = 130)
        coEvery { satelliteRepository.getSatellites() } returns ApiResult.Success(listOf(sat1))
        coEvery { passRepository.getPasses("sat-1", false) } returns ApiResult.Success(listOf(pass1))
        viewModel = DashboardViewModel(satelliteRepository, passRepository, clock)
        runCurrent()

        var tab = (viewModel.uiState.value as DashboardUiState.Content).tabs.first()
        assertEquals(Duration.ofSeconds(130), tab.nextPassCountdown)
        assertEquals(pass1, tab.nextPass)

        advanceTimeBy(60_000)
        runCurrent()

        tab = (viewModel.uiState.value as DashboardUiState.Content).tabs.first()
        assertEquals(Duration.ofSeconds(70), tab.nextPassCountdown)
        assertEquals(pass1, tab.nextPass)
    }

    @Test
    fun `countdown ticker rolls over to the next pass once the current pass's AOS arrives`() {
        val sat1 = satellite(id = "sat-1", isDefault = true)
        val passA = pass(id = "pass-a", satelliteId = "sat-1", aosOffsetSeconds = 5)
        val passB = pass(id = "pass-b", satelliteId = "sat-1", aosOffsetSeconds = 20)
        coEvery { satelliteRepository.getSatellites() } returns ApiResult.Success(listOf(sat1))
        coEvery { passRepository.getPasses("sat-1", false) } returns ApiResult.Success(listOf(passA, passB))
        viewModel = DashboardViewModel(satelliteRepository, passRepository, clock)
        runCurrent()

        advanceTimeBy(6_000) // past passA's AOS (5s), before passB's (20s)
        runCurrent()

        val tab = (viewModel.uiState.value as DashboardUiState.Content).tabs.first()
        assertEquals(Duration.ofSeconds(14), tab.nextPassCountdown) // 20 - 6
        assertEquals(passB, tab.nextPass)
    }

    @Test
    fun `countdown ticker restarts for the newly selected tab and stops updating the old one`() {
        val sat1 = satellite(id = "sat-1", isDefault = true)
        val sat2 = satellite(id = "sat-2")
        val pass1 = pass(id = "pass-1", satelliteId = "sat-1", aosOffsetSeconds = 100)
        val pass2 = pass(id = "pass-2", satelliteId = "sat-2", aosOffsetSeconds = 50)
        coEvery { satelliteRepository.getSatellites() } returns ApiResult.Success(listOf(sat1, sat2))
        coEvery { passRepository.getPasses("sat-1", false) } returns ApiResult.Success(listOf(pass1))
        coEvery { passRepository.getPasses("sat-2", false) } returns ApiResult.Success(listOf(pass2))
        viewModel = DashboardViewModel(satelliteRepository, passRepository, clock)
        runCurrent()

        var state = viewModel.uiState.value as DashboardUiState.Content
        assertEquals(Duration.ofSeconds(100), state.tabs.first { it.satelliteId == "sat-1" }.nextPassCountdown)
        assertNull(state.tabs.first { it.satelliteId == "sat-2" }.nextPassCountdown)

        viewModel.selectTab("sat-2")
        runCurrent()

        state = viewModel.uiState.value as DashboardUiState.Content
        assertEquals(Duration.ofSeconds(50), state.tabs.first { it.satelliteId == "sat-2" }.nextPassCountdown)
        val tab1AfterSwitch = state.tabs.first { it.satelliteId == "sat-1" }

        advanceTimeBy(10_000)
        runCurrent()

        state = viewModel.uiState.value as DashboardUiState.Content
        assertEquals(Duration.ofSeconds(40), state.tabs.first { it.satelliteId == "sat-2" }.nextPassCountdown)
        // sat-1's countdown was never re-ticked after the switch, so it's unchanged.
        assertEquals(
            tab1AfterSwitch.nextPassCountdown,
            state.tabs.first { it.satelliteId == "sat-1" }.nextPassCountdown
        )
    }
}
