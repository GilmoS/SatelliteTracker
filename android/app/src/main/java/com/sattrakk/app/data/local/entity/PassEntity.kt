package com.sattrakk.app.data.local.entity

import androidx.room.Entity
import androidx.room.PrimaryKey

// Cached copy of PassDto (see data/remote/dto). UUID/timestamp fields are stored as String/Long
// rather than java.util.UUID/OffsetDateTime so this entity needs no Room TypeConverters — mapping
// to/from the DTO and domain model happens in the repository layer (steps 2.2/2.3), not here.
@Entity(tableName = "passes")
data class PassEntity(
    @PrimaryKey val id: String,
    val satelliteId: String,
    val tleId: String,
    val orbitNumber: Int,
    val aosEpochMillis: Long,
    val losEpochMillis: Long,
    val maxElevation: Double,
    val aosAzimuth: Double,
    val losAzimuth: Double,
    val durationSec: Int,
    val outlookSynced: Boolean,
    val calculatedAtEpochMillis: Long
)
