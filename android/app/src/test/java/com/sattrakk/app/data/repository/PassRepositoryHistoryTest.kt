package com.sattrakk.app.data.repository

import com.sattrakk.app.data.local.CacheMetadataDao
import com.sattrakk.app.data.local.HistoryLoadStateDao
import com.sattrakk.app.data.local.PassDao
import com.sattrakk.app.data.local.entity.HistoryLoadStateEntity
import com.sattrakk.app.data.local.entity.PassEntity
import com.sattrakk.app.data.remote.SatTrakkApi
import com.sattrakk.app.data.remote.dto.PassDto
import com.sattrakk.app.data.remote.dto.PassDtoPagedResultDto
import com.sattrakk.app.data.session.SessionManager
import com.sattrakk.app.data.util.SafeApiCaller
import com.sattrakk.app.domain.model.ApiResult
import com.sattrakk.app.domain.model.PassHistoryFilter
import com.sattrakk.app.domain.model.TimeWindow
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.mockk
import io.mockk.slot
import java.io.IOException
import java.time.Clock
import java.time.Instant
import java.time.OffsetDateTime
import java.time.ZoneOffset
import java.util.UUID
import java.util.concurrent.TimeUnit
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import retrofit2.Response

class PassRepositoryHistoryTest {

    private val api = mockk<SatTrakkApi>()
    private val passDao = mockk<PassDao>(relaxUnitFun = true)
    private val cacheMetadataDao = mockk<CacheMetadataDao>(relaxUnitFun = true)
    private val safeApiCall = SafeApiCaller(mockk<SessionManager>(relaxUnitFun = true))
    private val applicationScope = CoroutineScope(Dispatchers.Unconfined)
    private val historyLoadStateDao = mockk<HistoryLoadStateDao>(relaxUnitFun = true)

    private val fixedInstant: Instant = Instant.parse("2026-08-30T00:00:00Z")
    private val clock: Clock = Clock.fixed(fixedInstant, ZoneOffset.UTC)
    private val now: OffsetDateTime = OffsetDateTime.ofInstant(fixedInstant, ZoneOffset.UTC)

    private lateinit var repository: PassRepository

    private val satelliteId = UUID.randomUUID()

    // Last7Days always resolves a non-null aosFrom (see TimeWindow's doc comment) -> never
    // "unfiltered". Custom(null, null) with no elevation floor is the one filter that resolves to
    // no bounds at all.
    private val filteredFilter = PassHistoryFilter(TimeWindow.Last7Days, minMaxElevation = null)
    private val unfilteredFilter = PassHistoryFilter(TimeWindow.Custom(null, null), minMaxElevation = null)

    private fun entity(id: UUID, aosEpochMillis: Long, maxElevation: Double = 45.0, notify: Boolean = true) =
        PassEntity(
            id = id.toString(),
            satelliteId = satelliteId.toString(),
            tleId = UUID.randomUUID().toString(),
            orbitNumber = 1,
            aosEpochMillis = aosEpochMillis,
            losEpochMillis = aosEpochMillis + 300_000,
            maxElevation = maxElevation,
            aosAzimuth = 10.0,
            losAzimuth = 20.0,
            durationSec = 300,
            notify = notify,
            outlookSynced = false,
            calculatedAtEpochMillis = aosEpochMillis
        )

    private fun dto(id: UUID = UUID.randomUUID(), aos: OffsetDateTime = now.minusDays(1)) = PassDto(
        id = id,
        satelliteId = satelliteId,
        tleId = UUID.randomUUID(),
        orbitNumber = 1,
        aos = aos,
        los = aos.plusMinutes(5),
        maxElevation = 45.0,
        aosAzimuth = 10.0,
        losAzimuth = 20.0,
        durationSec = 300,
        outlookSynced = false,
        calculatedAt = aos
    )

    @Before
    fun setUp() {
        repository = PassRepository(
            api, passDao, cacheMetadataDao, safeApiCall, applicationScope, historyLoadStateDao, clock
        )
    }

    @Test
    fun `fresh and fully loaded serves entirely from Room with zero network calls`() = runTest {
        coEvery { historyLoadStateDao.get(satelliteId.toString()) } returns
            HistoryLoadStateEntity(satelliteId.toString(), isFullyLoaded = true, lastVerifiedAtEpochMillis = fixedInstant.toEpochMilli())
        coEvery {
            passDao.getFilteredForSatellite(satelliteId.toString(), any(), any(), any(), any(), any())
        } returns listOf(entity(UUID.randomUUID(), fixedInstant.toEpochMilli()))

        val result = repository.getPassHistory(satelliteId.toString(), page = 1, filter = filteredFilter)

        assertTrue(result is ApiResult.Success)
        assertEquals(1, (result as ApiResult.Success).data.items.size)
        coVerify(exactly = 0) { api.getPassHistory(any(), any(), any(), any(), any(), any()) }
    }

    @Test
    fun `fresh and fully loaded applies local pagination correctly via limit and offset`() = runTest {
        coEvery { historyLoadStateDao.get(satelliteId.toString()) } returns
            HistoryLoadStateEntity(satelliteId.toString(), isFullyLoaded = true, lastVerifiedAtEpochMillis = fixedInstant.toEpochMilli())
        val slot = slot<Int>()
        coEvery {
            passDao.getFilteredForSatellite(
                satelliteId.toString(), any(), any(), any(), any(), capture(slot)
            )
        } returns emptyList()

        repository.getPassHistory(satelliteId.toString(), page = 3, filter = filteredFilter)

        // page 3, pageSize 50 -> offset 100
        assertEquals(100, slot.captured)
    }

    @Test
    fun `fresh and fully loaded computes hasMore from the extra row, mirroring the backend trick`() = runTest {
        coEvery { historyLoadStateDao.get(satelliteId.toString()) } returns
            HistoryLoadStateEntity(satelliteId.toString(), isFullyLoaded = true, lastVerifiedAtEpochMillis = fixedInstant.toEpochMilli())
        // 51 rows returned for a pageSize-50 request -> hasMore should be true, and only 50 items returned.
        val rows = (1..51).map { entity(UUID.randomUUID(), fixedInstant.toEpochMilli() - it * 1000L) }
        coEvery {
            passDao.getFilteredForSatellite(satelliteId.toString(), any(), any(), any(), any(), any())
        } returns rows

        val result = repository.getPassHistory(satelliteId.toString(), page = 1, filter = filteredFilter)

        assertTrue(result is ApiResult.Success)
        val data = (result as ApiResult.Success).data
        assertEquals(50, data.items.size)
        assertTrue(data.hasMore)
    }

    @Test
    fun `stale load state calls network, upserts rows, and does not set isFullyLoaded for a filtered fetch even when hasMore is false`() = runTest {
        coEvery { historyLoadStateDao.get(satelliteId.toString()) } returns
            HistoryLoadStateEntity(satelliteId.toString(), isFullyLoaded = true, lastVerifiedAtEpochMillis = fixedInstant.toEpochMilli() - TimeUnit.HOURS.toMillis(2))
        val newId = UUID.randomUUID()
        coEvery { passDao.getById(newId.toString()) } returns null
        coEvery { api.getPassHistory(satelliteId, 1, 50, any(), any(), any()) } returns
            Response.success(PassDtoPagedResultDto(items = listOf(dto(id = newId)), page = 1, pageSize = 50, hasMore = false))

        val result = repository.getPassHistory(satelliteId.toString(), page = 1, filter = filteredFilter)

        assertTrue(result is ApiResult.Success)
        assertFalse((result as ApiResult.Success).data.hasMore)
        coVerify(exactly = 1) { passDao.upsert(any()) }
        // The critical distinction: hasMore == false on a FILTERED fetch must never set isFullyLoaded.
        coVerify(exactly = 0) { historyLoadStateDao.upsert(any()) }
    }

    @Test
    fun `no load state calls network, and an unfiltered fetch reaching hasMore false sets isFullyLoaded true`() = runTest {
        coEvery { historyLoadStateDao.get(satelliteId.toString()) } returns null
        val newId = UUID.randomUUID()
        coEvery { passDao.getById(newId.toString()) } returns null
        coEvery { api.getPassHistory(satelliteId, 1, 50, null, null, null) } returns
            Response.success(PassDtoPagedResultDto(items = listOf(dto(id = newId)), page = 1, pageSize = 50, hasMore = false))

        val result = repository.getPassHistory(satelliteId.toString(), page = 1, filter = unfilteredFilter)

        assertTrue(result is ApiResult.Success)
        val slot = slot<HistoryLoadStateEntity>()
        coVerify(exactly = 1) { historyLoadStateDao.upsert(capture(slot)) }
        assertTrue(slot.captured.isFullyLoaded)
        assertEquals(satelliteId.toString(), slot.captured.satelliteId)
    }

    @Test
    fun `unfiltered fetch with hasMore true does not set isFullyLoaded`() = runTest {
        coEvery { historyLoadStateDao.get(satelliteId.toString()) } returns null
        val newId = UUID.randomUUID()
        coEvery { passDao.getById(newId.toString()) } returns null
        coEvery { api.getPassHistory(satelliteId, 1, 50, null, null, null) } returns
            Response.success(PassDtoPagedResultDto(items = listOf(dto(id = newId)), page = 1, pageSize = 50, hasMore = true))

        repository.getPassHistory(satelliteId.toString(), page = 1, filter = unfilteredFilter)

        coVerify(exactly = 0) { historyLoadStateDao.upsert(any()) }
    }

    @Test
    fun `network fetch preserves cached notify value and defaults to true for a newly seen pass`() = runTest {
        coEvery { historyLoadStateDao.get(satelliteId.toString()) } returns null
        val existingId = UUID.randomUUID()
        val newId = UUID.randomUUID()
        coEvery { passDao.getById(existingId.toString()) } returns entity(existingId, fixedInstant.toEpochMilli(), notify = false)
        coEvery { passDao.getById(newId.toString()) } returns null
        coEvery { api.getPassHistory(satelliteId, 1, 50, any(), any(), any()) } returns
            Response.success(
                PassDtoPagedResultDto(items = listOf(dto(id = existingId), dto(id = newId)), page = 1, pageSize = 50, hasMore = false)
            )

        val result = repository.getPassHistory(satelliteId.toString(), page = 1, filter = filteredFilter)

        assertTrue(result is ApiResult.Success)
        val items = (result as ApiResult.Success).data.items.associateBy { it.id }
        assertFalse(items.getValue(existingId.toString()).notify)
        assertTrue(items.getValue(newId.toString()).notify)
        coVerify(exactly = 2) { passDao.upsert(any()) }
    }

    @Test
    fun `network failure propagates as-is with no Room fallback`() = runTest {
        coEvery { historyLoadStateDao.get(satelliteId.toString()) } returns null
        coEvery { api.getPassHistory(satelliteId, 1, 50, any(), any(), any()) } throws IOException("offline")

        val result = repository.getPassHistory(satelliteId.toString(), page = 1, filter = filteredFilter)

        assertTrue(result is ApiResult.NetworkError)
        coVerify(exactly = 0) { passDao.upsert(any()) }
        coVerify(exactly = 0) { historyLoadStateDao.upsert(any()) }
    }
}
