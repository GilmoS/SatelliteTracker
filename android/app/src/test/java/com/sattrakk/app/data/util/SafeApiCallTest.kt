package com.sattrakk.app.data.util

import com.sattrakk.app.domain.model.ApiResult
import java.io.IOException
import kotlinx.coroutines.test.runTest
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.ResponseBody.Companion.toResponseBody
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import retrofit2.Response

class SafeApiCallTest {

    @Test
    fun `successful response maps to Success with the body`() = runTest {
        val result = safeApiCall { Response.success("hello") }

        assertTrue(result is ApiResult.Success<String>)
        assertEquals("hello", (result as ApiResult.Success<String>).data)
    }

    @Test
    fun `401 response maps to AuthRequired`() = runTest {
        val errorBody = """{"error":"Missing or invalid API key."}"""
            .toResponseBody("application/json".toMediaType())

        val result = safeApiCall<String> { Response.error(401, errorBody) }

        assertTrue(result is ApiResult.AuthRequired)
    }

    @Test
    fun `500 response maps to Error with the code and parsed message`() = runTest {
        val errorBody = """{"error":"Something went wrong."}"""
            .toResponseBody("application/json".toMediaType())

        val result = safeApiCall<String> { Response.error(500, errorBody) }

        assertTrue(result is ApiResult.Error)
        val error = result as ApiResult.Error
        assertEquals(500, error.code)
        assertEquals("Something went wrong.", error.message)
    }

    @Test
    fun `error response with unparseable body falls back to a generic message`() = runTest {
        val errorBody = "not json".toResponseBody("text/plain".toMediaType())

        val result = safeApiCall<String> { Response.error(404, errorBody) }

        assertTrue(result is ApiResult.Error)
        val error = result as ApiResult.Error
        assertEquals(404, error.code)
        assertEquals("Request failed with status 404", error.message)
    }

    @Test
    fun `thrown IOException maps to NetworkError`() = runTest {
        val result = safeApiCall<String> { throw IOException("no connection") }

        assertTrue(result is ApiResult.NetworkError)
    }
}
