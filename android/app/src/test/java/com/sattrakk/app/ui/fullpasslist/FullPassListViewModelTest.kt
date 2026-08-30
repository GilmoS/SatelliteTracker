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
    fun `HISTORY filter paginates independently via getPassHistory`() {
        val h1 = pass("h1", aosOffsetMinutes = -10)
        coEvery { passRepository.getPasses(satelliteId, any()) } returns ApiResult.Success(emptyList())
        coEvery { passRepository.getPassHistory(satelliteId, any(), any()) } returns ApiResult.Success(paged(emptyList()))
        val viewModel = createViewModel() // default filter ALL -> calls both getPasses and getPassHistory once
        clearMocks(passRepository, answers = false)
        coEvery { passRepository.getPassHistory(satelliteId, 1, any()) } returns
            ApiResult.Success(paged(listOf(h1), page = 1, hasMore = true))

        viewModel.setFilter(PassListFilter.HISTORY)
        runCurrent()

        val state = viewModel.uiState.value
        assertEquals(listOf(h1), state.passes)
        assertTrue(state.hasMoreHistory)
        assertNull(state.nearestPassId) // no boundary concept for a single-portion view
        coVerify(exactly = 0) { passRepository.getPasses(any(), any()) }
    }

    @Test
    fun `ALL filter merges upcoming and history with nearestPassId at the last upcoming pass`() {
        val upcomingNear = pass("u-near", aosOffsetMinutes = 5) // smallest AOS still >= now
        val upcomingFar = pass("u-far", aosOffsetMinutes = 50)
        val historyPass = pass("h1", aosOffsetMinutes = -10)
        coEvery { passRepository.getPasses(satelliteId, any()) } returns ApiResult.Success(listOf(upcomingNear, upcomingFar))
        coEvery { passRepository.getPassHistory(satelliteId, 1, any()) } returns ApiResult.Success(paged(listOf(historyPass)))

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
        coEvery { passRepository.getPassHistory(satelliteId, 1, any()) } returns
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
        coEvery { passRepository.getPassHistory(satelliteId, 1, any()) } returns ApiResult.Success(paged(listOf(historyPass)))

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
        coEvery { passRepository.getPassHistory(satelliteId, 1, any()) } returns ApiResult.Success(paged(listOf(historyPass)))
        val viewModel = createViewModel()
        assertEquals(listOf(upcomingPass, historyPass), viewModel.uiState.value.passes)

        viewModel.setFilter(PassListFilter.HISTORY)
        runCurrent()

        // Only the history pass remains -- old merged data was replaced, not appended to.
        assertEquals(listOf(historyPass), viewModel.uiState.value.passes)
    }

    @Test
    fun `changing timeWindow resets pagination and refetches page 1`() {
        val h1 = pass("h1", aosOffsetMinutes = -10)
        val h2 = pass("h2", aosOffsetMinutes = -20)
        coEvery { passRepository.getPasses(satelliteId, any()) } returns ApiResult.Success(emptyList())
        coEvery { passRepository.getPassHistory(satelliteId, 1, PassHistoryFilter(TimeWindow.Last7Days, null)) } returns
            ApiResult.Success(paged(listOf(h1), page = 1, hasMore = true))
        coEvery { passRepository.getPassHistory(satelliteId, 2, PassHistoryFilter(TimeWindow.Last7Days, null)) } returns
            ApiResult.Success(paged(listOf(h2), page = 2, hasMore = false))
        val viewModel = createViewModel()
        viewModel.setFilter(PassListFilter.HISTORY)
        runCurrent()
        viewModel.loadMore()
        runCurrent()
        assertEquals(listOf(h1, h2), viewModel.uiState.value.passes)

        // Switch time window: must reset back to page 1, not continue from page 3.
        coEvery { passRepository.getPassHistory(satelliteId, 1, PassHistoryFilter(TimeWindow.Last24h, null)) } returns
            ApiResult.Success(paged(listOf(h1), page = 1, hasMore = false))
        viewModel.setTimeWindow(TimeWindow.Last24h)
        runCurrent()

        assertEquals(listOf(h1), viewModel.uiState.value.passes)
        coVerify(exactly = 1) { passRepository.getPassHistory(satelliteId, 1, PassHistoryFilter(TimeWindow.Last24h, null)) }
    }

    @Test
    fun `loadMore only paginates the history portion without disturbing the upcoming portion in ALL mode`() {
        val upcomingPass = pass("u1", aosOffsetMinutes = 10)
        val h1 = pass("h1", aosOffsetMinutes = -10)
        val h2 = pass("h2", aosOffsetMinutes = -20)
        coEvery { passRepository.getPasses(satelliteId, any()) } returns ApiResult.Success(listOf(upcomingPass))
        coEvery { passRepository.getPassHistory(satelliteId, 1, any()) } returns
            ApiResult.Success(paged(listOf(h1), page = 1, hasMore = true))
        coEvery { passRepository.getPassHistory(satelliteId, 2, any()) } returns
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
}
