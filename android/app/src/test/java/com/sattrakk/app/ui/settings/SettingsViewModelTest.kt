package com.sattrakk.app.ui.settings

import com.sattrakk.app.MainDispatcherRule
import com.sattrakk.app.data.local.HiddenSatellitesStore
import com.sattrakk.app.data.permission.NotificationPermissionManager
import com.sattrakk.app.data.repository.SatelliteRepository
import com.sattrakk.app.data.repository.SettingsRepository
import com.sattrakk.app.domain.model.ApiResult
import com.sattrakk.app.domain.model.Satellite
import com.sattrakk.app.domain.model.UserSettings
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.every
import io.mockk.mockk
import java.time.OffsetDateTime
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.MutableStateFlow
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test

// SettingsViewModel's init launches a `combine(...).collect { }` coroutine on viewModelScope that
// suspends forever waiting on the next Flow emission (it never completes on its own — same shape
// as DashboardViewModel's polling job). As DashboardViewModelTest's own comment documents in
// detail, that rules out kotlinx-coroutines-test's `runTest { }` here too: these tests drive
// mainDispatcherRule.testDispatcher.scheduler.runCurrent() directly after each action instead,
// since nothing in a test body itself needs to suspend (MockK's coEvery/coVerify and reading
// uiState.value are plain synchronous calls).
@OptIn(ExperimentalCoroutinesApi::class)
class SettingsViewModelTest {

    @get:Rule
    val mainDispatcherRule = MainDispatcherRule()

    private val satelliteRepository = mockk<SatelliteRepository>()
    private val settingsRepository = mockk<SettingsRepository>()
    private val hiddenSatellitesStore = mockk<HiddenSatellitesStore>()
    private val notificationPermissionManager = mockk<NotificationPermissionManager>()

    private lateinit var hiddenIdsFlow: MutableStateFlow<Set<String>>

    private fun runCurrent() = mainDispatcherRule.testDispatcher.scheduler.runCurrent()

    private fun satellite(id: String, name: String = "Satellite-$id") = Satellite(
        id = id,
        name = name,
        noradId = 1,
        description = null,
        isActive = true,
        isDefault = false,
        createdAt = OffsetDateTime.parse("2026-08-01T00:00:00Z")
    )

    private fun buildViewModel(
        satellitesResult: ApiResult<List<Satellite>> = ApiResult.Success(emptyList()),
        settingsResult: ApiResult<UserSettings> = ApiResult.Success(UserSettings(emptyList(), null)),
        hiddenIds: Set<String> = emptySet(),
        permissionGranted: Boolean = true
    ): SettingsViewModel {
        hiddenIdsFlow = MutableStateFlow(hiddenIds)
        every { hiddenSatellitesStore.hiddenSatelliteIds } returns hiddenIdsFlow
        coEvery { satelliteRepository.getSatellites() } returns satellitesResult
        coEvery { settingsRepository.getSettings() } returns settingsResult
        every { notificationPermissionManager.isGranted() } returns permissionGranted
        val viewModel = SettingsViewModel(
            satelliteRepository,
            settingsRepository,
            hiddenSatellitesStore,
            notificationPermissionManager
        )
        runCurrent()
        return viewModel
    }

    @Test
    fun `initial load combines satellites, settings, and hidden-state correctly`() {
        val sat1 = satellite("sat-1")
        val sat2 = satellite("sat-2")
        val viewModel = buildViewModel(
            satellitesResult = ApiResult.Success(listOf(sat1, sat2)),
            settingsResult = ApiResult.Success(UserSettings(listOf(5, 10), "fcm-token")),
            hiddenIds = setOf("sat-2"),
            permissionGranted = true
        )

        val state = viewModel.uiState.value
        assertFalse(state.isLoading)
        assertNull(state.error)
        assertEquals(2, state.satellites.size)
        assertFalse(state.satellites.first { it.satelliteId == "sat-1" }.isHidden)
        assertTrue(state.satellites.first { it.satelliteId == "sat-2" }.isHidden)
        assertEquals(setOf(5, 10), state.alertMinutes)
        assertEquals(setOf(5, 10), state.lastNonEmptyAlertMinutes)
        assertTrue(state.pushPermissionGranted)
        assertTrue(state.sendPushEnabled)
    }

    @Test
    fun `toggleSatelliteVisibility does not call SettingsRepository`() {
        val viewModel = buildViewModel(satellitesResult = ApiResult.Success(listOf(satellite("sat-1"))))
        coEvery { hiddenSatellitesStore.setHidden(any(), any()) } answers {
            hiddenIdsFlow.value = hiddenIdsFlow.value + firstArg<String>()
        }

        viewModel.toggleSatelliteVisibility("sat-1")
        runCurrent()

        coVerify(exactly = 1) { hiddenSatellitesStore.setHidden("sat-1", true) }
        coVerify(exactly = 0) { settingsRepository.updateAlertMinutes(any()) }
        assertTrue(viewModel.uiState.value.satellites.first { it.satelliteId == "sat-1" }.isHidden)
    }

    @Test
    fun `toggleSatelliteVisibility on an already-hidden satellite unhides it`() {
        val viewModel = buildViewModel(
            satellitesResult = ApiResult.Success(listOf(satellite("sat-1"))),
            hiddenIds = setOf("sat-1")
        )
        coEvery { hiddenSatellitesStore.setHidden(any(), any()) } answers {
            hiddenIdsFlow.value = hiddenIdsFlow.value - firstArg<String>()
        }

        viewModel.toggleSatelliteVisibility("sat-1")
        runCurrent()

        coVerify(exactly = 1) { hiddenSatellitesStore.setHidden("sat-1", false) }
    }

    @Test
    fun `updateAlertMinutes with non-empty set calls backend and updates state`() {
        val viewModel = buildViewModel()
        coEvery { settingsRepository.updateAlertMinutes(listOf(15, 30)) } returns
            ApiResult.Success(UserSettings(listOf(15, 30), null))

        viewModel.updateAlertMinutes(setOf(15, 30))
        runCurrent()

        coVerify(exactly = 1) { settingsRepository.updateAlertMinutes(listOf(15, 30)) }
        val state = viewModel.uiState.value
        assertEquals(setOf(15, 30), state.alertMinutes)
        assertEquals(setOf(15, 30), state.lastNonEmptyAlertMinutes)
        assertTrue(state.sendPushEnabled)
    }

    @Test
    fun `setSendPushEnabled false calls backend with empty list and preserves lastNonEmptyAlertMinutes`() {
        val viewModel = buildViewModel(settingsResult = ApiResult.Success(UserSettings(listOf(10), null)))
        coEvery { settingsRepository.updateAlertMinutes(emptyList()) } returns
            ApiResult.Success(UserSettings(emptyList(), null))

        viewModel.setSendPushEnabled(false)
        runCurrent()

        coVerify(exactly = 1) { settingsRepository.updateAlertMinutes(emptyList()) }
        val state = viewModel.uiState.value
        assertFalse(state.sendPushEnabled)
        assertEquals(emptySet<Int>(), state.alertMinutes)
        assertEquals(setOf(10), state.lastNonEmptyAlertMinutes)
    }

    @Test
    fun `setSendPushEnabled true after a prior non-empty selection restores it via backend`() {
        val viewModel = buildViewModel(settingsResult = ApiResult.Success(UserSettings(listOf(10), null)))
        coEvery { settingsRepository.updateAlertMinutes(emptyList()) } returns
            ApiResult.Success(UserSettings(emptyList(), null))
        coEvery { settingsRepository.updateAlertMinutes(listOf(10)) } returns
            ApiResult.Success(UserSettings(listOf(10), null))
        viewModel.setSendPushEnabled(false)
        runCurrent()

        viewModel.setSendPushEnabled(true)
        runCurrent()

        coVerify(exactly = 1) { settingsRepository.updateAlertMinutes(listOf(10)) }
        val state = viewModel.uiState.value
        assertTrue(state.sendPushEnabled)
        assertEquals(setOf(10), state.alertMinutes)
    }

    @Test
    fun `setSendPushEnabled true with no prior selection this session sets needsAlertMinutesSelection without a backend call`() {
        val viewModel = buildViewModel(settingsResult = ApiResult.Success(UserSettings(emptyList(), null)))

        viewModel.setSendPushEnabled(true)
        runCurrent()

        coVerify(exactly = 0) { settingsRepository.updateAlertMinutes(any()) }
        val state = viewModel.uiState.value
        assertTrue(state.needsAlertMinutesSelection)
        assertFalse(state.sendPushEnabled)
    }

    @Test
    fun `addSatellite stub emits stubMessage without calling any repository`() {
        val viewModel = buildViewModel()

        viewModel.addSatellite()
        runCurrent()

        assertEquals("Adding satellites isn't available yet", viewModel.uiState.value.stubMessage)
        coVerify(exactly = 0) { settingsRepository.updateAlertMinutes(any()) }
    }

    @Test
    fun `removeSatellite stub emits stubMessage without calling any repository`() {
        val viewModel = buildViewModel()

        viewModel.removeSatellite("sat-1")
        runCurrent()

        assertEquals("Adding satellites isn't available yet", viewModel.uiState.value.stubMessage)
        coVerify(exactly = 0) { settingsRepository.updateAlertMinutes(any()) }
    }

    @Test
    fun `consumeStubMessage clears the stub message`() {
        val viewModel = buildViewModel()
        viewModel.addSatellite()
        runCurrent()

        viewModel.consumeStubMessage()
        runCurrent()

        assertNull(viewModel.uiState.value.stubMessage)
    }
}
