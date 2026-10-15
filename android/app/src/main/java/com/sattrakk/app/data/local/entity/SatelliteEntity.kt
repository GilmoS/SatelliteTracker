package com.sattrakk.app.data.local.entity

import androidx.room.Entity
import androidx.room.PrimaryKey

// Cached copy of SatelliteDto (see data/remote/dto). Same String/Long-over-UUID/OffsetDateTime
// choice as PassEntity — see that file for why.
@Entity(tableName = "satellites")
data class SatelliteEntity(
    @PrimaryKey val id: String,
    val name: String,
    val noradId: Int,
    val description: String?,
    val isActive: Boolean,
    val isDefault: Boolean,
    val createdAtEpochMillis: Long
)
