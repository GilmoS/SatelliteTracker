package com.sattrakk.app.data.repository

import com.sattrakk.app.data.local.PassDao
import com.sattrakk.app.data.remote.SatTrakkApi
import com.sattrakk.app.data.util.SafeApiCaller
import com.sattrakk.app.domain.mapper.toDomain
import com.sattrakk.app.domain.model.ApiResult
import com.sattrakk.app.domain.model.Pass
import com.sattrakk.app.domain.model.SatellitePosition
import com.sattrakk.app.domain.model.TrackPoint
import com.sattrakk.app.domain.model.mapSuccess
import java.util.UUID
import javax.inject.Inject
import javax.inject.Singleton

// Map screen data. The two real-time reads are straight safeApiCall passthroughs with NO Room
// caching, same rationale as PassRepository.getPassTrack: the backend already caches them
// (position 30s, live track 5 min — repo-root CLAUDE.md's caching table), and the repo-root rule
// is that real-time position data is never stored. A client-side cache would add staleness, not
// value. The fixed per-pass track is NOT here — MapViewModel reuses PassRepository.getPassTrack.
@Singleton
class MapRepository @Inject constructor(
    private val api: SatTrakkApi,
    private val passDao: PassDao,
    private val safeApiCall: SafeApiCaller
) {

    suspend fun getPosition(satelliteId: String): ApiResult<SatellitePosition> =
        safeApiCall { api.getSatellitePosition(UUID.fromString(satelliteId)) }.mapSuccess { it.toDomain() }

    suspend fun getLiveTrack(satelliteId: String): ApiResult<List<TrackPoint>> =
        safeApiCall { api.getSatelliteTrack(UUID.fromString(satelliteId)) }.mapSuccess { it.toDomain() }

    // Local-only read: `notify` is client-cached state (see PassEntity), so Room is the source here,
    // not the network. Backs the Map drawer in both the live and the static-pass flow.
    suspend fun getNotifyEnabledPasses(): List<Pass> =
        passDao.getNotifyEnabled().map { it.toDomain() }
}
