package com.sattrakk.app.ui.dashboard

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.sattrakk.app.data.repository.PassRepository
import com.sattrakk.app.data.repository.SatelliteRepository
import com.sattrakk.app.domain.model.ApiResult
import com.sattrakk.app.domain.model.Pass
import com.sattrakk.app.domain.model.Satellite
import dagger.hilt.android.lifecycle.HiltViewModel
import java.time.Clock
import java.time.Duration
import java.time.OffsetDateTime
import java.util.concurrent.TimeUnit
import javax.inject.Inject
import kotlinx.coroutines.Job
import kotlinx.coroutines.async
import kotlinx.coroutines.awaitAll
import kotlinx.coroutines.coroutineScope
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch

// Screen state + orchestration for the dashboard's per-satellite tabs. Deliberately generic over
// whatever satellites the backend returns (never hardcoded to EROS C3 / RUNNER 1) — see
// SatelliteTabState and android/CLAUDE.md.
@HiltViewModel
class DashboardViewModel @Inject constructor(
    private val satelliteRepository: SatelliteRepository,
    private val passRepository: PassRepository,
    private val clock: Clock
) : ViewModel() {

    private val _uiState = MutableStateFlow<DashboardUiState>(DashboardUiState.Loading)
    val uiState: StateFlow<DashboardUiState> = _uiState.asStateFlow()

    // Exactly one of each runs at a time. countdownTickerJob is restarted every time the selected
    // tab changes, so only the visible tab's countdown is ever actively ticking. pollingJob runs
    // for the ViewModel's whole lifetime.
    private var countdownTickerJob: Job? = null
    private var pollingJob: Job? = null

    init {
        loadDashboard()
        startPolling()
    }

    private fun loadDashboard() {
        viewModelScope.launch {
            when (val satellitesResult = satelliteRepository.getSatellites()) {
                is ApiResult.Success -> {
                    val satellites = satellitesResult.data
                    val tabs = loadAllTabs(satellites)
                    val selectedId = satellites.firstOrNull { it.isDefault }?.id
                        ?: satellites.firstOrNull()?.id
                        ?: ""
                    _uiState.value = DashboardUiState.Content(tabs = tabs, selectedSatelliteId = selectedId)
                    if (selectedId.isNotEmpty()) startCountdownTicker(selectedId)
                }
                else -> _uiState.value = DashboardUiState.Error(errorMessageFor(satellitesResult))
            }
        }
    }

    // Issued in parallel (async/awaitAll) since each satellite's passes are independent of the
    // others — see android/CLAUDE.md.
    private suspend fun loadAllTabs(satellites: List<Satellite>): List<SatelliteTabState> = coroutineScope {
        satellites.map { satellite -> async { loadTab(satellite) } }.awaitAll()
    }

    // A satellite's own passes call failing does NOT fail the whole screen — see
    // SatelliteTabState.loadError and android/CLAUDE.md for why a per-tab failure was chosen over
    // blanking out tabs that loaded fine.
    private suspend fun loadTab(satellite: Satellite): SatelliteTabState =
        when (val result = passRepository.getPasses(satellite.id)) {
            is ApiResult.Success -> SatelliteTabState(
                satelliteId = satellite.id,
                satelliteName = satellite.name,
                passes = result.data,
                nextPassCountdown = null,
                loadError = null
            )
            else -> SatelliteTabState(
                satelliteId = satellite.id,
                satelliteName = satellite.name,
                passes = emptyList(),
                nextPassCountdown = null,
                loadError = errorMessageFor(result)
            )
        }

    // Pure local state update — no repository call. Restarts the countdown ticker so only the
    // newly selected tab's countdown keeps ticking.
    fun selectTab(satelliteId: String) {
        val current = _uiState.value
        if (current !is DashboardUiState.Content) return
        if (current.selectedSatelliteId == satelliteId) return
        _uiState.value = current.copy(selectedSatelliteId = satelliteId)
        startCountdownTicker(satelliteId)
    }

    // Force-refreshes only the currently selected tab, not every satellite in the background —
    // the user is pulling to refresh what they're looking at.
    fun refresh() {
        val current = _uiState.value
        if (current !is DashboardUiState.Content) return
        val selectedId = current.selectedSatelliteId
        viewModelScope.launch {
            val result = passRepository.getPasses(selectedId, forceRefresh = true)
            applyPassesResult(selectedId, result)
        }
    }

    // Relies entirely on PassRepository's existing 1h TTL (step 2.2's cachedNetworkFirst) to
    // decide whether a given 5-minute tick actually reaches the network — this deliberately does
    // NOT force a network call every 5 minutes; most ticks are served from the still-fresh Room
    // cache. See android/CLAUDE.md — do not "fix" this into an unconditional network poll.
    private fun startPolling() {
        pollingJob = viewModelScope.launch {
            while (isActive) {
                delay(POLL_INTERVAL_MILLIS)
                val current = _uiState.value
                if (current is DashboardUiState.Content) {
                    current.tabs.forEach { tab ->
                        launch {
                            val result = passRepository.getPasses(tab.satelliteId, forceRefresh = false)
                            applyPassesResult(tab.satelliteId, result)
                        }
                    }
                }
            }
        }
    }

    private fun applyPassesResult(satelliteId: String, result: ApiResult<List<Pass>>) {
        val current = _uiState.value
        if (current !is DashboardUiState.Content) return
        val updatedTabs = current.tabs.map { tab ->
            if (tab.satelliteId != satelliteId) {
                tab
            } else when (result) {
                is ApiResult.Success -> tab.copy(passes = result.data, loadError = null)
                else -> tab.copy(loadError = errorMessageFor(result))
            }
        }
        _uiState.value = current.copy(tabs = updatedTabs)
    }

    // Recomputes from the tab's current passes list every second (rather than decrementing a
    // captured value), so a poll/refresh landing mid-countdown is picked up on the very next
    // tick, and never makes a network call itself. Only one ticker runs at a time.
    //
    // "Reaching zero" edge case (not specified by the original spec, resolved here): this field
    // always means "time until the next pass whose AOS is still in the future." Once a pass's AOS
    // arrives, it no longer qualifies as "next" — the ticker automatically rolls over to whatever
    // pass (if any) is next after it, or null if none remain. There is no separate "in progress"
    // state surfaced through nextPassCountdown; a pass currently between its own AOS and LOS is
    // simply not reflected by this field at all. A different, equally reasonable choice would
    // have been to hold at zero / expose an explicit "in progress" state until LOS — flagged here
    // per the task's instructions rather than silently decided.
    private fun startCountdownTicker(satelliteId: String) {
        countdownTickerJob?.cancel()
        countdownTickerJob = viewModelScope.launch {
            while (isActive) {
                val current = _uiState.value
                if (current is DashboardUiState.Content) {
                    val tab = current.tabs.find { it.satelliteId == satelliteId }
                    if (tab != null) {
                        val now = OffsetDateTime.now(clock)
                        val next = tab.passes.filter { it.aos.isAfter(now) }.minByOrNull { it.aos }
                        val countdown = next?.let { Duration.between(now, it.aos) }
                        updateCountdown(satelliteId, countdown, next)
                    }
                }
                delay(1000)
            }
        }
    }

    private fun updateCountdown(satelliteId: String, countdown: Duration?, nextPass: Pass?) {
        val current = _uiState.value
        if (current !is DashboardUiState.Content) return
        val updatedTabs = current.tabs.map { tab ->
            if (tab.satelliteId == satelliteId) {
                tab.copy(nextPassCountdown = countdown, nextPass = nextPass)
            } else {
                tab
            }
        }
        _uiState.value = current.copy(tabs = updatedTabs)
    }

    private fun errorMessageFor(result: ApiResult<*>): String = when (result) {
        is ApiResult.Error -> result.message
        ApiResult.AuthRequired -> "Authentication required."
        ApiResult.NetworkError -> "No network connection."
        is ApiResult.Success -> error("errorMessageFor called with a Success result")
    }

    private companion object {
        val POLL_INTERVAL_MILLIS = TimeUnit.MINUTES.toMillis(5)
    }
}
