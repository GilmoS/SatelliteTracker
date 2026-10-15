package com.sattrakk.app.data.repository

import com.sattrakk.app.data.local.CacheMetadataDao
import com.sattrakk.app.data.local.SatelliteDao
import com.sattrakk.app.data.local.entity.CacheMetadataEntity
import com.sattrakk.app.data.local.entity.SatelliteEntity
import com.sattrakk.app.data.remote.SatTrakkApi
import com.sattrakk.app.data.remote.dto.SatelliteDto
import com.sattrakk.app.data.session.SessionManager
import com.sattrakk.app.data.util.SafeApiCaller
import com.sattrakk.app.domain.model.ApiResult
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.mockk
import java.io.IOException
import java.time.OffsetDateTime
import java.util.UUID
import java.util.concurrent.TimeUnit
import kotlinx.coroutines.test.runTest
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.ResponseBody.Companion.toResponseBody
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import retrofit2.Response

class SatelliteRepositoryTest {

    private val api = mockk<SatTrakkApi>()
    private val satelliteDao = mockk<SatelliteDao>(relaxUnitFun = true)
    private val cacheMetadataDao = mockk<CacheMetadataDao>(relaxUnitFun = true)
    private val safeApiCall = SafeApiCaller(mockk<SessionManager>(relaxUnitFun = true))
    private lateinit var repository: SatelliteRepository

    private val satelliteId = UUID.randomUUID()
    private val cachedEntity = SatelliteEntity(
        id = satelliteId.toString(),
        name = "EROS C3",
        noradId = 1,
        description = null,
        isActive = true,
        isDefault = true,
        createdAtEpochMillis = 1_000L
    )
    private val dto = SatelliteDto(
        id = satelliteId,
        name = "EROS C3",
        noradId = 1,
        description = null,
        isActive = true,
        isDefault = true,
        createdAt = OffsetDateTime.now()
    )

    @Before
    fun setUp() {
        repository = SatelliteRepository(api, satelliteDao, cacheMetadataDao, safeApiCall)
    }

    @Test
    fun `fresh cache returns cached data without calling network`() = runTest {
        coEvery { cacheMetadataDao.get("satellites") } returns
            CacheMetadataEntity("satellites", System.currentTimeMillis())
        coEvery { satelliteDao.getCached() } returns listOf(cachedEntity)

        val result = repository.getSatellites()

        assertTrue(result is ApiResult.Success)
        assertEquals(1, (result as ApiResult.Success).data.size)
        coVerify(exactly = 0) { api.getSatellites() }
    }

    @Test
    fun `stale cache with network success refreshes cache and returns fresh data`() = runTest {
        coEvery { cacheMetadataDao.get("satellites") } returns
            CacheMetadataEntity("satellites", System.currentTimeMillis() - TimeUnit.HOURS.toMillis(25))
        coEvery { api.getSatellites() } returns Response.success(listOf(dto))

        val result = repository.getSatellites()

        assertTrue(result is ApiResult.Success)
        assertEquals(1, (result as ApiResult.Success).data.size)
        assertEquals("EROS C3", result.data[0].name)
        coVerify(exactly = 1) { api.getSatellites() }
        coVerify(exactly = 1) { satelliteDao.replaceAll(any()) }
        coVerify(exactly = 1) { cacheMetadataDao.upsert(any()) }
    }

    @Test
    fun `missing cache with network success fetches and populates cache`() = runTest {
        coEvery { cacheMetadataDao.get("satellites") } returns null
        coEvery { api.getSatellites() } returns Response.success(listOf(dto))

        val result = repository.getSatellites()

        assertTrue(result is ApiResult.Success)
        coVerify(exactly = 1) { api.getSatellites() }
        coVerify(exactly = 1) { satelliteDao.replaceAll(any()) }
    }

    @Test
    fun `stale cache, network fails with NetworkError, Room has data, falls back to stale cache`() = runTest {
        coEvery { cacheMetadataDao.get("satellites") } returns
            CacheMetadataEntity("satellites", System.currentTimeMillis() - TimeUnit.HOURS.toMillis(25))
        coEvery { satelliteDao.getCached() } returns listOf(cachedEntity)
        coEvery { api.getSatellites() } throws IOException("offline")

        val result = repository.getSatellites()

        assertTrue(result is ApiResult.Success)
        assertEquals(1, (result as ApiResult.Success).data.size)
    }

    @Test
    fun `missing cache, network fails with NetworkError, propagates NetworkError not empty list`() = runTest {
        coEvery { cacheMetadataDao.get("satellites") } returns null
        coEvery { api.getSatellites() } throws IOException("offline")

        val result = repository.getSatellites()

        assertTrue(result is ApiResult.NetworkError)
        coVerify(exactly = 0) { satelliteDao.getCached() }
    }

    @Test
    fun `network fails with Error, propagates Error even when stale cache exists`() = runTest {
        coEvery { cacheMetadataDao.get("satellites") } returns
            CacheMetadataEntity("satellites", System.currentTimeMillis() - TimeUnit.HOURS.toMillis(25))
        val errorBody = """{"error":"boom"}""".toResponseBody("application/json".toMediaType())
        coEvery { api.getSatellites() } returns Response.error(500, errorBody)

        val result = repository.getSatellites()

        assertTrue(result is ApiResult.Error)
        coVerify(exactly = 0) { satelliteDao.replaceAll(any()) }
    }

    @Test
    fun `network fails with AuthRequired, propagates AuthRequired even when stale cache exists`() = runTest {
        coEvery { cacheMetadataDao.get("satellites") } returns
            CacheMetadataEntity("satellites", System.currentTimeMillis() - TimeUnit.HOURS.toMillis(25))
        coEvery { api.getSatellites() } returns Response.error(
            401,
            "".toResponseBody("application/json".toMediaType())
        )

        val result = repository.getSatellites()

        assertTrue(result is ApiResult.AuthRequired)
    }

    @Test
    fun `forceRefresh true skips fresh cache and calls network`() = runTest {
        coEvery { cacheMetadataDao.get("satellites") } returns
            CacheMetadataEntity("satellites", System.currentTimeMillis())
        coEvery { api.getSatellites() } returns Response.success(listOf(dto))

        repository.getSatellites(forceRefresh = true)

        coVerify(exactly = 1) { api.getSatellites() }
    }
}
