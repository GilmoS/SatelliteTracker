package com.sattrakk.app.domain.util

import com.sattrakk.app.domain.model.Pass
import java.time.OffsetDateTime

// Shared by DashboardViewModel and FullPassListViewModel (see android/CLAUDE.md) to fix the
// "stale upcoming pass" bug found in the design-review round: a pass fetched while still upcoming
// can have its AOS pass while the TTL-gated cache that produced it is still considered fresh, so
// it keeps showing as "upcoming" until the next network refresh. This must be applied at
// state-computation/render time, recomputed against the current `now` each time — never baked
// into what gets cached, since the cached/fetched set should still represent everything the
// backend returned.
//
// Inclusive of exactly `now`: a pass whose AOS is this very instant is still treated as upcoming,
// not yet past (boundary case chosen deliberately, not the only reasonable choice).
fun List<Pass>.excludePastAos(now: OffsetDateTime): List<Pass> = filter { !it.aos.isBefore(now) }
