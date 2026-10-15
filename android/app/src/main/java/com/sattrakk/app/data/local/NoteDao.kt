package com.sattrakk.app.data.local

import androidx.room.Dao
import androidx.room.Insert
import androidx.room.OnConflictStrategy
import androidx.room.Query
import androidx.room.Transaction
import com.sattrakk.app.data.local.entity.NoteEntity

// Read side mirrors PassDao/SatelliteDao's network-first-with-fallback pattern. `insert` (single
// row, REPLACE) is what NotesRepository's create/update writes use to keep the cache consistent
// with a just-completed mutation without waiting for the next TTL-driven refresh — see
// android/CLAUDE.md.
@Dao
interface NoteDao {

    @Query("SELECT * FROM notes WHERE passId = :passId ORDER BY createdAtEpochMillis ASC")
    suspend fun getCachedForPass(passId: String): List<NoteEntity>

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insert(note: NoteEntity)

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertAll(notes: List<NoteEntity>)

    @Query("DELETE FROM notes WHERE passId = :passId")
    suspend fun deleteForPass(passId: String)

    @Query("DELETE FROM notes WHERE id = :id")
    suspend fun deleteById(id: String)

    @Transaction
    suspend fun replaceForPass(passId: String, notes: List<NoteEntity>) {
        deleteForPass(passId)
        insertAll(notes)
    }
}
