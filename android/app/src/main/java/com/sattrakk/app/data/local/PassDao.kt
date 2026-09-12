package com.sattrakk.app.data.local

import androidx.room.Dao
import androidx.room.Insert
import androidx.room.OnConflictStrategy
import androidx.room.Query
import androidx.room.Transaction
import com.sattrakk.app.data.local.entity.PassEntity

// Just enough for the network-first-with-fallback pattern: read the cache, replace it wholesale
// on a successful fetch. Feature-specific queries (e.g. "notify=true passes" for the drawer)
// belong to whichever step actually builds that feature.
@Dao
interface PassDao {

    @Query("SELECT * FROM passes WHERE satelliteId = :satelliteId ORDER BY aosEpochMillis ASC")
    suspend fun getCachedForSatellite(satelliteId: String): List<PassEntity>

    @Query("SELECT * FROM passes WHERE id = :id")
    suspend fun getById(id: String): PassEntity?

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertAll(passes: List<PassEntity>)

    // Single-row insert-or-replace for PassRepository.getPassById's cold-deep-link fetch — NOT
    // replaceForSatellite, which deletes and replaces every row for a satellite and would wipe out
    // the rest of that satellite's cached passes for a fetch that only concerns one pass.
    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun upsert(pass: PassEntity)

    @Query("DELETE FROM passes WHERE satelliteId = :satelliteId")
    suspend fun deleteForSatellite(satelliteId: String)

    // Local filtered + paginated read for PassRepository.getPassHistory's Room-fresh-and-fully-
    // loaded path (Full Pass List screen, Milestone E). Mirrors the backend's own filter semantics
    // (aos range + maxElevation floor) and its "query pageSize + 1 rows, check for the extra one"
    // hasMore trick (see repo-root CLAUDE.md's paginated pass history section), so the Room path
    // and the network path behave identically from the caller's point of view. Each `:x IS NULL OR
    // ...` clause makes that bound a no-op when the caller's filter didn't set it.
    //
    // `losEpochMillis < :nowMillis` (round-2 fix, RUNNER-1 mixed Upcoming/History bug): the
    // backend's own GetHistoryAsync unconditionally filters `p.Los < DateTime.UtcNow` regardless of
    // any caller-supplied aos/elevation filter (see backend PassRepository.cs) -- "history" means
    // "already completed" there, always. This local query was missing that exact clause, so it
    // mirrored the OPTIONAL filters correctly but not the backend's ALWAYS-ON one. That only became
    // visible once a satellite's HistoryLoadState.isFullyLoaded flipped true (switching this method
    // from the always-correctly-bounded network path to this local path) -- which happens sooner
    // for a satellite with a small total historical-pass count (few pages to exhaust), such as
    // RUNNER-1. Once on this path, every still-upcoming pass already cached here via getPasses()
    // (same `passes` table) also matched this query's WHERE clause and leaked into "History"
    // results, making Upcoming and History look identical for that satellite -- see
    // android/CLAUDE.md's Milestone E round-2 section for the full diagnosis.
    @Query(
        """
        SELECT * FROM passes
        WHERE satelliteId = :satelliteId
        AND losEpochMillis < :nowMillis
        AND (:aosFromMillis IS NULL OR aosEpochMillis >= :aosFromMillis)
        AND (:aosToMillis IS NULL OR aosEpochMillis <= :aosToMillis)
        AND (:maxElevationFrom IS NULL OR maxElevation >= :maxElevationFrom)
        ORDER BY aosEpochMillis DESC
        LIMIT :limit OFFSET :offset
        """
    )
    suspend fun getFilteredForSatellite(
        satelliteId: String,
        nowMillis: Long,
        aosFromMillis: Long?,
        aosToMillis: Long?,
        maxElevationFrom: Double?,
        limit: Int,
        offset: Int
    ): List<PassEntity>

    // Used by PassRepository.setNotify to reflect a tester's own toggle immediately, without
    // waiting for the next TTL-driven refresh. A no-op if the pass isn't currently cached.
    @Query("UPDATE passes SET notify = :notify WHERE id = :id")
    suspend fun updateNotify(id: String, notify: Boolean)

    @Transaction
    suspend fun replaceForSatellite(satelliteId: String, passes: List<PassEntity>) {
        deleteForSatellite(satelliteId)
        insertAll(passes)
    }
}
