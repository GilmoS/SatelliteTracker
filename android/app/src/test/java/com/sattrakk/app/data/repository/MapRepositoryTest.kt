package com.sattrakk.app.data.repository

import com.sattrakk.app.data.local.PassDao
import com.sattrakk.app.data.local.entity.PassEntity
import com.sattrakk.app.data.remote.SatTrakkApi
import com.sattrakk.app.data.remote.dto.PositionDto
import com.sattrakk.app.data.remote.dto.TrackDto
import com.sattrakk.app.data.remote.dto.TrackPointDto
import com.sattrakk.app.data.session.SessionManager
import com.sattrakk.app.data.util.SafeApiCaller
import com.sattrakk.app.domain.model.ApiResult
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.confirmVerified
import io.mockk.mockk
import java.io.IOException
import java.util.UUID
import kotlinx.coroutines.test.runTest
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.ResponseBody.Companion.toResponseBody
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import retrofit2.Response

class MapRepositoryTest {

    private val api = mockk<SatTrakkApi>()
    private val passDao = mockk<PassDao>()
    private val safeApiCall = SafeApiCaller(mockk<SessionManager>(relaxUnitFun = true))
    private val repository = MapRepository(api, passDao, safeApiCall)

    private val satelliteId = "3f2b8c1e-9a4d-4e6f-8b7a-1c2d3e4f5a6b"
    private val satelliteUuid = UUID.fromString(satelliteId)

    private fun <T> errorBody(code: Int): Response<T> = Response.error(code, "".toResponseBody("application/json".toMediaType()))

    @Test
    fun `getPosition success maps to domain and converts seconds to millis`() = runTest {
        coEvery { api.getSatellitePosition(satelliteUuid) } returns Response.success(
            PositionDto(
                noradId = 1, satName = "SAT", latitude = 31.5, longitude = 34.8, altitude = 500.0,
                azimuth = 120.0, elevation = 10.0, timestamp = 1_700_000_000L
            )
        )

        val result = repository.getPosition(satelliteId)

        val position = (result as ApiResult.Success).data
        assertEquals(31.5, position.latitude, 0.0)
        assertEquals(34.8, position.longitude, 0.0)
        assertEquals(1_700_000_000_000L, position.timestampEpochMillis)
        confirmVerified(passDao)
    }

    @Test
    fun `getPosition failures propagate and never touch the DAO`() = runTest {
        coEvery { api.getSatellitePosition(satelliteUuid) } returns errorBody(500)
        assertTrue(repository.getPosition(satelliteId) is ApiResult.Error)

        coEvery { api.getSatellitePosition(satelliteUuid) } throws IOException("offline")
        assertTrue(repository.getPosition(satelliteId) is ApiResult.NetworkError)

        coEvery { api.getSatellitePosition(satelliteUuid) } returns errorBody(401)
        assertTrue(repository.getPosition(satelliteId) is ApiResult.AuthRequired)

        confirmVerified(passDao)
    }

    @Test
    fun `getLiveTrack success maps every point`() = runTest {
        coEvery { api.getSatelliteTrack(satelliteUuid) } returns Response.success(
            TrackDto(
                noradId = 1, satName = "SAT",
                points = listOf(
                    TrackPointDto(latitude = 1.0, longitude = 2.0, altitude = 500.0, timestamp = 10L),
                    TrackPointDto(latitude = 3.0, longitude = 4.0, altitude = 501.0, timestamp = 11L)
                )
            )
        )

        val points = (repository.getLiveTrack(satelliteId) as ApiResult.Success).data

        assertEquals(2, points.size)
        assertEquals(3.0, points[1].latitude, 0.0)
        assertEquals(11_000L, points[1].timestampEpochMillis)
        confirmVerified(passDao)
    }

    @Test
    fun `getLiveTrack failures propagate and never touch the DAO`() = runTest {
        coEvery { api.getSatelliteTrack(satelliteUuid) } returns errorBody(404)
        assertTrue(repository.getLiveTrack(satelliteId) is ApiResult.Error)

        coEvery { api.getSatelliteTrack(satelliteUuid) } throws IOException("offline")
        assertTrue(repository.getLiveTrack(satelliteId) is ApiResult.NetworkError)

        confirmVerified(passDao)
    }

    @Test
    fun `getNotifyEnabledPasses maps the DAO rows in order`() = runTest {
        coEvery { passDao.getNotifyEnabled() } returns listOf(entity("p1", aos = 1_000L), entity("p2", aos = 2_000L))

        val passes = repository.getNotifyEnabledPasses()

        assertEquals(listOf("p1", "p2"), passes.map { it.id })
        coVerify(exactly = 1) { passDao.getNotifyEnabled() }
    }

    private fun entity(id: String, aos: Long) = PassEntity(
        id = id,
        satelliteId = satelliteId,
        tleId = "tle",
        orbitNumber = 1,
        aosEpochMillis = aos,
        losEpochMillis = aos + 300_000,
        maxElevation = 40.0,
        aosAzimuth = 10.0,
        losAzimuth = 200.0,
        durationSec = 300,
        notify = true,
        outlookSynced = false,
        calculatedAtEpochMillis = 0L
    )
}
