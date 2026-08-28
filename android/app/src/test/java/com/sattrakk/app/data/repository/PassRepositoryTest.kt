package com.sattrakk.app.data.repository

import com.sattrakk.app.data.local.CacheMetadataDao
import com.sattrakk.app.data.local.PassDao
import com.sattrakk.app.data.local.entity.CacheMetadataEntity
import com.sattrakk.app.data.local.entity.PassEntity
import com.sattrakk.app.data.remote.SatTrakkApi
import com.sattrakk.app.data.remote.dto.NotifyStatusDto
import com.sattrakk.app.data.remote.dto.PassDto
import com.sattrakk.app.data.remote.dto.PassTrackDto
import com.sattrakk.app.data.session.SessionManager
import com.sattrakk.app.data.util.SafeApiCaller
import com.sattrakk.app.domain.model.ApiResult
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.mockk
import io.mockk.slot
import java.io.IOException
import java.time.OffsetDateTime
import java.util.UUID
import java.util.concurrent.TimeUnit
import kotlinx.coroutines.test.runTest
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.ResponseBody.Companion.toResponseBody
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import retrofit2.Response

class PassRepositoryTest {

    private val api = mockk<SatTrakkApi>()
    private val passDao = mockk<PassDao>(relaxUnitFun = true)
    private val cacheMetadataDao = mockk<CacheMetadataDao>(relaxUnitFun = true)
    private val safeApiCall = SafeApiCaller(mockk<SessionManager>(relaxUnitFun = true))
    private lateinit var repository: PassRepository

    private val satelliteId = UUID.randomUUID()
    private val passId = UUID.randomUUID()
    private val cacheKey = "passes:$satelliteId"

    private val cachedEntity = PassEntity(
        id = passId.toString(),
        satelliteId = satelliteId.toString(),
        tleId = UUID.randomUUID().toString(),
        orbitNumber = 42,
        aosEpochMillis = 1_000L,
        losEpochMillis = 2_000L,
        maxElevation = 45.0,
        aosAzimuth = 10.0,
        losAzimuth = 20.0,
        durationSec = 300,
        notify = false,
        outlookSynced = false,
        calculatedAtEpochMillis = 500L
    )

    private fun passDto(id: UUID = passId) = PassDto(
        id = id,
        satelliteId = satelliteId,
        tleId = UUID.randomUUID(),
        orbitNumber = 42,
        aos = OffsetDateTime.now(),
        los = OffsetDateTime.now().plusMinutes(5),
        maxElevation = 45.0,
        aosAzimuth = 10.0,
        losAzimuth = 20.0,
        durationSec = 300,
        outlookSynced = false,
        calculatedAt = OffsetDateTime.now()
    )

    @Before
    fun setUp() {
        repository = PassRepository(api, passDao, cacheMetadataDao, safeApiCall)
    }

    @Test
    fun `fresh cache returns cached data without calling network`() = runTest {
        coEvery { cacheMetadataDao.get(cacheKey) } returns CacheMetadataEntity(cacheKey, System.currentTimeMillis())
        coEvery { passDao.getCachedForSatellite(satelliteId.toString()) } returns listOf(cachedEntity)

        val result = repository.getPasses(satelliteId.toString())

        assertTrue(result is ApiResult.Success)
        assertEquals(1, (result as ApiResult.Success).data.size)
        coVerify(exactly = 0) { api.getUpcomingPasses(any()) }
    }

    @Test
    fun `stale cache with network success refreshes cache and preserves cached notify override`() = runTest {
        coEvery { cacheMetadataDao.get(cacheKey) } returns
            CacheMetadataEntity(cacheKey, System.currentTimeMillis() - TimeUnit.HOURS.toMillis(2))
        // Two calls: once to build the notify-preservation map, once (implicitly, not re-stubbed
        // differently) since the repository reads the cache before fetching.
        coEvery { passDao.getCachedForSatellite(satelliteId.toString()) } returns listOf(cachedEntity)
        coEvery { api.getUpcomingPasses(satelliteId) } returns Response.success(listOf(passDto()))

        val result = repository.getPasses(satelliteId.toString())

        assertTrue(result is ApiResult.Success)
        val pass = (result as ApiResult.Success).data.single()
        assertFalse(pass.notify) // preserved from cachedEntity, not reset to the true default

        val slot = slot<List<PassEntity>>()
        coVerify(exactly = 1) { passDao.replaceForSatellite(satelliteId.toString(), capture(slot)) }
        assertFalse(slot.captured.single().notify)
        coVerify(exactly = 1) { cacheMetadataDao.upsert(any()) }
    }

    @Test
    fun `stale cache with network success defaults notify to true for a newly seen pass`() = runTest {
        coEvery { cacheMetadataDao.get(cacheKey) } returns
            CacheMetadataEntity(cacheKey, System.currentTimeMillis() - TimeUnit.HOURS.toMillis(2))
        coEvery { passDao.getCachedForSatellite(satelliteId.toString()) } returns emptyList()
        val newPassId = UUID.randomUUID()
        coEvery { api.getUpcomingPasses(satelliteId) } returns Response.success(listOf(passDto(id = newPassId)))

        val result = repository.getPasses(satelliteId.toString())

        assertTrue(result is ApiResult.Success)
        assertTrue((result as ApiResult.Success).data.single().notify)
    }

    @Test
    fun `missing cache, network fails with NetworkError, propagates NetworkError`() = runTest {
        coEvery { cacheMetadataDao.get(cacheKey) } returns null
        coEvery { passDao.getCachedForSatellite(satelliteId.toString()) } returns emptyList()
        coEvery { api.getUpcomingPasses(satelliteId) } throws IOException("offline")

        val result = repository.getPasses(satelliteId.toString())

        assertTrue(result is ApiResult.NetworkError)
    }

    @Test
    fun `stale cache, network fails with NetworkError, Room has data, falls back to stale cache`() = runTest {
        coEvery { cacheMetadataDao.get(cacheKey) } returns
            CacheMetadataEntity(cacheKey, System.currentTimeMillis() - TimeUnit.HOURS.toMillis(2))
        coEvery { passDao.getCachedForSatellite(satelliteId.toString()) } returns listOf(cachedEntity)
        coEvery { api.getUpcomingPasses(satelliteId) } throws IOException("offline")

        val result = repository.getPasses(satelliteId.toString())

        assertTrue(result is ApiResult.Success)
        assertEquals(1, (result as ApiResult.Success).data.size)
    }

    @Test
    fun `network fails with Error, propagates Error even when stale cache exists`() = runTest {
        coEvery { cacheMetadataDao.get(cacheKey) } returns
            CacheMetadataEntity(cacheKey, System.currentTimeMillis() - TimeUnit.HOURS.toMillis(2))
        coEvery { passDao.getCachedForSatellite(satelliteId.toString()) } returns listOf(cachedEntity)
        val errorBody = """{"error":"boom"}""".toResponseBody("application/json".toMediaType())
        coEvery { api.getUpcomingPasses(satelliteId) } returns Response.error(500, errorBody)

        val result = repository.getPasses(satelliteId.toString())

        assertTrue(result is ApiResult.Error)
    }

    @Test
    fun `forceRefresh true skips fresh cache and calls network`() = runTest {
        coEvery { cacheMetadataDao.get(cacheKey) } returns CacheMetadataEntity(cacheKey, System.currentTimeMillis())
        coEvery { passDao.getCachedForSatellite(satelliteId.toString()) } returns listOf(cachedEntity)
        coEvery { api.getUpcomingPasses(satelliteId) } returns Response.success(listOf(passDto()))

        repository.getPasses(satelliteId.toString(), forceRefresh = true)

        coVerify(exactly = 1) { api.getUpcomingPasses(satelliteId) }
    }

    @Test
    fun `getPassTrack is a straight passthrough with no Room involvement`() = runTest {
        val trackDto = PassTrackDto(passId = passId, points = emptyList())
        coEvery { api.getPassTrack(passId) } returns Response.success(trackDto)

        val result = repository.getPassTrack(passId.toString())

        assertTrue(result is ApiResult.Success)
        coVerify(exactly = 0) { passDao.getCachedForSatellite(any()) }
        coVerify(exactly = 0) { passDao.replaceForSatellite(any(), any()) }
    }

    @Test
    fun `setNotify success updates the local PassEntity notify field immediately`() = runTest {
        coEvery { api.patchPassNotify(passId, any()) } returns
            Response.success(NotifyStatusDto(passId = passId, notify = false))

        val result = repository.setNotify(passId.toString(), notify = false)

        assertTrue(result is ApiResult.Success)
        coVerify(exactly = 1) { passDao.updateNotify(passId.toString(), false) }
    }

    @Test
    fun `setNotify non-Success result leaves the local cache untouched`() = runTest {
        coEvery { api.patchPassNotify(passId, any()) } throws IOException("offline")

        val result = repository.setNotify(passId.toString(), notify = false)

        assertTrue(result is ApiResult.NetworkError)
        coVerify(exactly = 0) { passDao.updateNotify(any(), any()) }
    }
}
