package com.sattrakk.app.data.local.entity

import androidx.room.Entity
import androidx.room.PrimaryKey

// Cached copy of NoteDto (see data/remote/dto). Same String/Long-over-UUID/OffsetDateTime choice
// as PassEntity/SatelliteEntity — see PassEntity for why. Unlike Pass/Satellite, notes are also
// user-editable; NotesRepository's writes (create/update/delete) update this cache immediately on
// success and never fall back to it on failure — see android/CLAUDE.md's caching strategy.
@Entity(tableName = "notes")
data class NoteEntity(
    @PrimaryKey val id: String,
    val passId: String,
    val content: String,
    val createdAtEpochMillis: Long,
    val updatedAtEpochMillis: Long
)
