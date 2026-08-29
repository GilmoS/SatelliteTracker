package com.sattrakk.app.data.local

import androidx.room.Dao
import androidx.room.Insert
import androidx.room.OnConflictStrategy
import androidx.room.Query
import androidx.room.Transaction
import com.sattrakk.app.data.local.entity.PassEntity

// Just enough for the network-first-with-fallback pattern: read the cache, replace it wholesale
// on a successful fetch. Feature-specific queries (e.g. "notify=true passes" for the drawer)
// belong to whichever step actually builds that feature.
@Dao
interface PassDao {

    @Query("SELECT * FROM passes WHERE satelliteId = :satelliteId ORDER BY aosEpochMillis ASC")
    suspend fun getCachedForSatellite(satelliteId: String): List<PassEntity>

    @Query("SELECT * FROM passes WHERE id = :id")
    suspend fun getById(id: String): PassEntity?

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertAll(passes: List<PassEntity>)

    // Single-row insert-or-replace for PassRepository.getPassById's cold-deep-link fetch — NOT
    // replaceForSatellite, which deletes and replaces every row for a satellite and would wipe out
    // the rest of that satellite's cached passes for a fetch that only concerns one pass.
    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun upsert(pass: PassEntity)

    @Query("DELETE FROM passes WHERE satelliteId = :satelliteId")
    suspend fun deleteForSatellite(satelliteId: String)

    // Used by PassRepository.setNotify to reflect a tester's own toggle immediately, without
    // waiting for the next TTL-driven refresh. A no-op if the pass isn't currently cached.
    @Query("UPDATE passes SET notify = :notify WHERE id = :id")
    suspend fun updateNotify(id: String, notify: Boolean)

    @Transaction
    suspend fun replaceForSatellite(satelliteId: String, passes: List<PassEntity>) {
        deleteForSatellite(satelliteId)
        insertAll(passes)
    }
}
