package com.sattrakk.app.data.local

import androidx.room.Dao
import androidx.room.Insert
import androidx.room.OnConflictStrategy
import androidx.room.Query
import com.sattrakk.app.data.local.entity.HistoryLoadStateEntity

@Dao
interface HistoryLoadStateDao {

    @Query("SELECT * FROM history_load_state WHERE satelliteId = :satelliteId")
    suspend fun get(satelliteId: String): HistoryLoadStateEntity?

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun upsert(entity: HistoryLoadStateEntity)
}
