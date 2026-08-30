package com.sattrakk.app.data.repository

import com.sattrakk.app.data.local.CacheMetadataDao
import com.sattrakk.app.data.local.HistoryLoadStateDao
import com.sattrakk.app.data.local.PassDao
import com.sattrakk.app.data.local.entity.HistoryLoadStateEntity
import com.sattrakk.app.data.remote.SatTrakkApi
import com.sattrakk.app.data.remote.dto.PatchNotifyRequest
import com.sattrakk.app.data.util.SafeApiCaller
import com.sattrakk.app.data.util.cachedNetworkFirst
import com.sattrakk.app.di.ApplicationScope
import com.sattrakk.app.domain.mapper.resolve
import com.sattrakk.app.domain.mapper.toDomain
import com.sattrakk.app.domain.mapper.toEntity
import com.sattrakk.app.domain.model.ApiResult
import com.sattrakk.app.domain.model.NotifyStatus
import com.sattrakk.app.domain.model.PagedResult
import com.sattrakk.app.domain.model.Pass
import com.sattrakk.app.domain.model.PassHistoryFilter
import com.sattrakk.app.domain.model.PassTrack
import com.sattrakk.app.domain.model.mapSuccess
import java.time.Clock
import java.time.OffsetDateTime
import java.util.UUID
import java.util.concurrent.TimeUnit
import javax.inject.Inject
import javax.inject.Singleton
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.launch

@Singleton
class PassRepository @Inject constructor(
    private val api: SatTrakkApi,
    private val passDao: PassDao,
    private val cacheMetadataDao: CacheMetadataDao,
    private val safeApiCall: SafeApiCaller,
    @ApplicationScope private val applicationScope: CoroutineScope,
    private val historyLoadStateDao: HistoryLoadStateDao,
    private val clock: Clock
) {

    // TTL-gated network-first read, keyed per satelliteId — see android/CLAUDE.md. `notify` isn't
    // on PassDto (see PassEntity's doc comment), so a fresh network fetch merges in whatever
    // notify value is already cached locally for each pass id, defaulting to true (the backend's
    // own sparse-opt-out default) only for a pass seen for the first time. Without this merge, a
    // tester's own setNotify toggle would be silently reverted by the very next TTL-driven or
    // force-refresh fetch.
    suspend fun getPasses(satelliteId: String, forceRefresh: Boolean = false): ApiResult<List<Pass>> =
        cachedNetworkFirst(
            cacheKey = cacheKeyForSatellite(satelliteId),
            ttlMillis = PASSES_TTL_MILLIS,
            forceRefresh = forceRefresh,
            metadataDao = cacheMetadataDao,
            readCache = { passDao.getCachedForSatellite(satelliteId).map { it.toDomain() } },
            fetchNetwork = {
                val cachedNotifyById = passDao.getCachedForSatellite(satelliteId)
                    .associate { it.id to it.notify }
                safeApiCall { api.getUpcomingPasses(UUID.fromString(satelliteId)) }.mapSuccess { dtos ->
                    dtos.map { dto -> dto.toDomain(notify = cachedNotifyById[dto.id.toString()] ?: true) }
                }
            },
            writeCache = { passes -> passDao.replaceForSatellite(satelliteId, passes.map { it.toEntity() }) }
        )

    // No Room caching — the backend already caches this server-side for 1h per passId (repo-root
    // CLAUDE.md's caching table); a client-side cache on top would add no value. Straight
    // passthrough via safeApiCall.
    suspend fun getPassTrack(passId: String): ApiResult<PassTrack> =
        safeApiCall { api.getPassTrack(UUID.fromString(passId)) }.mapSuccess { it.toDomain() }

    // Room-first, network-fallback single-pass lookup for the cold-deep-link case (app opened
    // fresh from a push notification, or any other case where this pass was never loaded via
    // getPasses). Deliberately does NOT participate in the TTL/CacheMetadata system — this is a
    // point lookup, not a refresh of the satellite's passes-list cache, so it never reads or
    // writes that list's CacheMetadata row. See android/CLAUDE.md.
    suspend fun getPassById(passId: String): ApiResult<Pass> {
        val cached = passDao.getById(passId)
        if (cached != null) {
            return ApiResult.Success(cached.toDomain())
        }

        // No prior local value to preserve for a pass never seen before — same default-for-new-
        // pass rule getPasses' merge logic already uses.
        val result = safeApiCall { api.getPassById(UUID.fromString(passId)) }
            .mapSuccess { it.toDomain(notify = true) }

        if (result is ApiResult.Success) {
            passDao.upsert(result.data.toEntity())
            // Fire-and-forget "since we're here" convenience refresh of the pass's own
            // satellite's list, so the Dashboard is more likely to already show this pass on
            // return — not a correctness requirement, so it must never affect what this method
            // returns. Launched on the application-lifetime scope (not viewModelScope) since the
            // caller (e.g. a dialog-scoped ViewModel) may be cleared before this finishes.
            applicationScope.launch { getPasses(result.data.satelliteId, forceRefresh = false) }
        }

        return result
    }

    // On success, updates the cached PassEntity's notify column immediately so the pass list the
    // tester is looking at reflects their own toggle right away, rather than up to PASSES_TTL_MILLIS
    // later. On any non-Success result, the local cache is left untouched — the toggle didn't
    // actually take effect.
    suspend fun setNotify(passId: String, notify: Boolean): ApiResult<NotifyStatus> {
        val result = safeApiCall {
            api.patchPassNotify(UUID.fromString(passId), PatchNotifyRequest(notify = notify))
        }
        if (result is ApiResult.Success) {
            passDao.updateNotify(passId, notify)
        }
        return result.mapSuccess { it.toDomain() }
    }

    // Paginated, filterable per-satellite pass history for the Full Pass List screen (Milestone
    // E) — see android/CLAUDE.md's "Full Pass List" section for the full decision tree.
    //
    // 1. If HistoryLoadStateEntity says this satellite's full history is loaded AND that record is
    //    still within the freshness window, serve entirely from Room — filter + paginate locally,
    //    zero network calls, for ANY filter/page (not just the unfiltered case).
    // 2. Otherwise call the backend's paginated history endpoint, upsert results into the existing
    //    PassDao (single-row `upsert`, the same one getPassById uses — never `replaceForSatellite`,
    //    which would wipe out the rest of this satellite's cached passes).
    // 3. IMPORTANT, easy to get backwards: a network fetch only marks isFullyLoaded = true when
    //    BOTH hasMore is false AND the query was unfiltered (ResolvedHistoryQuery.isUnfiltered) —
    //    a filtered fetch (or a mid-pagination one) reaching the end of ITS OWN result set says
    //    nothing about whether the satellite's full, unfiltered history is cached. See
    //    domain/mapper/PassHistoryFilterMappers.kt and TimeWindow's doc comments: under
    //    Last24h/Last48h/Last7Days this branch never fires (they always resolve a non-null
    //    aosFrom) — only TimeWindow.Custom(null, null) with no minMaxElevation reaches it.
    suspend fun getPassHistory(
        satelliteId: String,
        page: Int,
        filter: PassHistoryFilter
    ): ApiResult<PagedResult<Pass>> {
        val now = OffsetDateTime.now(clock)
        val query = filter.resolve(now)

        val loadState = historyLoadStateDao.get(satelliteId)
        val isFreshAndFullyLoaded = loadState != null &&
            loadState.isFullyLoaded &&
            now.toInstant().toEpochMilli() - loadState.lastVerifiedAtEpochMillis < HISTORY_FRESHNESS_TTL_MILLIS

        if (isFreshAndFullyLoaded) {
            val rows = passDao.getFilteredForSatellite(
                satelliteId = satelliteId,
                aosFromMillis = query.aosFrom?.toInstant()?.toEpochMilli(),
                aosToMillis = query.aosTo?.toInstant()?.toEpochMilli(),
                maxElevationFrom = query.maxElevationFrom,
                limit = HISTORY_PAGE_SIZE + 1,
                offset = (page - 1) * HISTORY_PAGE_SIZE
            )
            val hasMore = rows.size > HISTORY_PAGE_SIZE
            return ApiResult.Success(
                PagedResult(
                    items = rows.take(HISTORY_PAGE_SIZE).map { it.toDomain() },
                    page = page,
                    pageSize = HISTORY_PAGE_SIZE,
                    hasMore = hasMore
                )
            )
        }

        val networkResult = safeApiCall {
            api.getPassHistory(
                satelliteId = UUID.fromString(satelliteId),
                page = page,
                pageSize = HISTORY_PAGE_SIZE,
                maxElevationFrom = query.maxElevationFrom,
                aosFrom = query.aosFrom,
                aosTo = query.aosTo
            )
        }

        return networkResult.mapSuccess { dto ->
            // Same notify-preservation rule as getPasses/getPassById's merge: a pass id already
            // cached keeps whatever notify value it has locally; a pass seen for the first time
            // defaults to true (the backend's own sparse opt-out default).
            val passes = dto.items.orEmpty().map { passDto ->
                val id = requireNotNull(passDto.id) { "PassDto.id" }.toString()
                val existingNotify = passDao.getById(id)?.notify ?: true
                passDto.toDomain(notify = existingNotify)
            }
            passes.forEach { passDao.upsert(it.toEntity()) }

            val hasMore = dto.hasMore ?: false
            if (query.isUnfiltered && !hasMore) {
                historyLoadStateDao.upsert(
                    HistoryLoadStateEntity(
                        satelliteId = satelliteId,
                        isFullyLoaded = true,
                        lastVerifiedAtEpochMillis = now.toInstant().toEpochMilli()
                    )
                )
            }

            PagedResult(
                items = passes,
                page = dto.page ?: page,
                pageSize = dto.pageSize ?: HISTORY_PAGE_SIZE,
                hasMore = hasMore
            )
        }
    }

    private companion object {
        val PASSES_TTL_MILLIS = TimeUnit.HOURS.toMillis(1)
        val HISTORY_FRESHNESS_TTL_MILLIS = TimeUnit.HOURS.toMillis(1)
        const val HISTORY_PAGE_SIZE = 50
        fun cacheKeyForSatellite(satelliteId: String) = "passes:$satelliteId"
    }
}
