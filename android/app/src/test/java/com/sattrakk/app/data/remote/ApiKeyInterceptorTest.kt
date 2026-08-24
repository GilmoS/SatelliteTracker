package com.sattrakk.app.data.remote

import com.sattrakk.app.data.local.ApiKeyStore
import io.mockk.every
import io.mockk.mockk
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Before
import org.junit.Test

class ApiKeyInterceptorTest {

    private lateinit var server: MockWebServer
    private lateinit var apiKeyStore: ApiKeyStore

    @Before
    fun setUp() {
        server = MockWebServer()
        server.start()
        apiKeyStore = mockk()
    }

    @After
    fun tearDown() {
        server.shutdown()
    }

    @Test
    fun `request gets X-Api-Key header when a key is stored`() {
        every { apiKeyStore.getKey() } returns "stored-key-123"
        val client = OkHttpClient.Builder().addInterceptor(ApiKeyInterceptor(apiKeyStore)).build()
        server.enqueue(MockResponse().setResponseCode(200))

        val request = Request.Builder().url(server.url("/api/satellites")).build()
        client.newCall(request).execute().close()

        val recorded = server.takeRequest()
        assertEquals("stored-key-123", recorded.getHeader("X-Api-Key"))
    }

    @Test
    fun `request is sent unmodified when no key is stored`() {
        every { apiKeyStore.getKey() } returns null
        val client = OkHttpClient.Builder().addInterceptor(ApiKeyInterceptor(apiKeyStore)).build()
        server.enqueue(MockResponse().setResponseCode(200))

        val request = Request.Builder().url(server.url("/api/auth/register")).build()
        client.newCall(request).execute().close()

        val recorded = server.takeRequest()
        assertNull(recorded.getHeader("X-Api-Key"))
    }
}
