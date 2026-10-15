package com.sattrakk.app.data.local

import androidx.room.Dao
import androidx.room.Insert
import androidx.room.OnConflictStrategy
import androidx.room.Query
import androidx.room.Transaction
import com.sattrakk.app.data.local.entity.SatelliteEntity

@Dao
interface SatelliteDao {

    @Query("SELECT * FROM satellites")
    suspend fun getCached(): List<SatelliteEntity>

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertAll(satellites: List<SatelliteEntity>)

    @Query("DELETE FROM satellites")
    suspend fun deleteAll()

    @Transaction
    suspend fun replaceAll(satellites: List<SatelliteEntity>) {
        deleteAll()
        insertAll(satellites)
    }
}
