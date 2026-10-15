package com.sattrakk.app.data.repository

import com.sattrakk.app.data.local.CacheMetadataDao
import com.sattrakk.app.data.local.SatelliteDao
import com.sattrakk.app.data.remote.SatTrakkApi
import com.sattrakk.app.data.util.SafeApiCaller
import com.sattrakk.app.data.util.cachedNetworkFirst
import com.sattrakk.app.domain.mapper.toDomain
import com.sattrakk.app.domain.mapper.toEntity
import com.sattrakk.app.domain.model.ApiResult
import com.sattrakk.app.domain.model.Satellite
import com.sattrakk.app.domain.model.mapSuccess
import java.util.concurrent.TimeUnit
import javax.inject.Inject
import javax.inject.Singleton

// TTL-gated network-first read of the satellite catalog — see android/CLAUDE.md's caching
// strategy section. 24h TTL: the backend has no cache TTL of its own for GET /api/satellites
// (it changes rarely), so this is a client-side-only choice, not matched to a backend value like
// Pass/Notes' 1h TTLs are.
@Singleton
class SatelliteRepository @Inject constructor(
    private val api: SatTrakkApi,
    private val satelliteDao: SatelliteDao,
    private val cacheMetadataDao: CacheMetadataDao,
    private val safeApiCall: SafeApiCaller
) {

    suspend fun getSatellites(forceRefresh: Boolean = false): ApiResult<List<Satellite>> =
        cachedNetworkFirst(
            cacheKey = CACHE_KEY_SATELLITES,
            ttlMillis = TTL_MILLIS,
            forceRefresh = forceRefresh,
            metadataDao = cacheMetadataDao,
            readCache = { satelliteDao.getCached().map { it.toDomain() } },
            fetchNetwork = {
                safeApiCall { api.getSatellites() }.mapSuccess { dtos -> dtos.map { it.toDomain() } }
            },
            writeCache = { satellites -> satelliteDao.replaceAll(satellites.map { it.toEntity() }) }
        )

    private companion object {
        const val CACHE_KEY_SATELLITES = "satellites"
        val TTL_MILLIS = TimeUnit.HOURS.toMillis(24)
    }
}
