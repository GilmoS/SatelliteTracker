package com.sattrakk.app.data.session

import org.junit.Assert.assertEquals
import org.junit.Before
import org.junit.Test

class SessionManagerTest {

    private lateinit var sessionManager: SessionManager

    @Before
    fun setUp() {
        sessionManager = SessionManager()
    }

    @Test
    fun `initial state is Valid`() {
        assertEquals(SessionState.Valid, sessionManager.sessionState.value)
    }

    @Test
    fun `markReauthRequired transitions to RequiresReauth`() {
        sessionManager.markReauthRequired()

        assertEquals(SessionState.RequiresReauth, sessionManager.sessionState.value)
    }

    @Test
    fun `markValid transitions back to Valid`() {
        sessionManager.markReauthRequired()
        sessionManager.markValid()

        assertEquals(SessionState.Valid, sessionManager.sessionState.value)
    }
}
