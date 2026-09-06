package com.sattrakk.app.ui.dashboard

import com.sattrakk.app.domain.model.Pass
import java.time.Duration

// Deliberately generic rather than naming specific satellites (EROS C3 / RUNNER 1) — the
// satellites list already comes from the backend and may grow, so nothing here should assume a
// fixed set or count of tabs. See android/CLAUDE.md.
data class SatelliteTabState(
    val satelliteId: String,
    val satelliteName: String,
    val passes: List<Pass>,
    val nextPassCountdown: Duration?, // null if there's no upcoming pass at all
    // The same pass nextPassCountdown counts down to — i.e. the earliest pass in `passes` whose
    // AOS is still in the future, per DashboardViewModel's countdown-ticker derivation. Exposed
    // here (rather than recomputed in the Composable layer) so the UI's hero-pass card can render
    // the actual Pass fields (orbit/AOS/LOS/max elevation) without duplicating that "find nearest
    // future pass" logic a second time. Like nextPassCountdown, only ever populated for the
    // selected tab — see DashboardViewModel and android/CLAUDE.md.
    val nextPass: Pass? = null,
    // Per-tab load-failure signal — not in the original spec shape, added so one satellite's
    // passes call failing doesn't blank out tabs that loaded fine. Null means this tab's passes
    // are (still) good data, either freshly loaded or left over from a prior successful load; a
    // non-null message means the most recent load/poll/refresh for THIS tab failed and `passes`
    // is whatever was last known (possibly empty, if it has never succeeded). See
    // android/CLAUDE.md and DashboardViewModel's report for why this was chosen over failing the
    // whole screen.
    val loadError: String? = null
)

sealed interface DashboardUiState {
    object Loading : DashboardUiState
    data class Content(
        val tabs: List<SatelliteTabState>,
        val selectedSatelliteId: String
    ) : DashboardUiState
    data class Error(val message: String) : DashboardUiState
}
