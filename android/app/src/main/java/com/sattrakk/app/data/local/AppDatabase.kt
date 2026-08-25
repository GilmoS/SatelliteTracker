package com.sattrakk.app.data.local

import androidx.room.Database
import androidx.room.RoomDatabase
import com.sattrakk.app.data.local.entity.CacheMetadataEntity
import com.sattrakk.app.data.local.entity.NoteEntity
import com.sattrakk.app.data.local.entity.PassEntity
import com.sattrakk.app.data.local.entity.SatelliteEntity

// Room caches Pass, Satellite, and (as of step 2.2) Note — all network-first, TTL-gated, falling
// back to the local cache only on a NetworkError (see repo-root and android CLAUDE.md's caching
// strategy). CacheMetadataEntity tracks per-key last-fetched timestamps for the TTL check; it
// isn't tied to any one entity because the fetch granularity differs per resource (all
// satellites; passes per satelliteId; notes per passId).
//
// Version bumped 1 -> 2 for the notify column on PassEntity plus the new notes/cache_metadata
// tables. No migration is provided (see DatabaseModule's fallbackToDestructiveMigration) — this
// app hasn't shipped yet, so there's no installed data worth preserving across the bump.
@Database(
    entities = [PassEntity::class, SatelliteEntity::class, NoteEntity::class, CacheMetadataEntity::class],
    version = 2,
    exportSchema = false
)
abstract class AppDatabase : RoomDatabase() {
    abstract fun passDao(): PassDao
    abstract fun satelliteDao(): SatelliteDao
    abstract fun noteDao(): NoteDao
    abstract fun cacheMetadataDao(): CacheMetadataDao
}
