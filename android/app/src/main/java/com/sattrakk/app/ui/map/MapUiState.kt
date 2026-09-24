package com.sattrakk.app.ui.map

import com.sattrakk.app.domain.model.LatLng
import com.sattrakk.app.domain.model.Pass
import com.sattrakk.app.domain.model.PassTrackPoint
import com.sattrakk.app.domain.model.SatellitePosition
import com.sattrakk.app.domain.model.TrackPoint

// Two mutually exclusive flows, chosen once from the nav args (see MapViewModel):
//  - LiveTrack (no passId): the satellite's position right now, polled, plus its live track and
//    a ~2000 km visibility footprint around the current sub-satellite point.
//  - StaticPassTrack (passId): one already-calculated pass's fixed ground track. No polling, no
//    footprint.
// The notify-enabled pass drawer is available in both.
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

    data class Error(val message: String) : MapUiState
}
