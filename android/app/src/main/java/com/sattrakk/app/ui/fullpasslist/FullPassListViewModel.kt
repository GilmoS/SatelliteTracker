package com.sattrakk.app.ui.fullpasslist

import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.sattrakk.app.data.repository.PassRepository
import com.sattrakk.app.domain.model.ApiResult
import com.sattrakk.app.domain.model.Pass
import com.sattrakk.app.domain.model.PassHistoryFilter
import com.sattrakk.app.domain.model.TimeWindow
import com.sattrakk.app.domain.util.excludePastAos
import dagger.hilt.android.lifecycle.HiltViewModel
import java.time.Clock
import java.time.OffsetDateTime
import javax.inject.Inject
import kotlinx.coroutines.async
import kotlinx.coroutines.coroutineScope
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

// Screen state + orchestration for the Full Pass List screen (Milestone E) — a separate
// destination from the Dashboard, one continuous mixed-chronology list (already-loaded upcoming
// passes + paginated historical passes) for a single satellite, with an Upcoming/History/All/
// Filtered segmented control plus a time-window and minimum-elevation filter. See
// android/CLAUDE.md's "Full Pass List" section for the full design, including the FILTERED
// segment added in the design-review bug-fix round.
//
// satelliteId/satelliteName come from nav args via SavedStateHandle (the Dashboard already knows
// both when the user taps into this screen) rather than a second lookup call.
@HiltViewModel
class FullPassListViewModel @Inject constructor(
    private val passRepository: PassRepository,
    private val clock: Clock,
    savedStateHandle: SavedStateHandle
) : ViewModel() {

    private val satelliteId: String = requireNotNull(savedStateHandle["satelliteId"]) { "satelliteId nav arg" }
    private val satelliteName: String = requireNotNull(savedStateHandle["satelliteName"]) { "satelliteName nav arg" }

    private val _uiState = MutableStateFlow(
        FullPassListUiState(
            satelliteId = satelliteId,
            satelliteName = satelliteName,
            filter = PassListFilter.ALL,
            timeWindow = DEFAULT_TIME_WINDOW,
            minMaxElevation = DEFAULT_MIN_MAX_ELEVATION,
            passes = emptyList(),
            nearestPassId = null,
            isLoadingMore = true,
            hasMoreHistory = false,
            error = null
        )
    )
    val uiState: StateFlow<FullPassListUiState> = _uiState.asStateFlow()

    // 1-based backend/Room page cursor for the history/filtered portion only — UPCOMING has no
    // pagination.  Reset to 1 by reload(); advanced by loadMore().
    private var historyPage = 1

    init {
        reload()
    }

    // UPCOMING/HISTORY/ALL are pure time-based views and never read timeWindow/minMaxElevation —
    // only setFilter(FILTERED) (driven internally by setTimeWindow/setMinMaxElevation below, or
    // directly if the caller already knows a filter is active) does. Resets pagination and
    // rebuilds the list from scratch — per android/CLAUDE.md, a filter change is effectively a new
    // query, not an incremental update. There's no free-text search on this screen, so there's
    // deliberately no "restore scroll position" behavior to preserve here, unlike a search-clearing
    // flow.
    fun setFilter(filter: PassListFilter) {
        if (_uiState.value.filter == filter) return
        _uiState.value = _uiState.value.copy(filter = filter)
        reload()
    }

    // Selecting ANY filter value — a non-default time window or elevation floor — immediately and
    // automatically activates the FILTERED segment (closing the Filter Modal is the Composable
    // layer's job, see FullPassListScreen). This is not a separate user action; choosing a filter
    // IS what activates FILTERED. See android/CLAUDE.md and PassListFilter's doc comment.
    fun setTimeWindow(timeWindow: TimeWindow) {
        if (_uiState.value.timeWindow == timeWindow) return
        _uiState.value = _uiState.value.copy(timeWindow = timeWindow)
        applyFilterActivation()
    }

    fun setMinMaxElevation(minMaxElevation: Double?) {
        if (_uiState.value.minMaxElevation == minMaxElevation) return
        _uiState.value = _uiState.value.copy(minMaxElevation = minMaxElevation)
        applyFilterActivation()
    }

    // Switches to FILTERED if either field is now non-default, or back to ALL if a change (e.g.
    // clearing the elevation floor directly) brought both back to default while FILTERED was
    // showing — "FILTERED is never shown with no active filter." Always reloads: even when the
    // target segment doesn't change, the query params driving FILTERED's own fetch did.
    private fun applyFilterActivation() {
        val current = _uiState.value
        val isFilterActive = current.timeWindow != DEFAULT_TIME_WINDOW || current.minMaxElevation != DEFAULT_MIN_MAX_ELEVATION
        val targetFilter = if (isFilterActive) PassListFilter.FILTERED else PassListFilter.ALL
        _uiState.value = current.copy(filter = targetFilter)
        reload()
    }

    // Resets only timeWindow/minMaxElevation, not `filter` directly — except that per
    // PassListFilter's design, FILTERED can never be shown with no active filter, so resetting
    // while on FILTERED auto-returns to ALL (and reloads it). Resetting while on UPCOMING/HISTORY/
    // ALL leaves the segment untouched and skips the reload entirely -- those views never read
    // these fields, so their currently-shown data can't be stale with respect to them.
    fun resetFilters() {
        val current = _uiState.value
        if (current.timeWindow == DEFAULT_TIME_WINDOW && current.minMaxElevation == DEFAULT_MIN_MAX_ELEVATION) return
        val wasFiltered = current.filter == PassListFilter.FILTERED
        _uiState.value = current.copy(
            timeWindow = DEFAULT_TIME_WINDOW,
            minMaxElevation = DEFAULT_MIN_MAX_ELEVATION,
            filter = if (wasFiltered) PassListFilter.ALL else current.filter
        )
        if (wasFiltered) reload()
    }

    // Triggered by the UI nearing the end of the currently loaded list. Only meaningful for
    // HISTORY, ALL, and FILTERED — UPCOMING has no pagination, and appending to the tail of
    // `passes` never disturbs an ALL/FILTERED view's already-shown upcoming portion, since that
    // portion always sits at the front of the list.
    fun loadMore() {
        val current = _uiState.value
        if (current.filter == PassListFilter.UPCOMING) return
        if (!current.hasMoreHistory || current.isLoadingMore) return

        viewModelScope.launch {
            _uiState.value = _uiState.value.copy(isLoadingMore = true)
            val nextPage = historyPage + 1
            when (val result = passRepository.getPassHistory(satelliteId, nextPage, queryFor(current.filter))) {
                is ApiResult.Success -> {
                    historyPage = nextPage
                    _uiState.value = _uiState.value.copy(
                        passes = _uiState.value.passes + result.data.items,
                        isLoadingMore = false,
                        hasMoreHistory = result.data.hasMore,
                        error = null
                    )
                }
                else -> _uiState.value = _uiState.value.copy(isLoadingMore = false, error = errorMessageFor(result))
            }
        }
    }

    private fun reload() {
        historyPage = 1
        _uiState.value = _uiState.value.copy(
            isLoadingMore = true,
            passes = emptyList(),
            nearestPassId = null,
            hasMoreHistory = false,
            error = null
        )
        viewModelScope.launch {
            when (_uiState.value.filter) {
                PassListFilter.UPCOMING -> loadUpcomingOnly()
                PassListFilter.HISTORY -> loadHistoryOnly()
                PassListFilter.ALL -> loadAll()
                PassListFilter.FILTERED -> loadFilteredOnly()
            }
        }
    }

    // Reuses PassRepository.getPasses as-is (its own TTL/Room-first caching, unchanged) — no new
    // caching logic here. getPasses returns ascending-by-AOS (what Dashboard depends on); this
    // screen re-sorts that same result descending in-memory so it's consistent with the merged
    // list's overall ordering, without touching getPasses' own sort order.
    //
    // excludePastAos(now) is applied here too (same shared utility DashboardViewModel uses, see
    // android/CLAUDE.md) -- a pass whose AOS has slipped into the past while getPasses' own 1h TTL
    // cache was still "fresh" must not keep showing in the Upcoming portion.
    private suspend fun loadUpcomingOnly() {
        when (val result = passRepository.getPasses(satelliteId)) {
            is ApiResult.Success -> _uiState.value = _uiState.value.copy(
                passes = result.data.excludePastAos(OffsetDateTime.now(clock)).sortedByDescending { it.aos },
                nearestPassId = null,
                isLoadingMore = false,
                hasMoreHistory = false,
                error = null
            )
            else -> _uiState.value = _uiState.value.copy(isLoadingMore = false, error = errorMessageFor(result))
        }
    }

    // Pure, always-unfiltered time-based view — see PassListFilter's doc comment. Queries with
    // UNFILTERED_QUERY regardless of the Filter Modal's current timeWindow/minMaxElevation values,
    // which only ever apply to FILTERED. This also means HISTORY naturally paginates through the
    // satellite's ENTIRE retained history, not just whatever the Filter Modal's own default window
    // happens to be -- and reaching its true end is exactly the condition that lets
    // PassRepository.getPassHistory mark HistoryLoadState.isFullyLoaded (see that method's own
    // doc comment and android/CLAUDE.md).
    private suspend fun loadHistoryOnly() {
        when (val result = passRepository.getPassHistory(satelliteId, 1, UNFILTERED_QUERY)) {
            is ApiResult.Success -> _uiState.value = _uiState.value.copy(
                passes = result.data.items,
                nearestPassId = null,
                isLoadingMore = false,
                hasMoreHistory = result.data.hasMore,
                error = null
            )
            else -> _uiState.value = _uiState.value.copy(isLoadingMore = false, error = errorMessageFor(result))
        }
    }

    // FILTERED -- the only segment where the Filter Modal's timeWindow/minMaxElevation actually
    // apply. This is deliberately the SAME call shape loadHistoryOnly() uses (getPassHistory with
    // page 1), just with the user's actual filter instead of UNFILTERED_QUERY: since the backend's
    // paginated history endpoint (and PassRepository's local Room-fresh-and-fully-loaded path) has
    // no notion of "only past" -- it returns every stored pass matching the aos/elevation bounds,
    // upcoming or historical alike -- reusing it here is exactly what performs the "actual mixed-
    // chronology filtered query that ALL was incorrectly implying it did" (this task's own
    // framing). No separate nearestPassId boundary concept applies: unlike ALL (which pastes
    // together two independently-fetched portions), FILTERED gets one already-sorted, already-
    // homogeneous list from a single query. Room-first-then-network is inherited for free from
    // PassRepository.getPassHistory's existing decision tree -- no new caching strategy was
    // invented for this (see android/CLAUDE.md).
    private suspend fun loadFilteredOnly() {
        when (val result = passRepository.getPassHistory(satelliteId, 1, currentFilter())) {
            is ApiResult.Success -> _uiState.value = _uiState.value.copy(
                passes = result.data.items,
                nearestPassId = null,
                isLoadingMore = false,
                hasMoreHistory = result.data.hasMore,
                error = null
            )
            else -> _uiState.value = _uiState.value.copy(isLoadingMore = false, error = errorMessageFor(result))
        }
    }

    // Fetches upcoming (small, whole list) and the first history page in parallel — independent of
    // each other, same async/awaitAll shape as DashboardViewModel's per-tab loading. A failure on
    // one side doesn't blank the other: whichever side succeeded is still shown, with `error` set
    // to describe which side failed (mirrors DashboardViewModel's per-tab-error philosophy, applied
    // here to the two halves of one merged list instead of separate tabs). Also a pure, always-
    // unfiltered view (UNFILTERED_QUERY) -- see PassListFilter's doc comment.
    private suspend fun loadAll() = coroutineScope {
        val now = OffsetDateTime.now(clock)
        val upcomingDeferred = async { passRepository.getPasses(satelliteId) }
        val historyDeferred = async { passRepository.getPassHistory(satelliteId, 1, UNFILTERED_QUERY) }
        val upcomingResult = upcomingDeferred.await()
        val historyResult = historyDeferred.await()

        val upcoming = (upcomingResult as? ApiResult.Success)?.data?.excludePastAos(now)?.sortedByDescending { it.aos } ?: emptyList()
        val history = (historyResult as? ApiResult.Success)?.data?.items ?: emptyList()

        val error = when {
            upcomingResult is ApiResult.Success && historyResult is ApiResult.Success -> null
            upcomingResult !is ApiResult.Success && historyResult !is ApiResult.Success ->
                errorMessageFor(historyResult)
            upcomingResult !is ApiResult.Success -> "Upcoming passes: ${errorMessageFor(upcomingResult)}"
            else -> "History: ${errorMessageFor(historyResult)}"
        }

        _uiState.value = _uiState.value.copy(
            passes = upcoming + history,
            nearestPassId = computeNearestPassId(upcoming, history, now),
            isLoadingMore = false,
            hasMoreHistory = (historyResult as? ApiResult.Success)?.data?.hasMore ?: false,
            error = error
        )
    }

    // Boundary marker between the upcoming and history portions of the ALL list — NOT a generic
    // "closest pass to now" pick. Chosen resolution (flagged per the task's own instructions as a
    // deliberate edge-case call, not the only reasonable one): the last upcoming pass in display
    // order, i.e. the one with the smallest AOS that's still >= now, since upcoming is shown
    // newest-AOS-first descending down to soonest-AOS just above the boundary; if there are no
    // upcoming passes at all, the first (most recent) history pass instead.
    private fun computeNearestPassId(upcoming: List<Pass>, history: List<Pass>, now: OffsetDateTime): String? =
        upcoming.filter { it.aos >= now }.minByOrNull { it.aos }?.id
            ?: history.firstOrNull()?.id

    private fun currentFilter(): PassHistoryFilter =
        PassHistoryFilter(_uiState.value.timeWindow, _uiState.value.minMaxElevation)

    // Which query loadMore() (History/All/Filtered pagination) should use for a given segment --
    // UNFILTERED_QUERY for the two pure time-based views, the user's actual filter for FILTERED.
    private fun queryFor(filter: PassListFilter): PassHistoryFilter = when (filter) {
        PassListFilter.FILTERED -> currentFilter()
        else -> UNFILTERED_QUERY
    }

    private fun errorMessageFor(result: ApiResult<*>): String = when (result) {
        is ApiResult.Error -> result.message
        ApiResult.AuthRequired -> "Authentication required."
        ApiResult.NetworkError -> "No network connection."
        is ApiResult.Success -> error("errorMessageFor called with a Success result")
    }

    companion object {
        // Single source of truth for "default" filter values — used both for the initial state
        // above and resetFilters()/applyFilterActivation()'s "is a filter active at all" check.
        // Also public so the Composable layer's filter-badge-count, active-filter-chip logic, and
        // segmented-control visibility (both derived at render time, not stored in UiState)
        // compare against these exact values instead of a second hardcoded copy of "default".
        val DEFAULT_TIME_WINDOW: TimeWindow = TimeWindow.Last7Days
        val DEFAULT_MIN_MAX_ELEVATION: Double? = null

        // What UPCOMING/HISTORY/ALL always query with, regardless of the Filter Modal's current
        // field values -- see PassListFilter's doc comment. Deliberately NOT the same as
        // DEFAULT_TIME_WINDOW/DEFAULT_MIN_MAX_ELEVATION (which describe the Filter Modal's own
        // "nothing chosen yet" state and happens to resolve to a real 7-day bound, see TimeWindow's
        // doc comment) -- this is a fully open Custom(null, null), the one query shape
        // PassHistoryFilterMappers.resolve() treats as truly unfiltered.
        private val UNFILTERED_QUERY = PassHistoryFilter(TimeWindow.Custom(null, null), null)
    }
}
