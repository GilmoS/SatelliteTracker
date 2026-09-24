package com.sattrakk.app.domain.mapper

import com.sattrakk.app.data.remote.dto.PositionDto
import com.sattrakk.app.data.remote.dto.TrackDto
import com.sattrakk.app.data.remote.dto.TrackPointDto
import com.sattrakk.app.domain.model.SatellitePosition
import com.sattrakk.app.domain.model.TrackPoint

// PositionDto/TrackPointDto timestamps are N2YO's Unix *seconds*, passed through unchanged by the
// backend — converted to millis here, the same convention PassTrackMappers uses.

fun PositionDto.toDomain(): SatellitePosition = SatellitePosition(
    latitude = requireNotNull(latitude) { "PositionDto.latitude" },
    longitude = requireNotNull(longitude) { "PositionDto.longitude" },
    altitude = requireNotNull(altitude) { "PositionDto.altitude" },
    azimuth = requireNotNull(azimuth) { "PositionDto.azimuth" },
    elevation = requireNotNull(elevation) { "PositionDto.elevation" },
    timestampEpochMillis = requireNotNull(timestamp) { "PositionDto.timestamp" } * 1000
)

fun TrackDto.toDomain(): List<TrackPoint> =
    requireNotNull(points) { "TrackDto.points" }.map { it.toDomain() }

fun TrackPointDto.toDomain(): TrackPoint = TrackPoint(
    latitude = requireNotNull(latitude) { "TrackPointDto.latitude" },
    longitude = requireNotNull(longitude) { "TrackPointDto.longitude" },
    altitude = requireNotNull(altitude) { "TrackPointDto.altitude" },
    timestampEpochMillis = requireNotNull(timestamp) { "TrackPointDto.timestamp" } * 1000
)
