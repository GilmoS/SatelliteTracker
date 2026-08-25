package com.sattrakk.app.domain.mapper

import com.sattrakk.app.data.local.entity.PassEntity
import com.sattrakk.app.data.remote.dto.PassDto
import com.sattrakk.app.domain.model.Pass
import java.time.Instant
import java.time.OffsetDateTime
import java.time.ZoneOffset

// `notify` isn't on PassDto at all — see PassEntity/Pass's doc comments for why. The caller
// supplies it: PassRepository merges in whatever notify value was already cached locally
// (defaulting to true for a pass seen for the first time) before this mapper ever runs, so the
// mapper itself stays a pure DTO-plus-known-value -> domain transform.
fun PassDto.toDomain(notify: Boolean): Pass = Pass(
    id = requireNotNull(id) { "PassDto.id" }.toString(),
    satelliteId = requireNotNull(satelliteId) { "PassDto.satelliteId" }.toString(),
    tleId = requireNotNull(tleId) { "PassDto.tleId" }.toString(),
    orbitNumber = requireNotNull(orbitNumber) { "PassDto.orbitNumber" },
    aos = requireNotNull(aos) { "PassDto.aos" },
    los = requireNotNull(los) { "PassDto.los" },
    maxElevation = requireNotNull(maxElevation) { "PassDto.maxElevation" },
    aosAzimuth = requireNotNull(aosAzimuth) { "PassDto.aosAzimuth" },
    losAzimuth = requireNotNull(losAzimuth) { "PassDto.losAzimuth" },
    durationSec = requireNotNull(durationSec) { "PassDto.durationSec" },
    notify = notify,
    outlookSynced = requireNotNull(outlookSynced) { "PassDto.outlookSynced" },
    calculatedAt = requireNotNull(calculatedAt) { "PassDto.calculatedAt" }
)

fun Pass.toEntity(): PassEntity = PassEntity(
    id = id,
    satelliteId = satelliteId,
    tleId = tleId,
    orbitNumber = orbitNumber,
    aosEpochMillis = aos.toInstant().toEpochMilli(),
    losEpochMillis = los.toInstant().toEpochMilli(),
    maxElevation = maxElevation,
    aosAzimuth = aosAzimuth,
    losAzimuth = losAzimuth,
    durationSec = durationSec,
    notify = notify,
    outlookSynced = outlookSynced,
    calculatedAtEpochMillis = calculatedAt.toInstant().toEpochMilli()
)

fun PassEntity.toDomain(): Pass = Pass(
    id = id,
    satelliteId = satelliteId,
    tleId = tleId,
    orbitNumber = orbitNumber,
    aos = OffsetDateTime.ofInstant(Instant.ofEpochMilli(aosEpochMillis), ZoneOffset.UTC),
    los = OffsetDateTime.ofInstant(Instant.ofEpochMilli(losEpochMillis), ZoneOffset.UTC),
    maxElevation = maxElevation,
    aosAzimuth = aosAzimuth,
    losAzimuth = losAzimuth,
    durationSec = durationSec,
    notify = notify,
    outlookSynced = outlookSynced,
    calculatedAt = OffsetDateTime.ofInstant(Instant.ofEpochMilli(calculatedAtEpochMillis), ZoneOffset.UTC)
)
