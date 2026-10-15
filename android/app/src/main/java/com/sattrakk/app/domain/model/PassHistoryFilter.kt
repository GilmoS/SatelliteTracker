package com.sattrakk.app.domain.model

// Filter inputs for PassRepository.getPassHistory / the Full Pass List screen (Milestone E).
// `minMaxElevation` null means no elevation floor. See TimeWindow's doc comment: duration- and
// direction-based filters from the design mockup are NOT represented here — out of scope, since
// the backend doesn't support them yet.
data class PassHistoryFilter(
    val timeWindow: TimeWindow,
    val minMaxElevation: Double? = null
)
