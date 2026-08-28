package com.sattrakk.app.data.repository

import com.sattrakk.app.data.remote.SatTrakkApi
import com.sattrakk.app.data.remote.dto.UpdateAlertMinutesRequest
import com.sattrakk.app.data.remote.dto.UpdateFcmTokenRequest
import com.sattrakk.app.data.remote.dto.UserSettingsDto
import com.sattrakk.app.domain.model.ApiResult
import io.mockk.coEvery
import io.mockk.mockk
import io.mockk.slot
import java.io.IOException
import kotlinx.coroutines.test.runTest
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.ResponseBody.Companion.toResponseBody
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import retrofit2.Response

class SettingsRepositoryTest {

    private val api = mockk<SatTrakkApi>()
    private lateinit var repository: SettingsRepository

    @Before
    fun setUp() {
        repository = SettingsRepository(api)
    }

    @Test
    fun `getSettings success maps to domain UserSettings`() = runTest {
        coEvery { api.getMySettings() } returns Response.success(
            UserSettingsDto(alertMinutes = listOf(5, 10), fcmToken = "token-abc")
        )

        val result = repository.getSettings()

        assertTrue(result is ApiResult.Success)
        val settings = (result as ApiResult.Success).data
        assertEquals(listOf(5, 10), settings.alertMinutes)
        assertEquals("token-abc", settings.fcmToken)
    }

    @Test
    fun `getSettings success maps the computed default of empty alertMinutes and null fcmToken`() = runTest {
        coEvery { api.getMySettings() } returns Response.success(
            UserSettingsDto(alertMinutes = emptyList(), fcmToken = null)
        )

        val result = repository.getSettings()

        assertTrue(result is ApiResult.Success)
        val settings = (result as ApiResult.Success).data
        assertEquals(emptyList<Int>(), settings.alertMinutes)
        assertNull(settings.fcmToken)
    }

    @Test
    fun `getSettings AuthRequired is propagated as-is`() = runTest {
        coEvery { api.getMySettings() } returns
            Response.error(401, "".toResponseBody("application/json".toMediaType()))

        val result = repository.getSettings()

        assertTrue(result is ApiResult.AuthRequired)
    }

    @Test
    fun `getSettings NetworkError is propagated as-is`() = runTest {
        coEvery { api.getMySettings() } throws IOException("offline")

        val result = repository.getSettings()

        assertTrue(result is ApiResult.NetworkError)
    }

    @Test
    fun `updateAlertMinutes sends the request and maps the response`() = runTest {
        val requestSlot = slot<UpdateAlertMinutesRequest>()
        coEvery { api.updateMyAlertMinutes(capture(requestSlot)) } returns Response.success(
            UserSettingsDto(alertMinutes = listOf(15, 30), fcmToken = null)
        )

        val result = repository.updateAlertMinutes(listOf(15, 30))

        assertTrue(result is ApiResult.Success)
        assertEquals(listOf(15, 30), requestSlot.captured.alertMinutes)
        assertEquals(listOf(15, 30), (result as ApiResult.Success).data.alertMinutes)
    }

    @Test
    fun `updateAlertMinutes failure is propagated as-is`() = runTest {
        val errorBody = """{"error":"invalid alert minutes"}""".toResponseBody("application/json".toMediaType())
        coEvery { api.updateMyAlertMinutes(any()) } returns Response.error(400, errorBody)

        val result = repository.updateAlertMinutes(listOf(7))

        assertTrue(result is ApiResult.Error)
    }

    @Test
    fun `updateFcmToken sends the request and maps the response`() = runTest {
        val requestSlot = slot<UpdateFcmTokenRequest>()
        coEvery { api.updateMyFcmToken(capture(requestSlot)) } returns Response.success(
            UserSettingsDto(alertMinutes = emptyList(), fcmToken = "new-token")
        )

        val result = repository.updateFcmToken("new-token")

        assertTrue(result is ApiResult.Success)
        assertEquals("new-token", requestSlot.captured.fcmToken)
        assertEquals("new-token", (result as ApiResult.Success).data.fcmToken)
    }

    @Test
    fun `updateFcmToken NetworkError is propagated as-is`() = runTest {
        coEvery { api.updateMyFcmToken(any()) } throws IOException("offline")

        val result = repository.updateFcmToken("new-token")

        assertTrue(result is ApiResult.NetworkError)
    }
}
