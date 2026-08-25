package com.sattrakk.app.data.local

import androidx.room.Dao
import androidx.room.Insert
import androidx.room.OnConflictStrategy
import androidx.room.Query
import com.sattrakk.app.data.local.entity.CacheMetadataEntity

@Dao
interface CacheMetadataDao {

    @Query("SELECT * FROM cache_metadata WHERE cacheKey = :cacheKey")
    suspend fun get(cacheKey: String): CacheMetadataEntity?

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun upsert(entity: CacheMetadataEntity)
}
