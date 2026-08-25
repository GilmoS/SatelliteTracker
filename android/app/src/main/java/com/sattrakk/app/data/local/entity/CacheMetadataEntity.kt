package com.sattrakk.app.data.local.entity

import androidx.room.Entity
import androidx.room.PrimaryKey

// One row per TTL-gated cache key (e.g. "satellites", "passes:{satelliteId}", "notes:{passId}")
// — see android/CLAUDE.md's caching strategy section. This is the single source of truth for
// "is there any cached data at all for this key" (row absent) vs. "cached, how fresh" (row
// present, compare lastFetchedAtEpochMillis against the resource's TTL) — deliberately NOT
// inferred from whether the corresponding Pass/Satellite/Note rows list happens to be empty,
// since a satellite can legitimately have zero passes, or a pass zero notes, and that's still a
// real (empty) cached result, not "never fetched."
@Entity(tableName = "cache_metadata")
data class CacheMetadataEntity(
    @PrimaryKey val cacheKey: String,
    val lastFetchedAtEpochMillis: Long
)
