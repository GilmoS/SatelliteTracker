package com.sattrakk.app.ui.dashboard

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.sattrakk.app.data.local.HiddenSatellitesStore
import com.sattrakk.app.data.local.NotificationPromptStore
import com.sattrakk.app.data.permission.NotificationPermissionManager
import com.sattrakk.app.data.push.FcmTokenFetcher
import com.sattrakk.app.data.repository.PassRepository
import com.sattrakk.app.data.repository.SatelliteRepository
import com.sattrakk.app.domain.model.ApiResult
import com.sattrakk.app.domain.model.Pass
import com.sattrakk.app.domain.model.Satellite
import com.sattrakk.app.domain.util.excludePastAos
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
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.combine
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch

// Screen state + orchestration for the dashboard's per-satellite tabs. Deliberately generic over
// whatever satellites the backend returns (never hardcoded to EROS C3 / RUNNER 1) — see
// SatelliteTabState and android/CLAUDE.md.
@HiltViewModel
class DashboardViewModel @Inject constructor(
    private val satelliteRepository: SatelliteRepository,
    private val passRepository: PassRepository,
    private val clock: Clock,
    private val hiddenSatellitesStore: HiddenSatellitesStore,
    private val notificationPermissionManager: NotificationPermissionManager,
    private val notificationPromptStore: NotificationPromptStore,
    private val fcmTokenFetcher: FcmTokenFetcher
) : ViewModel() {

    // True while DashboardScreen should launch the system POST_NOTIFICATIONS dialog. State rather
    // than a one-shot event (see SessionManager's rationale), cleared by the screen the moment it
    // launches the dialog so a recomposition/config change can't launch it twice.
    private val _requestNotificationPermission = MutableStateFlow(false)
    val requestNotificationPermission: StateFlow<Boolean> = _requestNotificationPermission.asStateFlow()

    // Internal source of truth — ALL loaded tabs, regardless of hidden status, exactly as the
    // pre-hidden-satellites design worked. Every existing load/poll/ticker method below reads and
    // writes this one, unchanged in shape, so hiding a satellite never touches what's actually
    // fetched/cached (see android/CLAUDE.md — "the cache should still hold the full fetched set").
    private val _rawState = MutableStateFlow<DashboardUiState>(DashboardUiState.Loading)

    // Publicly exposed state: the same Content, with hidden satellites' tabs filtered out. Built
    // as a combine() of _rawState and the HiddenSatellitesStore Flow (rather than writing hidden-
    // filtered tabs directly into _rawState) specifically so a poll/refresh/ticker tick — which
    // only knows the raw fetched data, not the current hidden set — can never clobber a previous
    // filtering pass. See android/CLAUDE.md's DashboardViewModel section.
    val uiState: StateFlow<DashboardUiState> =
        combine(_rawState, hiddenSatellitesStore.hiddenSatelliteIds) { raw, hiddenIds ->
            if (raw is DashboardUiState.Content) {
                raw.copy(tabs = raw.tabs.filterNot { hiddenIds.contains(it.satelliteId) })
            } else {
                raw
            }
        }.stateIn(viewModelScope, SharingStarted.Eagerly, DashboardUiState.Loading)

    // Exactly one of each runs at a time. countdownTickerJob is restarted every time the selected
    // tab changes, so only the visible tab's countdown is ever actively ticking. pollingJob runs
    // for the ViewModel's whole lifetime.
    private var countdownTickerJob: Job? = null
    private var pollingJob: Job? = null

    init {
        loadDashboard()
        startPolling()
        observeHiddenSatellites()
    }

    private fun loadDashboard() {
        viewModelScope.launch {
            // Snapshot of the current hidden set, just for picking a sensible default selected
            // tab — the publicly exposed `uiState` combine() above is what actually keeps `tabs`
            // filtered live; this is only about not defaulting the selection to a hidden satellite
            // on first load (see android/CLAUDE.md).
            val hiddenIds = hiddenSatellitesStore.hiddenSatelliteIds.first()
            when (val satellitesResult = satelliteRepository.getSatellites()) {
                is ApiResult.Success -> {
                    val satellites = satellitesResult.data
                    val tabs = loadAllTabs(satellites)
                    val visibleSatellites = satellites.filterNot { hiddenIds.contains(it.id) }
                    val selectedId = visibleSatellites.firstOrNull { it.isDefault }?.id
                        ?: visibleSatellites.firstOrNull()?.id
                        ?: satellites.firstOrNull()?.id
                        ?: ""
                    _rawState.value = DashboardUiState.Content(tabs = tabs, selectedSatelliteId = selectedId)
                    if (selectedId.isNotEmpty()) startCountdownTicker(selectedId)
                    maybeRequestNotificationPermission()
                }
                else -> _rawState.value = DashboardUiState.Error(errorMessageFor(satellitesResult))
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
    private suspend fun loadTab(satellite: Satellite): SatelliteTabState {
        val now = OffsetDateTime.now(clock)
        return when (val result = passRepository.getPasses(satellite.id)) {
            is ApiResult.Success -> SatelliteTabState(
                satelliteId = satellite.id,
                satelliteName = satellite.name,
                passes = result.data,
                visiblePasses = result.data.excludePastAos(now),
                nextPassCountdown = null,
                loadError = null
            )
            else -> SatelliteTabState(
                satelliteId = satellite.id,
                satelliteName = satellite.name,
                passes = emptyList(),
                visiblePasses = emptyList(),
                nextPassCountdown = null,
                loadError = errorMessageFor(result)
            )
        }
    }

    // Pure local state update — no repository call. Restarts the countdown ticker so only the
    // newly selected tab's countdown keeps ticking.
    fun selectTab(satelliteId: String) {
        val current = _rawState.value
        if (current !is DashboardUiState.Content) return
        if (current.selectedSatelliteId == satelliteId) return
        _rawState.value = current.copy(selectedSatelliteId = satelliteId)
        startCountdownTicker(satelliteId)
    }

    // Force-refreshes only the currently selected tab, not every satellite in the background —
    // the user is pulling to refresh what they're looking at. isRefreshing drives the
    // PullToRefreshBox spinner in DashboardScreen — see DashboardUiState.Content.isRefreshing.
    fun refresh() {
        val current = _rawState.value
        if (current !is DashboardUiState.Content) return
        if (current.isRefreshing) return
        val selectedId = current.selectedSatelliteId
        _rawState.value = current.copy(isRefreshing = true)
        viewModelScope.launch {
            val result = passRepository.getPasses(selectedId, forceRefresh = true)
            applyPassesResult(selectedId, result)
            val afterLoad = _rawState.value
            if (afterLoad is DashboardUiState.Content) {
                _rawState.value = afterLoad.copy(isRefreshing = false)
            }
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
                val current = _rawState.value
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
        val current = _rawState.value
        if (current !is DashboardUiState.Content) return
        val now = OffsetDateTime.now(clock)
        val updatedTabs = current.tabs.map { tab ->
            if (tab.satelliteId != satelliteId) {
                tab
            } else when (result) {
                is ApiResult.Success -> tab.copy(
                    passes = result.data,
                    visiblePasses = result.data.excludePastAos(now),
                    loadError = null
                )
                else -> tab.copy(loadError = errorMessageFor(result))
            }
        }
        _rawState.value = current.copy(tabs = updatedTabs)
    }

    // Reacts to the hidden-satellites set changing WHILE Dashboard is active (e.g. the user
    // backgrounds the app, hides a satellite from Settings, and returns — or hides one without
    // ever leaving Dashboard, if that's reachable). Only ever moves the *selection* away from a
    // satellite that just became hidden and restarts its ticker; the actual tab-list filtering for
    // display is handled reactively by the `uiState` combine() above regardless of this method.
    private fun observeHiddenSatellites() {
        viewModelScope.launch {
            hiddenSatellitesStore.hiddenSatelliteIds.collect { hiddenIds ->
                val current = _rawState.value
                if (current is DashboardUiState.Content && hiddenIds.contains(current.selectedSatelliteId)) {
                    val fallbackId = current.tabs.map { it.satelliteId }.firstOrNull { it !in hiddenIds }
                    if (fallbackId != null) {
                        _rawState.value = current.copy(selectedSatelliteId = fallbackId)
                        startCountdownTicker(fallbackId)
                    } else {
                        // Every loaded satellite is now hidden — nothing sensible to select or
                        // tick for. Leave selectedSatelliteId as-is; the Composable's empty-tabs
                        // branch already handles "no visible satellites."
                        countdownTickerJob?.cancel()
                    }
                }
            }
        }
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
                val current = _rawState.value
                if (current is DashboardUiState.Content) {
                    val tab = current.tabs.find { it.satelliteId == satelliteId }
                    if (tab != null) {
                        val now = OffsetDateTime.now(clock)
                        val next = tab.passes.filter { it.aos.isAfter(now) }.minByOrNull { it.aos }
                        val countdown = next?.let { Duration.between(now, it.aos) }
                        updateCountdown(satelliteId, countdown, next, tab.passes.excludePastAos(now))
                    }
                }
                delay(1000)
            }
        }
    }

    private fun updateCountdown(satelliteId: String, countdown: Duration?, nextPass: Pass?, visiblePasses: List<Pass>) {
        val current = _rawState.value
        if (current !is DashboardUiState.Content) return
        val updatedTabs = current.tabs.map { tab ->
            if (tab.satelliteId == satelliteId) {
                tab.copy(nextPassCountdown = countdown, nextPass = nextPass, visiblePasses = visiblePasses)
            } else {
                tab
            }
        }
        _rawState.value = current.copy(tabs = updatedTabs)
    }

    // The one-time "hard ask" trigger point: the first successful Dashboard load (getSatellites()
    // succeeded) on this install. The flag is persisted before anything else so the dialog is
    // never re-triggered on a later app open, even if this process dies mid-dialog — from then on
    // the Settings screen's permission card is the way back. If the permission is already granted
    // (always true below API 33, or granted earlier from Settings), no dialog is needed and the
    // FCM token is fetched straight away.
    private suspend fun maybeRequestNotificationPermission() {
        if (notificationPromptStore.hasRequestedNotificationPermission.first()) return
        notificationPromptStore.markNotificationPermissionRequested()
        if (notificationPermissionManager.isGranted()) {
            fcmTokenFetcher.fetchToken()
        } else {
            _requestNotificationPermission.value = true
        }
    }

    fun onNotificationPermissionRequestLaunched() {
        _requestNotificationPermission.value = false
    }

    // On grant, proactively feed the token-sync flow rather than waiting for onNewToken, which
    // won't fire for an already-stable token. On denial, nothing — no re-ask from Dashboard.
    fun onNotificationPermissionResult(granted: Boolean) {
        if (granted) fcmTokenFetcher.fetchToken()
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
