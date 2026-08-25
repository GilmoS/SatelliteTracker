package com.sattrakk.app.data.local.entity

import androidx.room.Entity
import androidx.room.PrimaryKey

// Cached copy of PassDto (see data/remote/dto). UUID/timestamp fields are stored as String/Long
// rather than java.util.UUID/OffsetDateTime so this entity needs no Room TypeConverters — mapping
// to/from the DTO and domain model happens in the repository layer (step 2.2+).
//
// `notify` is NOT on PassDto — it's per-tester sparse opt-out state that lives server-side in
// PassSubscription (see repo-root CLAUDE.md), not on Pass. It's a local-only column: populated
// from PassRepository's TTL-refresh merge (preserving whatever was already cached, defaulting to
// true for a pass seen for the first time) and updated immediately on a successful
// PassRepository.setNotify call. See android/CLAUDE.md's caching strategy section.
@Entity(tableName = "passes")
data class PassEntity(
    @PrimaryKey val id: String,
    val satelliteId: String,
    val tleId: String,
    val orbitNumber: Int,
    val aosEpochMillis: Long,
    val losEpochMillis: Long,
    val maxElevation: Double,
    val aosAzimuth: Double,
    val losAzimuth: Double,
    val durationSec: Int,
    val notify: Boolean,
    val outlookSynced: Boolean,
    val calculatedAtEpochMillis: Long
)
