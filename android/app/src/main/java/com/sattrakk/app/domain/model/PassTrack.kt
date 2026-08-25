package com.sattrakk.app.domain.model

// Never Room-cached — the backend already caches this server-side for an hour, keyed by passId
// alone (repo-root CLAUDE.md's caching table); duplicating that client-side adds no value. See
// PassRepository.getPassTrack, a straight passthrough via safeApiCall.
data class PassTrack(
    val passId: String,
    val points: List<PassTrackPoint>
)

data class PassTrackPoint(
    val latitude: Double,
    val longitude: Double,
    val altitude: Double,
    val timestampEpochMillis: Long
)
