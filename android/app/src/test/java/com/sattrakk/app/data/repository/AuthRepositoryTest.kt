package com.sattrakk.app.data.repository

import com.sattrakk.app.data.local.ApiKeyStore
import com.sattrakk.app.data.remote.SatTrakkApi
import com.sattrakk.app.data.remote.dto.RegisterResponse
import com.sattrakk.app.data.session.SessionManager
import com.sattrakk.app.data.util.SafeApiCaller
import com.sattrakk.app.domain.model.ApiResult
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.mockk
import java.io.IOException
import kotlinx.coroutines.test.runTest
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.ResponseBody.Companion.toResponseBody
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import retrofit2.Response

class AuthRepositoryTest {

    private val api = mockk<SatTrakkApi>()
    private val apiKeyStore = mockk<ApiKeyStore>(relaxUnitFun = true)
    private val safeApiCall = SafeApiCaller(mockk<SessionManager>(relaxUnitFun = true))
    private lateinit var repository: AuthRepository

    @Before
    fun setUp() {
        repository = AuthRepository(api, apiKeyStore, safeApiCall)
    }

    @Test
    fun `register success saves the raw api key from the response`() = runTest {
        coEvery { api.register(any()) } returns Response.success(
            RegisterResponse(apiKey = "raw-key-123", email = "tester@example.com", displayName = "Tester")
        )

        val result = repository.register("tester@example.com", "Tester")

        assertTrue(result is ApiResult.Success)
        coVerify(exactly = 1) { apiKeyStore.saveKey("raw-key-123") }
    }

    @Test
    fun `register 403 not allowlisted does not save any key`() = runTest {
        val errorBody = """{"error":"not allowlisted"}""".toResponseBody("application/json".toMediaType())
        coEvery { api.register(any()) } returns Response.error(403, errorBody)

        val result = repository.register("tester@example.com", "Tester")

        assertTrue(result is ApiResult.Error)
        coVerify(exactly = 0) { apiKeyStore.saveKey(any()) }
    }

    @Test
    fun `register 409 already registered does not save any key`() = runTest {
        val errorBody = """{"error":"already registered"}""".toResponseBody("application/json".toMediaType())
        coEvery { api.register(any()) } returns Response.error(409, errorBody)

        val result = repository.register("tester@example.com", "Tester")

        assertTrue(result is ApiResult.Error)
        coVerify(exactly = 0) { apiKeyStore.saveKey(any()) }
    }

    @Test
    fun `register NetworkError does not save any key`() = runTest {
        coEvery { api.register(any()) } throws IOException("offline")

        val result = repository.register("tester@example.com", "Tester")

        assertTrue(result is ApiResult.NetworkError)
        coVerify(exactly = 0) { apiKeyStore.saveKey(any()) }
    }
}
