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

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertAll(passes: List<PassEntity>)

    @Query("DELETE FROM passes WHERE satelliteId = :satelliteId")
    suspend fun deleteForSatellite(satelliteId: String)

    @Transaction
    suspend fun replaceForSatellite(satelliteId: String, passes: List<PassEntity>) {
        deleteForSatellite(satelliteId)
        insertAll(passes)
    }
}
