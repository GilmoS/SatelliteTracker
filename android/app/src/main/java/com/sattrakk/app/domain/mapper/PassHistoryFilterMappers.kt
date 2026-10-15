package com.sattrakk.app.domain.mapper

import com.sattrakk.app.domain.model.PassHistoryFilter
import com.sattrakk.app.domain.model.TimeWindow
import java.time.OffsetDateTime

// Resolved (aosFrom, aosTo, maxElevationFrom) query params for the backend's paginated pass
// history endpoint, and for the equivalent local Room filter in PassRepository.getPassHistory —
// see repo-root CLAUDE.md's paginated pass history section for the backend side of this shape.
//
// `isUnfiltered` gates PassRepository.getPassHistory's decision to mark a satellite's history as
// fully loaded (HistoryLoadStateEntity.isFullyLoaded) — see that method and android/CLAUDE.md.
// IMPORTANT: Last24h/Last48h/Last7Days always resolve a non-null aosFrom, so isUnfiltered is only
// ever true for TimeWindow.Custom(from = null, to = null) (or minMaxElevation left null alongside
// it) — there is no separate "All time" case. This is intentional per the "only the true end of
// the UNFILTERED dataset may set isFullyLoaded" rule; see TimeWindow's own doc comment for why
// Custom's bounds are nullable specifically to keep this path reachable at all, rather than
// permanently-dead code guarding a state nothing could ever produce.
data class ResolvedHistoryQuery(
    val aosFrom: OffsetDateTime?,
    val aosTo: OffsetDateTime?,
    val maxElevationFrom: Double?
) {
    val isUnfiltered: Boolean get() = aosFrom == null && aosTo == null && maxElevationFrom == null
}

fun PassHistoryFilter.resolve(now: OffsetDateTime): ResolvedHistoryQuery {
    val (aosFrom, aosTo) = when (val window = timeWindow) {
        is TimeWindow.Last24h -> now.minusHours(24) to null
        is TimeWindow.Last48h -> now.minusHours(48) to null
        is TimeWindow.Last7Days -> now.minusDays(7) to null
        is TimeWindow.Custom -> window.from to window.to
    }
    return ResolvedHistoryQuery(aosFrom, aosTo, minMaxElevation)
}
