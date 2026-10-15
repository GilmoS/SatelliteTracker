package com.sattrakk.app.domain.mapper

import com.sattrakk.app.data.local.entity.SatelliteEntity
import com.sattrakk.app.data.remote.dto.SatelliteDto
import com.sattrakk.app.domain.model.Satellite
import java.time.Instant
import java.time.OffsetDateTime
import java.time.ZoneOffset

// Every DTO field here is nullable only because of an openapi-generator codegen limitation (see
// android/CLAUDE.md, "Generated DTOs and the OpenAPI response-schema fix") — the backend never
// omits any of them on a successful response. requireNotNull documents that contract rather than
// defending against a real null case.
fun SatelliteDto.toDomain(): Satellite = Satellite(
    id = requireNotNull(id) { "SatelliteDto.id" }.toString(),
    name = requireNotNull(name) { "SatelliteDto.name" },
    noradId = requireNotNull(noradId) { "SatelliteDto.noradId" },
    description = description,
    isActive = requireNotNull(isActive) { "SatelliteDto.isActive" },
    isDefault = requireNotNull(isDefault) { "SatelliteDto.isDefault" },
    createdAt = requireNotNull(createdAt) { "SatelliteDto.createdAt" }
)

fun Satellite.toEntity(): SatelliteEntity = SatelliteEntity(
    id = id,
    name = name,
    noradId = noradId,
    description = description,
    isActive = isActive,
    isDefault = isDefault,
    createdAtEpochMillis = createdAt.toInstant().toEpochMilli()
)

fun SatelliteEntity.toDomain(): Satellite = Satellite(
    id = id,
    name = name,
    noradId = noradId,
    description = description,
    isActive = isActive,
    isDefault = isDefault,
    createdAt = OffsetDateTime.ofInstant(Instant.ofEpochMilli(createdAtEpochMillis), ZoneOffset.UTC)
)
