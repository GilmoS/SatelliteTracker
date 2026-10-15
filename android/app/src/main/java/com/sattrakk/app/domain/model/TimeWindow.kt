package com.sattrakk.app.domain.model

import java.time.OffsetDateTime

// Time-window filter for the Full Pass List screen's history portion (Milestone E). Resolves to
// the backend's aosFrom/aosTo query params (repo-root CLAUDE.md's paginated pass history section)
// via domain/mapper/PassHistoryFilterMappers.kt's `resolve`. Duration- and pass-direction filters
// seen in the design mockup are explicitly NOT implemented — those fields don't exist as backend
// filter params; only this (-> aosFrom/aosTo) and PassHistoryFilter.minMaxElevation
// (-> maxElevationFrom) are real. Flag this explicitly to whoever wires the Composable UI up.
//
// `Custom`'s bounds are deliberately nullable and independent (open-ended "from X onward" / "up to
// Y" / fully unbounded are all valid), not the non-null pair a literal reading of the original
// task sketch ("Custom(from, to)") might suggest. This isn't scope creep: PassRepository.getPassHistory's
// isFullyLoaded bookkeeping is only ever allowed to flip true from a query with NO aos bound at
// all (see PassHistoryFilterMappers.kt's `resolve`/`isUnfiltered`) — with Last24h/Last48h/Last7Days
// always resolving a non-null lower bound, and no separate "All time" case specified, a non-null-
// only Custom would make that path permanently unreachable (and untestable) dead code. A fully
// open Custom(null, null) is the one way this filter model can express "no time constraint."
sealed class TimeWindow {
    object Last24h : TimeWindow()
    object Last48h : TimeWindow()
    object Last7Days : TimeWindow()
    data class Custom(val from: OffsetDateTime?, val to: OffsetDateTime?) : TimeWindow()
}
