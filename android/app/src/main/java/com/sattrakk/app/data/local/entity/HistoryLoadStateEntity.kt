package com.sattrakk.app.data.local.entity

import androidx.room.Entity
import androidx.room.PrimaryKey

// Load-state tracking for the Full Pass List screen's paginated history fetch (Milestone E) — NOT
// a duplicate of PassEntity/PassDao. Historical and upcoming passes share the exact same `passes`
// table (consistent with the backend's own single-table model, see repo-root and android
// CLAUDE.md); this table only tracks whether THIS satellite's full, unfiltered history has ever
// been paginated all the way to its end, distinct from CacheMetadataEntity's simple time-based TTL
// freshness (which answers "was this fetched within the TTL", not "was ALL of it ever fetched").
//
// `isFullyLoaded` is set true ONLY by an unfiltered fetch (no time-window/elevation filter
// constraints) reaching hasMore = false — see PassRepository.getPassHistory and android/CLAUDE.md
// for why a filtered fetch's own hasMore = false must never set this. `lastVerifiedAtEpochMillis`
// is then used the same way CacheMetadataEntity's timestamp is: gating a 1h freshness window so a
// fully-loaded satellite doesn't need to be re-verified against the backend on every screen visit.
@Entity(tableName = "history_load_state")
data class HistoryLoadStateEntity(
    @PrimaryKey val satelliteId: String,
    val isFullyLoaded: Boolean,
    val lastVerifiedAtEpochMillis: Long
)
