package com.sattrakk.app.data.util

import com.sattrakk.app.data.local.CacheMetadataDao
import com.sattrakk.app.data.local.entity.CacheMetadataEntity
import com.sattrakk.app.domain.model.ApiResult

// The single implementation of the TTL-gated, network-first, stale-on-NetworkError-only caching
// strategy shared by SatelliteRepository/PassRepository/NotesRepository's list reads — see
// android/CLAUDE.md's caching strategy section. Every repository following this pattern should
// call this rather than re-implementing the decision tree.
//
// "Is there any cache at all for this key" is decided from whether a CacheMetadata row exists for
// `cacheKey`, NOT from whether `readCache()` returns an empty list — an empty list is a valid
// cached result (e.g. a satellite with zero upcoming passes), distinct from "never fetched."
suspend fun <T> cachedNetworkFirst(
    cacheKey: String,
    ttlMillis: Long,
    forceRefresh: Boolean,
    metadataDao: CacheMetadataDao,
    readCache: suspend () -> List<T>,
    fetchNetwork: suspend () -> ApiResult<List<T>>,
    writeCache: suspend (List<T>) -> Unit
): ApiResult<List<T>> {
    val metadata = metadataDao.get(cacheKey)
    val now = System.currentTimeMillis()
    val isFresh = metadata != null && now - metadata.lastFetchedAtEpochMillis < ttlMillis

    if (!forceRefresh && isFresh) {
        return ApiResult.Success(readCache())
    }

    return when (val networkResult = fetchNetwork()) {
        is ApiResult.Success -> {
            writeCache(networkResult.data)
            metadataDao.upsert(CacheMetadataEntity(cacheKey, now))
            networkResult
        }
        is ApiResult.NetworkError -> {
            if (metadata != null) ApiResult.Success(readCache()) else ApiResult.NetworkError
        }
        else -> networkResult
    }
}
