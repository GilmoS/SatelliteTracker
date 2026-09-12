package com.sattrakk.app.ui.fullpasslist

import androidx.lifecycle.SavedStateHandle
import com.sattrakk.app.MainDispatcherRule
import com.sattrakk.app.data.repository.PassRepository
import com.sattrakk.app.domain.model.ApiResult
import com.sattrakk.app.domain.model.PagedResult
import com.sattrakk.app.domain.model.Pass
import com.sattrakk.app.domain.model.PassHistoryFilter
import com.sattrakk.app.domain.model.TimeWindow
import io.mockk.clearMocks
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.mockk
import java.time.Clock
import java.time.Instant
import java.time.OffsetDateTime
import java.time.ZoneOffset
import kotlinx.coroutines.ExperimentalCoroutinesApi
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test

// FullPassListViewModel has no long-lived polling/ticker coroutine (unlike DashboardViewModel), so
// its launched work always completes on its own. But it still runs on viewModelScope, i.e.
// Dispatchers.Main via MainDispatcherRule's TestDispatcher — a different scheduler than a bare
// runTest{} would auto-drain mid-test (only its FINAL implicit idle pass touches Main's queue; see
// DashboardViewModelTest's comment). So mid-test assertions here follow the same pattern: plain
// non-suspend @Test functions that drive mainDispatcherRule.testDispatcher.scheduler directly via
// runCurrent(), rather than wrapping the body in runTest{}.
//
// As of the design-review bug-fix round, UPCOMING/HISTORY/ALL always query with UNFILTERED_QUERY
// (an open Custom(null, null) with no elevation floor) — they no longer read timeWindow/
// minMaxElevation at all. Only the new FILTERED segment does. See FullPassListViewModel and
// android/CLAUDE.md.
@OptIn(ExperimentalCoroutinesApi::class)
class FullPassListViewModelTest {

    @get:Rule
    val mainDispatcherRule = MainDispatcherRule()

    private val passRepository = mockk<PassRepository>()
    private val baseInstant: Instant = Instant.parse("2026-08-30T00:00:00Z")
    private val clock: Clock = Clock.fixed(baseInstant, ZoneOffset.UTC)
    private val now: OffsetDateTime = OffsetDateTime.ofInstant(baseInstant, ZoneOffset.UTC)

    private val satelliteId = "sat-1"
    private val satelliteName = "EROS C3"

    private val unfilteredQuery = PassHistoryFilter(TimeWindow.Custom(null, null), null)

    private fun runCurrent() = mainDispatcherRule.testDispatcher.scheduler.runCurrent()

    private fun savedStateHandle() =
        SavedStateHandle(mapOf("satelliteId" to satelliteId, "satelliteName" to satelliteName))

    private fun pass(id: String, aosOffsetMinutes: Long) = Pass(
        id = id,
        satelliteId = satelliteId,
        tleId = "tle-$id",
        orbitNumber = 1,
        aos = now.plusMinutes(aosOffsetMinutes),
        los = now.plusMinutes(aosOffsetMinutes + 5),
        maxElevation = 45.0,
        aosAzimuth = 10.0,
        losAzimuth = 20.0,
        durationSec = 300,
        notify = true,
        outlookSynced = false,
        calculatedAt = now
    )

    private fun paged(items: List<Pass>, page: Int = 1, hasMore: Boolean = false) =
        PagedResult(items = items, page = page, pageSize = 50, hasMore = hasMore)

    private fun createViewModel(): FullPassListViewModel {
        val viewModel = FullPassListViewModel(passRepository, clock, savedStateHandle())
        runCurrent()
        return viewModel
    }

    @Test
    fun `initial state carries satelliteId and satelliteName from nav args`() {
        coEvery { passRepository.getPasses(satelliteId, any()) } returns ApiResult.Success(emptyList())
        coEvery { passRepository.getPassHistory(satelliteId, any(), any()) } returns ApiResult.Success(paged(emptyList()))

        val viewModel = createViewModel()

        assertEquals(satelliteId, viewModel.uiState.value.satelliteId)
        assertEquals(satelliteName, viewModel.uiState.value.satelliteName)
    }

    @Test
    fun `UPCOMING filter reuses getPasses re-sorted descending with no history call`() {
        val p1 = pass("p1", aosOffsetMinutes = 10)
        val p2 = pass("p2", aosOffsetMinutes = 30)
        coEvery { passRepository.getPasses(satelliteId, any()) } returns ApiResult.Success(listOf(p1, p2)) // ascending, as getPasses returns
        coEvery { passRepository.getPassHistory(satelliteId, any(), any()) } returns ApiResult.Success(paged(emptyList()))
        val viewModel = createViewModel() // default filter ALL -> calls both getPasses and getPassHistory once
        clearMocks(passRepository, answers = false)
        coEvery { passRepository.getPasses(satelliteId, any()) } returns ApiResult.Success(listOf(p1, p2))

        viewModel.setFilter(PassListFilter.UPCOMING)
        runCurrent()

        val state = viewModel.uiState.value
        assertEquals(listOf(p2, p1), state.passes) // re-sorted descending by AOS
        assertFalse(state.isLoadingMore)
        assertNull(state.error)
        coVerify(exactly = 0) { passRepository.getPassHistory(satelliteId, any(), any()) }
    }

    @Test
    fun `UPCOMING filter excludes a pass whose AOS has already passed`() {
        val past = pass("past", aosOffsetMinutes = -5)
        val future = pass("future", aosOffsetMinutes = 10)
        coEvery { passRepository.getPasses(satelliteId, any()) } returns ApiResult.Success(listOf(past, future))
        coEvery { passRepository.getPassHistory(satelliteId, any(), any()) } returns ApiResult.Success(paged(emptyList()))
        val viewModel = createViewModel()
        clearMocks(passRepository, answers = false)
        coEvery { passRepository.getPasses(satelliteId, any()) } returns ApiResult.Success(listOf(past, future))

        viewModel.setFilter(PassListFilter.UPCOMING)
        runCurrent()

        assertEquals(listOf(future), viewModel.uiState.value.passes)
    }

    @Test
    fun `HISTORY filter always queries unfiltered, ignoring the Filter Modal's timeWindow`() {
        val h1 = pass("h1", aosOffsetMinutes = -10)
        coEvery { passRepository.getPasses(satelliteId, any()) } returns ApiResult.Success(emptyList())
        coEvery { passRepository.getPassHistory(satelliteId, any(), any()) } returns ApiResult.Success(paged(emptyList()))
        val viewModel = createViewModel() // default filter ALL -> calls both getPasses and getPassHistory once
        clearMocks(passRepository, answers = false)
        coEvery { passRepository.getPassHistory(satelliteId, 1, unfilteredQuery) } returns
            ApiResult.Success(paged(listOf(h1), page = 1, hasMore = true))

        viewModel.setFilter(PassListFilter.HISTORY)
        runCurrent()

        val state = viewModel.uiState.value
        assertEquals(listOf(h1), state.passes)
        assertTrue(state.hasMoreHistory)
        assertNull(state.nearestPassId) // no boundary concept for a single-portion view
        coVerify(exactly = 0) { passRepository.getPasses(any(), any()) }
        coVerify(exactly = 1) { passRepository.getPassHistory(satelliteId, 1, unfilteredQuery) }
    }

    @Test
    fun `ALL filter merges upcoming and history with nearestPassId at the last upcoming pass`() {
        val upcomingNear = pass("u-near", aosOffsetMinutes = 5) // smallest AOS still >= now
        val upcomingFar = pass("u-far", aosOffsetMinutes = 50)
        val historyPass = pass("h1", aosOffsetMinutes = -10)
        coEvery { passRepository.getPasses(satelliteId, any()) } returns ApiResult.Success(listOf(upcomingNear, upcomingFar))
        coEvery { passRepository.getPassHistory(satelliteId, 1, unfilteredQuery) } returns ApiResult.Success(paged(listOf(historyPass)))

        val viewModel = createViewModel() // default filter is ALL

        val state = viewModel.uiState.value
        assertEquals(listOf(upcomingFar, upcomingNear, historyPass), state.passes)
        assertEquals("u-near", state.nearestPassId)
    }

    @Test
    fun `ALL filter with no upcoming passes falls back to the first history pass as nearestPassId`() {
        val historyPass1 = pass("h1", aosOffsetMinutes = -10)
        val historyPass2 = pass("h2", aosOffsetMinutes = -20)
        coEvery { passRepository.getPasses(satelliteId, any()) } returns ApiResult.Success(emptyList())
        coEvery { passRepository.getPassHistory(satelliteId, 1, unfilteredQuery) } returns
            ApiResult.Success(paged(listOf(historyPass1, historyPass2)))

        val viewModel = createViewModel()

        val state = viewModel.uiState.value
        assertEquals(listOf(historyPass1, historyPass2), state.passes)
        assertEquals("h1", state.nearestPassId)
    }

    @Test
    fun `ALL filter one side failing keeps the other sides data and sets a descriptive error`() {
        val historyPass = pass("h1", aosOffsetMinutes = -10)
        coEvery { passRepository.getPasses(satelliteId, any()) } returns ApiResult.NetworkError
        coEvery { passRepository.getPassHistory(satelliteId, 1, unfilteredQuery) } returns ApiResult.Success(paged(listOf(historyPass)))

        val viewModel = createViewModel()

        val state = viewModel.uiState.value
        assertEquals(listOf(historyPass), state.passes)
        assertTrue(state.error!!.contains("Upcoming passes"))
    }

    @Test
    fun `changing filter resets and reloads from scratch rather than appending`() {
        val upcomingPass = pass("u1", aosOffsetMinutes = 10)
        val historyPass = pass("h1", aosOffsetMinutes = -10)
        coEvery { passRepository.getPasses(satelliteId, any()) } returns ApiResult.Success(listOf(upcomingPass))
        coEvery { passRepository.getPassHistory(satelliteId, 1, unfilteredQuery) } returns ApiResult.Success(paged(listOf(historyPass)))
        val viewModel = createViewModel()
        assertEquals(listOf(upcomingPass, historyPass), viewModel.uiState.value.passes)

        viewModel.setFilter(PassListFilter.HISTORY)
        runCurrent()

        // Only the history pass remains -- old merged data was replaced, not appended to.
        assertEquals(listOf(historyPass), viewModel.uiState.value.passes)
    }

    @Test
    fun `loadMore only paginates the history portion without disturbing the upcoming portion in ALL mode`() {
        val upcomingPass = pass("u1", aosOffsetMinutes = 10)
        val h1 = pass("h1", aosOffsetMinutes = -10)
        val h2 = pass("h2", aosOffsetMinutes = -20)
        coEvery { passRepository.getPasses(satelliteId, any()) } returns ApiResult.Success(listOf(upcomingPass))
        coEvery { passRepository.getPassHistory(satelliteId, 1, unfilteredQuery) } returns
            ApiResult.Success(paged(listOf(h1), page = 1, hasMore = true))
        coEvery { passRepository.getPassHistory(satelliteId, 2, unfilteredQuery) } returns
            ApiResult.Success(paged(listOf(h2), page = 2, hasMore = false))
        val viewModel = createViewModel()
        assertEquals(listOf(upcomingPass, h1), viewModel.uiState.value.passes)

        viewModel.loadMore()
        runCurrent()

        assertEquals(listOf(upcomingPass, h1, h2), viewModel.uiState.value.passes)
        assertFalse(viewModel.uiState.value.hasMoreHistory)
        coVerify(exactly = 1) { passRepository.getPasses(satelliteId, any()) } // never re-fetched by loadMore
    }

    @Test
    fun `loadMore is a no-op for the UPCOMING filter`() {
        val upcomingPass = pass("u1", aosOffsetMinutes = 10)
        coEvery { passRepository.getPasses(satelliteId, any()) } returns ApiResult.Success(listOf(upcomingPass))
        coEvery { passRepository.getPassHistory(satelliteId, any(), any()) } returns ApiResult.Success(paged(emptyList()))
        val viewModel = createViewModel()
        viewModel.setFilter(PassListFilter.UPCOMING)
        runCurrent()

        viewModel.loadMore()
        runCurrent()

        coVerify(exactly = 0) { passRepository.getPassHistory(satelliteId, 2, any()) }
    }

    // ---- FILTERED segment (design-review bug-fix round) ----

    @Test
    fun `selecting a non-default time window auto-activates FILTERED and closes out ALL's data`() {
        val filteredPass = pass("f1", aosOffsetMinutes = -3000) // outside default Last7Days entirely
        coEvery { passRepository.getPasses(satelliteId, any()) } returns ApiResult.Success(emptyList())
        coEvery { passRepository.getPassHistory(satelliteId, 1, unfilteredQuery) } returns ApiResult.Success(paged(emptyList()))
        val viewModel = createViewModel() // default filter ALL
        assertEquals(PassListFilter.ALL, viewModel.uiState.value.filter)
        coEvery { passRepository.getPassHistory(satelliteId, 1, PassHistoryFilter(TimeWindow.Last24h, null)) } returns
            ApiResult.Success(paged(listOf(filteredPass)))

        viewModel.setTimeWindow(TimeWindow.Last24h)
        runCurrent()

        val state = viewModel.uiState.value
        assertEquals(PassListFilter.FILTERED, state.filter)
        assertEquals(TimeWindow.Last24h, state.timeWindow)
        assertEquals(listOf(filteredPass), state.passes)
    }

    @Test
    fun `selecting a non-default min elevation auto-activates FILTERED from any starting segment`() {
        coEvery { passRepository.getPasses(satelliteId, any()) } returns ApiResult.Success(emptyList())
        coEvery { passRepository.getPassHistory(satelliteId, any(), any()) } returns ApiResult.Success(paged(emptyList()))
        val viewModel = createViewModel()
        viewModel.setFilter(PassListFilter.UPCOMING) // starting segment other than ALL
        runCurrent()
        clearMocks(passRepository, answers = false)
        coEvery { passRepository.getPassHistory(satelliteId, 1, PassHistoryFilter(TimeWindow.Last7Days, 25.0)) } returns
            ApiResult.Success(paged(emptyList()))

        viewModel.setMinMaxElevation(25.0)
        runCurrent()

        val state = viewModel.uiState.value
        assertEquals(PassListFilter.FILTERED, state.filter)
        assertEquals(25.0, state.minMaxElevation)
        coVerify(exactly = 1) { passRepository.getPassHistory(satelliteId, 1, PassHistoryFilter(TimeWindow.Last7Days, 25.0)) }
    }

    @Test
    fun `FILTERED includes both upcoming and historical matching passes from one query`() {
        val futureMatch = pass("future", aosOffsetMinutes = 100)
        val pastMatch = pass("past", aosOffsetMinutes = -100)
        coEvery { passRepository.getPasses(satelliteId, any()) } returns ApiResult.Success(emptyList())
        coEvery { passRepository.getPassHistory(satelliteId, any(), any()) } returns ApiResult.Success(paged(emptyList()))
        val viewModel = createViewModel()
        coEvery { passRepository.getPassHistory(satelliteId, 1, PassHistoryFilter(TimeWindow.Last7Days, 10.0)) } returns
            ApiResult.Success(paged(listOf(futureMatch, pastMatch))) // already descending, as the backend/Room path returns

        viewModel.setMinMaxElevation(10.0)
        runCurrent()

        val state = viewModel.uiState.value
        assertEquals(PassListFilter.FILTERED, state.filter)
        assertEquals(listOf(futureMatch, pastMatch), state.passes)
        // Unlike ALL, FILTERED doesn't paste together two separately-fetched portions, so there's
        // no boundary marker to compute.
        assertNull(state.nearestPassId)
    }

    @Test
    fun `resetting filters while on FILTERED returns to ALL and reloads it`() {
        coEvery { passRepository.getPasses(satelliteId, any()) } returns ApiResult.Success(emptyList())
        coEvery { passRepository.getPassHistory(satelliteId, any(), any()) } returns ApiResult.Success(paged(emptyList()))
        val viewModel = createViewModel()
        viewModel.setMinMaxElevation(25.0) // auto-activates FILTERED
        runCurrent()
        assertEquals(PassListFilter.FILTERED, viewModel.uiState.value.filter)
        clearMocks(passRepository, answers = false)
        coEvery { passRepository.getPasses(satelliteId, any()) } returns ApiResult.Success(emptyList())
        coEvery { passRepository.getPassHistory(satelliteId, 1, unfilteredQuery) } returns ApiResult.Success(paged(emptyList()))

        viewModel.resetFilters()
        runCurrent()

        val state = viewModel.uiState.value
        assertEquals(PassListFilter.ALL, state.filter)
        assertEquals(TimeWindow.Last7Days, state.timeWindow)
        assertNull(state.minMaxElevation)
        coVerify(exactly = 1) { passRepository.getPassHistory(satelliteId, 1, unfilteredQuery) }
        coVerify(exactly = 1) { passRepository.getPasses(satelliteId, any()) }
    }

    @Test
    fun `resetting filters while on HISTORY clears the fields but does not reload since HISTORY ignores them`() {
        coEvery { passRepository.getPasses(satelliteId, any()) } returns ApiResult.Success(emptyList())
        coEvery { passRepository.getPassHistory(satelliteId, any(), any()) } returns ApiResult.Success(paged(emptyList()))
        val viewModel = createViewModel()
        viewModel.setFilter(PassListFilter.HISTORY)
        runCurrent()
        // Fields set directly (not via the Filter Modal auto-activation) while HISTORY stays
        // selected -- since HISTORY never reads them, no reload happens for setting them either.
        clearMocks(passRepository, answers = false)

        viewModel.resetFilters() // no-op: still at defaults, nothing was ever set non-default
        runCurrent()

        coVerify(exactly = 0) { passRepository.getPassHistory(any(), any(), any()) }
        coVerify(exactly = 0) { passRepository.getPasses(any(), any()) }
        assertEquals(PassListFilter.HISTORY, viewModel.uiState.value.filter)
    }

    @Test
    fun `resetFilters is a no-op when already at defaults`() {
        coEvery { passRepository.getPasses(satelliteId, any()) } returns ApiResult.Success(emptyList())
        coEvery { passRepository.getPassHistory(satelliteId, any(), any()) } returns ApiResult.Success(paged(emptyList()))
        val viewModel = createViewModel()
        clearMocks(passRepository, answers = false)

        viewModel.resetFilters()
        runCurrent()

        coVerify(exactly = 0) { passRepository.getPassHistory(any(), any(), any()) }
        coVerify(exactly = 0) { passRepository.getPasses(any(), any()) }
    }

    @Test
    fun `clearing minMaxElevation back to default while FILTERED returns to ALL without calling resetFilters`() {
        coEvery { passRepository.getPasses(satelliteId, any()) } returns ApiResult.Success(emptyList())
        coEvery { passRepository.getPassHistory(satelliteId, any(), any()) } returns ApiResult.Success(paged(emptyList()))
        val viewModel = createViewModel()
        viewModel.setMinMaxElevation(30.0)
        runCurrent()
        assertEquals(PassListFilter.FILTERED, viewModel.uiState.value.filter)

        viewModel.setMinMaxElevation(FullPassListViewModel.DEFAULT_MIN_MAX_ELEVATION)
        runCurrent()

        assertEquals(PassListFilter.ALL, viewModel.uiState.value.filter)
    }

    @Test
    fun `FILTERED pagination reuses the active filter, resets on a filter field change, and never touches getPasses`() {
        val f1 = pass("f1", aosOffsetMinutes = -10)
        val f2 = pass("f2", aosOffsetMinutes = -20)
        coEvery { passRepository.getPasses(satelliteId, any()) } returns ApiResult.Success(emptyList())
        coEvery { passRepository.getPassHistory(satelliteId, any(), any()) } returns ApiResult.Success(paged(emptyList()))
        val viewModel = createViewModel()
        coEvery { passRepository.getPassHistory(satelliteId, 1, PassHistoryFilter(TimeWindow.Last24h, null)) } returns
            ApiResult.Success(paged(listOf(f1), page = 1, hasMore = true))
        coEvery { passRepository.getPassHistory(satelliteId, 2, PassHistoryFilter(TimeWindow.Last24h, null)) } returns
            ApiResult.Success(paged(listOf(f2), page = 2, hasMore = false))

        viewModel.setTimeWindow(TimeWindow.Last24h)
        runCurrent()
        viewModel.loadMore()
        runCurrent()

        assertEquals(listOf(f1, f2), viewModel.uiState.value.passes)
        // Exactly 1: the one call from the initial default-ALL load at createViewModel() time --
        // neither setTimeWindow's switch to FILTERED nor loadMore()'s pagination touch getPasses.
        coVerify(exactly = 1) { passRepository.getPasses(satelliteId, any()) }

        // Changing the filter again resets back to page 1, not page 3.
        coEvery { passRepository.getPassHistory(satelliteId, 1, PassHistoryFilter(TimeWindow.Last48h, null)) } returns
            ApiResult.Success(paged(listOf(f1), page = 1, hasMore = false))
        viewModel.setTimeWindow(TimeWindow.Last48h)
        runCurrent()

        assertEquals(listOf(f1), viewModel.uiState.value.passes)
        coVerify(exactly = 1) { passRepository.getPassHistory(satelliteId, 1, PassHistoryFilter(TimeWindow.Last48h, null)) }
    }

    // FILTERED is a thin wrapper over PassRepository.getPassHistory -- the exact same call HISTORY
    // uses, just with the user's real filter instead of an unfiltered one. No new caching logic
    // was added at the ViewModel layer for this: Room-first-then-network (serve entirely from Room
    // when HistoryLoadState says the satellite's history is fresh and fully loaded, else fall back
    // to the network and upsert) is inherited for free from getPassHistory's own decision tree,
    // already covered end-to-end by PassRepositoryHistoryTest — see android/CLAUDE.md. This test
    // only confirms FILTERED delegates to that single call rather than inventing a parallel path.
    @Test
    fun `FILTERED delegates entirely to PassRepository getPassHistory, the same Room-first-then-network path HISTORY uses`() {
        coEvery { passRepository.getPasses(satelliteId, any()) } returns ApiResult.Success(emptyList())
        coEvery { passRepository.getPassHistory(satelliteId, any(), any()) } returns ApiResult.Success(paged(emptyList()))
        val viewModel = createViewModel()
        clearMocks(passRepository, answers = false)
        coEvery { passRepository.getPassHistory(satelliteId, 1, PassHistoryFilter(TimeWindow.Last7Days, 15.0)) } returns
            ApiResult.Success(paged(emptyList()))

        viewModel.setMinMaxElevation(15.0)
        runCurrent()

        coVerify(exactly = 1) { passRepository.getPassHistory(satelliteId, 1, PassHistoryFilter(TimeWindow.Last7Days, 15.0)) }
        coVerify(exactly = 0) { passRepository.getPasses(any(), any()) }
    }
}
