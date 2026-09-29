package com.sattrakk.app.ui.map

import com.sattrakk.app.domain.model.LatLng
import com.sattrakk.app.domain.model.Pass
import com.sattrakk.app.domain.model.PassTrackPoint
import com.sattrakk.app.domain.model.SatellitePosition
import com.sattrakk.app.domain.model.TrackPoint

// Two mutually exclusive flows, chosen once from the nav args (see MapViewModel):
//  - LiveTrack (no passId): the satellite's position right now, polled, plus its full orbit track
//    (one orbit back to one orbit ahead, from GET /api/satellites/{id}/orbit) and
//    a ~2000 km visibility footprint around the current sub-satellite point.
//  - StaticPassTrack (passId): one already-calculated pass's fixed ground track. No polling, no
//    footprint.
// The notify-enabled pass drawer is available in both. Its list never includes passes of hidden
// satellites (HiddenSatellitesStore); MapViewModel filters them out reactively.
//
// satelliteNames (satelliteId -> name, the whole catalog) exists for the drawer: Pass carries only
// satelliteId, and the drawer lists passes of every satellite, not just the one on screen.
sealed interface MapUiState {
    object Loading : MapUiState

    data class LiveTrack(
        val satelliteId: String,
        val satelliteName: String,
        val currentPosition: SatellitePosition,
        val trackPoints: List<TrackPoint>,
        val footprintPolygon: List<LatLng>,
        val notifyEnabledPasses: List<Pass>,
        val satelliteNames: Map<String, String>,
    ) : MapUiState

    // satelliteNames is empty if the catalog lookup failed — the drawer's names are secondary to
    // the pass track itself, so that failure doesn't turn the whole screen into an Error.
    data class StaticPassTrack(
        val pass: Pass,
        val trackPoints: List<PassTrackPoint>,
        val notifyEnabledPasses: List<Pass>,
        val satelliteNames: Map<String, String>,
    ) : MapUiState

    // Flow 1 only: the satellite being tracked was hidden in Settings (HiddenSatellitesStore) while
    // this Map instance was alive, or before it opened. Polling is stopped while in this state (see
    // MapViewModel.observeHiddenLiveSatellite). Unhiding it resumes polling and returns to
    // LiveTrack. The drawer stays available, so it still carries the drawer's fields.
    data class SatelliteHidden(
        val satelliteId: String,
        val satelliteName: String,
        val notifyEnabledPasses: List<Pass>,
        val satelliteNames: Map<String, String>,
    ) : MapUiState

    data class Error(val message: String) : MapUiState
}
