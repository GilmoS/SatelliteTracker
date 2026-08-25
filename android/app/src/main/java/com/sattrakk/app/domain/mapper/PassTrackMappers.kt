package com.sattrakk.app.domain.mapper

import com.sattrakk.app.data.remote.dto.PassTrackDto
import com.sattrakk.app.data.remote.dto.PassTrackPointDto
import com.sattrakk.app.domain.model.PassTrack
import com.sattrakk.app.domain.model.PassTrackPoint

fun PassTrackDto.toDomain(): PassTrack = PassTrack(
    passId = requireNotNull(passId) { "PassTrackDto.passId" }.toString(),
    points = requireNotNull(points) { "PassTrackDto.points" }.map { it.toDomain() }
)

fun PassTrackPointDto.toDomain(): PassTrackPoint = PassTrackPoint(
    latitude = requireNotNull(latitude) { "PassTrackPointDto.latitude" },
    longitude = requireNotNull(longitude) { "PassTrackPointDto.longitude" },
    altitude = requireNotNull(altitude) { "PassTrackPointDto.altitude" },
    timestampEpochMillis = requireNotNull(timestamp) { "PassTrackPointDto.timestamp" }
)
