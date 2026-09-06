package com.sattrakk.app.ui.fullpasslist

import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.sattrakk.app.data.repository.PassRepository
import com.sattrakk.app.domain.model.ApiResult
import com.sattrakk.app.domain.model.Pass
import com.sattrakk.app.domain.model.PassHistoryFilter
import com.sattrakk.app.domain.model.TimeWindow
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
// passes + paginated historical passes) for a single satellite, with an Upcoming/History/All
// filter plus a time-window and minimum-elevation filter. See android/CLAUDE.md's "Full Pass List"
// section. No Composable is wired to this yet — covered entirely by FullPassListViewModelTest.
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

    // 1-based backend/Room page cursor for the history portion only — UPCOMING has no pagination.
    // Reset to 1 by reload(); advanced by loadMore().
    private var historyPage = 1

    init {
        reload()
    }

    // Any of the three filter setters reset pagination and rebuild the list from scratch — per
    // android/CLAUDE.md, a filter change is effectively a new query, not an incremental update.
    // There's no free-text search on this screen, so there's deliberately no "restore scroll
    // position" behavior to preserve here, unlike a search-clearing flow.
    fun setFilter(filter: PassListFilter) {
        if (_uiState.value.filter == filter) return
        _uiState.value = _uiState.value.copy(filter = filter)
        reload()
    }

    fun setTimeWindow(timeWindow: TimeWindow) {
        if (_uiState.value.timeWindow == timeWindow) return
        _uiState.value = _uiState.value.copy(timeWindow = timeWindow)
        reload()
    }

    fun setMinMaxElevation(minMaxElevation: Double?) {
        if (_uiState.value.minMaxElevation == minMaxElevation) return
        _uiState.value = _uiState.value.copy(minMaxElevation = minMaxElevation)
        reload()
    }

    // Resets only timeWindow/minMaxElevation, not `filter` — the UPCOMING/HISTORY/ALL choice isn't
    // one of the Filter Modal's controls (it's the screen's own segmented control) and "reset
    // filters" shouldn't silently switch the user away from whichever of the three they're
    // looking at. Wired to the Filter Modal's "Reset" button rather than composing two setter
    // calls from the Composable layer, so the definition of "default" lives in exactly one place.
    fun resetFilters() {
        val current = _uiState.value
        if (current.timeWindow == DEFAULT_TIME_WINDOW && current.minMaxElevation == DEFAULT_MIN_MAX_ELEVATION) return
        _uiState.value = current.copy(timeWindow = DEFAULT_TIME_WINDOW, minMaxElevation = DEFAULT_MIN_MAX_ELEVATION)
        reload()
    }

    // Triggered by the UI nearing the end of the currently loaded list. Only meaningful for
    // HISTORY and ALL — UPCOMING has no pagination, and appending to the tail of `passes` never
    // disturbs an ALL view's already-shown upcoming portion, since that portion always sits at the
    // front of the list.
    fun loadMore() {
        val current = _uiState.value
        if (current.filter == PassListFilter.UPCOMING) return
        if (!current.hasMoreHistory || current.isLoadingMore) return

        viewModelScope.launch {
            _uiState.value = _uiState.value.copy(isLoadingMore = true)
            val nextPage = historyPage + 1
            when (val result = passRepository.getPassHistory(satelliteId, nextPage, currentFilter())) {
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
            }
        }
    }

    // Reuses PassRepository.getPasses as-is (its own TTL/Room-first caching, unchanged) — no new
    // caching logic here. getPasses returns ascending-by-AOS (what Dashboard depends on); this
    // screen re-sorts that same result descending in-memory so it's consistent with the merged
    // list's overall ordering, without touching getPasses' own sort order.
    private suspend fun loadUpcomingOnly() {
        when (val result = passRepository.getPasses(satelliteId)) {
            is ApiResult.Success -> _uiState.value = _uiState.value.copy(
                passes = result.data.sortedByDescending { it.aos },
                nearestPassId = null,
                isLoadingMore = false,
                hasMoreHistory = false,
                error = null
            )
            else -> _uiState.value = _uiState.value.copy(isLoadingMore = false, error = errorMessageFor(result))
        }
    }

    private suspend fun loadHistoryOnly() {
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
    // here to the two halves of one merged list instead of separate tabs).
    private suspend fun loadAll() = coroutineScope {
        val upcomingDeferred = async { passRepository.getPasses(satelliteId) }
        val historyDeferred = async { passRepository.getPassHistory(satelliteId, 1, currentFilter()) }
        val upcomingResult = upcomingDeferred.await()
        val historyResult = historyDeferred.await()

        val upcoming = (upcomingResult as? ApiResult.Success)?.data?.sortedByDescending { it.aos } ?: emptyList()
        val history = (historyResult as? ApiResult.Success)?.data?.items ?: emptyList()
        val now = OffsetDateTime.now(clock)

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

    private fun errorMessageFor(result: ApiResult<*>): String = when (result) {
        is ApiResult.Error -> result.message
        ApiResult.AuthRequired -> "Authentication required."
        ApiResult.NetworkError -> "No network connection."
        is ApiResult.Success -> error("errorMessageFor called with a Success result")
    }

    companion object {
        // Single source of truth for "default" filter values — used both for the initial state
        // above and resetFilters(). Also public so the Composable layer's filter-badge-count and
        // active-filter-chip logic (both derived at render time, not stored in UiState) compares
        // against these exact values instead of a second hardcoded copy of "default".
        val DEFAULT_TIME_WINDOW: TimeWindow = TimeWindow.Last7Days
        val DEFAULT_MIN_MAX_ELEVATION: Double? = null
    }
}
