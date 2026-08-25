package com.sattrakk.app.data.local

import androidx.room.Database
import androidx.room.RoomDatabase
import com.sattrakk.app.data.local.entity.PassEntity
import com.sattrakk.app.data.local.entity.SatelliteEntity

// Room caches ONLY Pass and Satellite (network-first, fallback to offline on failure) — see
// backend CLAUDE.md's caching strategy. No other endpoint gets local caching.
@Database(entities = [PassEntity::class, SatelliteEntity::class], version = 1, exportSchema = false)
abstract class AppDatabase : RoomDatabase() {
    abstract fun passDao(): PassDao
    abstract fun satelliteDao(): SatelliteDao
}
