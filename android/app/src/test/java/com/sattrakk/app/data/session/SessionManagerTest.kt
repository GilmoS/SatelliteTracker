package com.sattrakk.app.data.session

import com.sattrakk.app.data.local.ApiKeyStore
import io.mockk.every
import io.mockk.mockk
import org.junit.Assert.assertEquals
import org.junit.Test

class SessionManagerTest {

    private fun sessionManager(storedKey: String?): SessionManager {
        val apiKeyStore = mockk<ApiKeyStore>()
        every { apiKeyStore.getKey() } returns storedKey
        return SessionManager(apiKeyStore)
    }

    @Test
    fun `initial state is Valid when a key is already stored`() {
        assertEquals(SessionState.Valid, sessionManager(storedKey = "raw-key").sessionState.value)
    }

    @Test
    fun `initial state is RequiresReauth when no key is stored`() {
        assertEquals(SessionState.RequiresReauth, sessionManager(storedKey = null).sessionState.value)
    }

    @Test
    fun `markReauthRequired transitions to RequiresReauth`() {
        val sessionManager = sessionManager(storedKey = "raw-key")

        sessionManager.markReauthRequired()

        assertEquals(SessionState.RequiresReauth, sessionManager.sessionState.value)
    }

    @Test
    fun `markValid transitions back to Valid`() {
        val sessionManager = sessionManager(storedKey = null)

        sessionManager.markValid()

        assertEquals(SessionState.Valid, sessionManager.sessionState.value)
    }
}
