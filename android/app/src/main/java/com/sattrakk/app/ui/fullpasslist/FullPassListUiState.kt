package com.sattrakk.app.ui.fullpasslist

import com.sattrakk.app.domain.model.Pass
import com.sattrakk.app.domain.model.TimeWindow

// Which portion(s) of the merged upcoming+history list the screen currently shows. See
// FullPassListViewModel for how each value sources/merges its data.
enum class PassListFilter { UPCOMING, HISTORY, ALL }

// Single flat state for the Full Pass List screen (Milestone E) — no Composable wired to this yet,
// see android/CLAUDE.md's "Full Pass List" section for the full design.
data class FullPassListUiState(
    val satelliteId: String,
    val satelliteName: String,
    val filter: PassListFilter,
    val timeWindow: TimeWindow,
    val minMaxElevation: Double?,
    val passes: List<Pass>,
    // Boundary marker between the upcoming and history portions of the merged ALL list — see
    // FullPassListViewModel.computeNearestPassId. Always null for UPCOMING/HISTORY-only views,
    // since there's no boundary to mark when only one portion is shown.
    val nearestPassId: String?,
    // Doubles as "initial load in progress" (true from construction until the first load
    // completes) and "loadMore() in progress" — both represent "don't issue another fetch/render a
    // spinner right now," and this screen has no separate use for distinguishing the two.
    val isLoadingMore: Boolean,
    val hasMoreHistory: Boolean,
    val error: String?
)
