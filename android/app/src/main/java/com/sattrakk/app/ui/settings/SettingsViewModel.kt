package com.sattrakk.app.ui.settings

import android.app.Activity
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.sattrakk.app.data.local.HiddenSatellitesStore
import com.sattrakk.app.data.permission.NotificationPermissionManager
import com.sattrakk.app.data.repository.SatelliteRepository
import com.sattrakk.app.data.repository.SettingsRepository
import com.sattrakk.app.domain.model.ApiResult
import com.sattrakk.app.domain.model.Satellite
import dagger.hilt.android.lifecycle.HiltViewModel
import javax.inject.Inject
import kotlinx.coroutines.async
import kotlinx.coroutines.coroutineScope
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.combine
import kotlinx.coroutines.launch

// Screen state + orchestration for the Settings screen (Milestone E) — hidden-satellite
// visibility (local-only, HiddenSatellitesStore), alert-minute/push preferences (backend-synced,
// SettingsRepository), and notification permission status (NotificationPermissionManager). No
// Composable is wired to this yet (designed separately) — covered entirely by
// SettingsViewModelTest. See android/CLAUDE.md's "Settings screen" section.
@HiltViewModel
class SettingsViewModel @Inject constructor(
    private val satelliteRepository: SatelliteRepository,
    private val settingsRepository: SettingsRepository,
    private val hiddenSatellitesStore: HiddenSatellitesStore,
    private val notificationPermissionManager: NotificationPermissionManager
) : ViewModel() {

    private val _uiState = MutableStateFlow(SettingsUiState())
    val uiState: StateFlow<SettingsUiState> = _uiState.asStateFlow()

    // The backend-fetched satellite catalog, held separately from uiState.satellites
    // (SatelliteVisibility): satellites is a one-shot fetch on init, while hiddenSatelliteIds is a
    // continuously-collected Flow — every emission from the store needs to rebuild the
    // SatelliteVisibility list against whatever satellites are currently known, without re-fetching
    // them. combine() (below) recomputes uiState.satellites whenever either input changes.
    private val loadedSatellites = MutableStateFlow<List<Satellite>>(emptyList())

    init {
        viewModelScope.launch {
            combine(loadedSatellites, hiddenSatellitesStore.hiddenSatelliteIds) { satellites, hiddenIds ->
                satellites.map { satellite ->
                    SatelliteVisibility(
                        satelliteId = satellite.id,
                        satelliteName = satellite.name,
                        noradId = satellite.noradId,
                        isHidden = hiddenIds.contains(satellite.id)
                    )
                }
            }.collect { visibilities ->
                _uiState.value = _uiState.value.copy(satellites = visibilities)
            }
        }
        viewModelScope.launch { loadInitialData() }
    }

    private suspend fun loadInitialData() {
        _uiState.value = _uiState.value.copy(
            isLoading = true,
            pushPermissionGranted = notificationPermissionManager.isGranted()
        )

        coroutineScope {
            val satellitesDeferred = async { satelliteRepository.getSatellites() }
            val settingsDeferred = async { settingsRepository.getSettings() }
            val satellitesResult = satellitesDeferred.await()
            val settingsResult = settingsDeferred.await()

            if (satellitesResult is ApiResult.Success) {
                loadedSatellites.value = satellitesResult.data
            }

            val alertMinutes = (settingsResult as? ApiResult.Success)?.data?.alertMinutes?.toSet()

            // Per-source error message, same philosophy as DashboardViewModel/
            // FullPassListViewModel's per-tab / per-half error handling: whichever side loaded
            // fine is still reflected in the state, `error` just describes what didn't.
            val error = when {
                satellitesResult is ApiResult.Success && settingsResult is ApiResult.Success -> null
                satellitesResult !is ApiResult.Success && settingsResult !is ApiResult.Success ->
                    errorMessageFor(settingsResult)
                satellitesResult !is ApiResult.Success -> "Satellites: ${errorMessageFor(satellitesResult)}"
                else -> "Settings: ${errorMessageFor(settingsResult)}"
            }

            _uiState.value = _uiState.value.copy(
                alertMinutes = alertMinutes ?: _uiState.value.alertMinutes,
                lastNonEmptyAlertMinutes = if (!alertMinutes.isNullOrEmpty()) {
                    alertMinutes
                } else {
                    _uiState.value.lastNonEmptyAlertMinutes
                },
                isLoading = false,
                error = error
            )
        }
    }

    // Local-only — never calls SettingsRepository/the backend. See HiddenSatellitesStore.
    fun toggleSatelliteVisibility(satelliteId: String) {
        val currentlyHidden = _uiState.value.satellites
            .firstOrNull { it.satelliteId == satelliteId }
            ?.isHidden
            ?: false
        viewModelScope.launch {
            hiddenSatellitesStore.setHidden(satelliteId, hidden = !currentlyHidden)
        }
    }

    // The real, persisted, backend-synced preference. An empty set is a valid, real state (the
    // backend already treats empty AlertMinutes as "no alerts" — see android/CLAUDE.md); this
    // isn't a new backend concept, sendPush/setSendPushEnabled below are UI-derived views over it.
    fun updateAlertMinutes(minutes: Set<Int>) {
        viewModelScope.launch {
            when (val result = settingsRepository.updateAlertMinutes(minutes.toList())) {
                is ApiResult.Success -> {
                    val updated = result.data.alertMinutes.toSet()
                    _uiState.value = _uiState.value.copy(
                        alertMinutes = updated,
                        lastNonEmptyAlertMinutes = if (updated.isNotEmpty()) {
                            updated
                        } else {
                            _uiState.value.lastNonEmptyAlertMinutes
                        },
                        needsAlertMinutesSelection = false,
                        error = null
                    )
                }
                else -> _uiState.value = _uiState.value.copy(error = errorMessageFor(result))
            }
        }
    }

    // sendPush is a UI-level concept, not a new backend field — see SettingsUiState.sendPushEnabled
    // and android/CLAUDE.md.
    fun setSendPushEnabled(enabled: Boolean) {
        if (!enabled) {
            updateAlertMinutes(emptySet())
            return
        }

        val restore = _uiState.value.lastNonEmptyAlertMinutes
        if (restore.isEmpty()) {
            // No prior selection to restore this session (e.g. fresh app start) — do NOT guess
            // default alert minutes on the user's behalf. No backend call; surface the flag
            // instead and let the future UI prompt the user. See SettingsUiState's doc comment.
            _uiState.value = _uiState.value.copy(needsAlertMinutesSelection = true)
            return
        }
        updateAlertMinutes(restore)
    }

    // Meant to be called from the UI layer's onResume-equivalent lifecycle hook (e.g. a Composable
    // observing Lifecycle.Event.ON_RESUME) — permission state can change externally while the app
    // is backgrounded (user grants it via system settings and returns). The Activity is supplied
    // by the caller for this one call and never retained — see
    // NotificationPermissionManager.shouldShowRationale's doc comment for why an Activity is
    // needed here at all.
    fun refreshPermissionStatus(activity: Activity) {
        _uiState.value = _uiState.value.copy(
            pushPermissionGranted = notificationPermissionManager.isGranted(),
            pushPermissionShouldShowRationale = notificationPermissionManager.shouldShowRationale(activity)
        )
    }

    // STUBS: no backend support exists for satellite catalog management yet (repo-root
    // CLAUDE.md's MVP scope notes already mark satellite search/add as deferred). Neither calls
    // any repository — they only surface stubMessage so a future UI can show it, e.g. as a
    // snackbar, rather than silently doing nothing.
    fun addSatellite() {
        _uiState.value = _uiState.value.copy(stubMessage = STUB_MESSAGE)
    }

    fun removeSatellite(satelliteId: String) {
        _uiState.value = _uiState.value.copy(stubMessage = STUB_MESSAGE)
    }

    fun consumeStubMessage() {
        _uiState.value = _uiState.value.copy(stubMessage = null)
    }

    private fun errorMessageFor(result: ApiResult<*>): String = when (result) {
        is ApiResult.Error -> result.message
        ApiResult.AuthRequired -> "Authentication required."
        ApiResult.NetworkError -> "No network connection."
        is ApiResult.Success -> error("errorMessageFor called with a Success result")
    }

    private companion object {
        const val STUB_MESSAGE = "Adding satellites isn't available yet"
    }
}
