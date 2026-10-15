package com.sattrakk.app.domain.model

import java.time.OffsetDateTime

// `notify` has no equivalent on the backend's PassDto — see PassEntity's doc comment for why it's
// carried here as local-only, tester-toggleable state.
data class Pass(
    val id: String,
    val satelliteId: String,
    val tleId: String,
    val orbitNumber: Int,
    val aos: OffsetDateTime,
    val los: OffsetDateTime,
    val maxElevation: Double,
    val aosAzimuth: Double,
    val losAzimuth: Double,
    val durationSec: Int,
    val notify: Boolean,
    val outlookSynced: Boolean,
    val calculatedAt: OffsetDateTime
)
