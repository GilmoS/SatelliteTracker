package com.sattrakk.app.ui.map

import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.sattrakk.app.data.repository.MapRepository
import com.sattrakk.app.data.repository.PassRepository
import com.sattrakk.app.data.repository.SatelliteRepository
import com.sattrakk.app.domain.model.ApiResult
import com.sattrakk.app.domain.model.LatLng
import com.sattrakk.app.domain.model.Satellite
import com.sattrakk.app.domain.model.SatellitePosition
import com.sattrakk.app.domain.util.GeoUtils
import dagger.hilt.android.lifecycle.HiltViewModel
import java.util.concurrent.TimeUnit
import javax.inject.Inject
import kotlinx.coroutines.async
import kotlinx.coroutines.coroutineScope
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch

// Screen state + orchestration for the Map screen (rendered by MapScreen). See android/CLAUDE.md's
// Map sections.
//
// The flow is chosen once, from nav args read via SavedStateHandle (same pattern as
// PassDetailsViewModel/FullPassListViewModel):
//  - `passId` present  -> StaticPassTrack: that pass's fixed ground track. One-shot, no polling.
//  - `passId` absent   -> LiveTrack for `satelliteId`, polled (see startLivePolling).
// Both are optional at the type level because the current `map` route takes no args at all —
// until the nav graph passes one, a missing satelliteId in the live flow is surfaced as an Error
// rather than guessing a default satellite.
@HiltViewModel
class MapViewModel @Inject constructor(
    private val mapRepository: MapRepository,
    private val passRepository: PassRepository,
    private val satelliteRepository: SatelliteRepository,
    savedStateHandle: SavedStateHandle
) : ViewModel() {

    private val passId: String? = savedStateHandle[PASS_ID_ARG]
    private val satelliteId: String? = savedStateHandle[SATELLITE_ID_ARG]

    private val _uiState = MutableStateFlow<MapUiState>(MapUiState.Loading)
    val uiState: StateFlow<MapUiState> = _uiState.asStateFlow()

    init {
        viewModelScope.launch {
            if (passId != null) loadStaticPassTrack(passId) else loadLiveTrack(satelliteId)
        }
    }

    // Flow 2. PassTrackDto carries only passId + points (no AOS/LOS/elevation/satellite), so the
    // header needs the Pass itself — fetched via the existing Room-first getPassById in parallel
    // with the existing getPassTrack (reused, not duplicated). The (24h-TTL, Room-cached) satellite
    // catalog is fetched alongside purely for the drawer's names; its failure is tolerated (empty
    // names), unlike the pass/track failures, which have nothing to show without them.
    private suspend fun loadStaticPassTrack(passId: String) = coroutineScope {
        val trackDeferred = async { passRepository.getPassTrack(passId) }
        val passDeferred = async { passRepository.getPassById(passId) }
        val drawerDeferred = async { mapRepository.getNotifyEnabledPasses() }
        val satellitesDeferred = async { satelliteRepository.getSatellites() }
        val trackResult = trackDeferred.await()
        val passResult = passDeferred.await()
        val drawer = drawerDeferred.await()
        val satellitesResult = satellitesDeferred.await()

        _uiState.value = when {
            passResult !is ApiResult.Success -> MapUiState.Error(errorMessageFor(passResult))
            trackResult !is ApiResult.Success -> MapUiState.Error(errorMessageFor(trackResult))
            else -> MapUiState.StaticPassTrack(
                pass = passResult.data,
                trackPoints = trackResult.data.points,
                notifyEnabledPasses = drawer,
                satelliteNames = (satellitesResult as? ApiResult.Success)?.data?.toNameMap().orEmpty()
            )
        }
    }

    // Flow 1. Satellite name comes from the (24h-TTL, Room-cached) catalog, matched by id — the same
    // lookup PassDetailsViewModel uses — not from N2YO's own satName on the position response, so
    // the Map shows the same name as every other screen.
    private suspend fun loadLiveTrack(satelliteId: String?) {
        if (satelliteId == null) {
            _uiState.value = MapUiState.Error("No satellite selected for the map.")
            return
        }

        val initial = coroutineScope {
            val satellitesDeferred = async { satelliteRepository.getSatellites() }
            val positionDeferred = async { mapRepository.getPosition(satelliteId) }
            val trackDeferred = async { mapRepository.getLiveTrack(satelliteId) }
            val drawerDeferred = async { mapRepository.getNotifyEnabledPasses() }
            val satellitesResult = satellitesDeferred.await()
            val positionResult = positionDeferred.await()
            val trackResult = trackDeferred.await()
            val drawer = drawerDeferred.await()

            when {
                satellitesResult !is ApiResult.Success -> MapUiState.Error(errorMessageFor(satellitesResult))
                positionResult !is ApiResult.Success -> MapUiState.Error(errorMessageFor(positionResult))
                trackResult !is ApiResult.Success -> MapUiState.Error(errorMessageFor(trackResult))
                else -> {
                    val satellite = satellitesResult.data.firstOrNull { it.id == satelliteId }
                    if (satellite == null) {
                        MapUiState.Error("Unknown satellite.")
                    } else {
                        MapUiState.LiveTrack(
                            satelliteId = satelliteId,
                            satelliteName = satellite.name,
                            currentPosition = positionResult.data,
                            trackPoints = trackResult.data,
                            footprintPolygon = footprintFor(positionResult.data),
                            notifyEnabledPasses = drawer,
                            // Same catalog already fetched for satelliteName above — no extra call.
                            satelliteNames = satellitesResult.data.toNameMap()
                        )
                    }
                }
            }
        }

        _uiState.value = initial
        if (initial is MapUiState.LiveTrack) startLivePolling(satelliteId)
    }

    // Two independent loops on viewModelScope, so both stop when the ViewModel is cleared (leaving
    // the Map destination) — the same lifecycle as DashboardViewModel's ticker/poller:
    //  - position every 15s, recomputing the footprint each time;
    //  - live track every 5 min, matching its server-side cache TTL. Polling it every 15s would
    //    only re-read the backend's cached copy; never refreshing it would leave the drawn track
    //    visibly behind the moving position marker within a single long viewing session (the
    //    track covers the next ~5 min of flight, per RealTimeController's `seconds: 300`).
    // A failed poll keeps the last good LiveTrack on screen and simply tries again next tick —
    // only the INITIAL load's failure becomes MapUiState.Error, so a transient network blip
    // doesn't blank a map the user is looking at. (An auth failure still reaches SessionManager
    // via SafeApiCaller, which swaps the whole app to the Tester Entry screen anyway.)
    private fun startLivePolling(satelliteId: String) {
        viewModelScope.launch {
            while (isActive) {
                delay(POSITION_POLL_INTERVAL_MILLIS)
                val result = mapRepository.getPosition(satelliteId)
                if (result is ApiResult.Success) {
                    updateLive { it.copy(currentPosition = result.data, footprintPolygon = footprintFor(result.data)) }
                }
            }
        }
        viewModelScope.launch {
            while (isActive) {
                delay(TRACK_POLL_INTERVAL_MILLIS)
                val result = mapRepository.getLiveTrack(satelliteId)
                if (result is ApiResult.Success) {
                    updateLive { it.copy(trackPoints = result.data) }
                }
            }
        }
    }

    private inline fun updateLive(transform: (MapUiState.LiveTrack) -> MapUiState.LiveTrack) {
        val current = _uiState.value
        if (current is MapUiState.LiveTrack) _uiState.value = transform(current)
    }

    private fun List<Satellite>.toNameMap(): Map<String, String> = associate { it.id to it.name }

    private fun footprintFor(position: SatellitePosition): List<LatLng> =
        GeoUtils.footprintPolygon(LatLng(position.latitude, position.longitude))

    private fun errorMessageFor(result: ApiResult<*>): String = when (result) {
        is ApiResult.Error -> result.message
        ApiResult.AuthRequired -> "Authentication required."
        ApiResult.NetworkError -> "No network connection."
        is ApiResult.Success -> error("errorMessageFor called with a Success result")
    }

    companion object {
        const val PASS_ID_ARG = "passId"
        const val SATELLITE_ID_ARG = "satelliteId"
        val POSITION_POLL_INTERVAL_MILLIS = TimeUnit.SECONDS.toMillis(15)
        val TRACK_POLL_INTERVAL_MILLIS = TimeUnit.MINUTES.toMillis(5)
    }
}
