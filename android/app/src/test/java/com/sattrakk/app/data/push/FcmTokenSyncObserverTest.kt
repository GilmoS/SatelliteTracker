package com.sattrakk.app.data.push

import com.sattrakk.app.data.local.ApiKeyStore
import com.sattrakk.app.data.local.FcmTokenStore
import com.sattrakk.app.data.repository.SettingsRepository
import com.sattrakk.app.data.session.SessionManager
import com.sattrakk.app.domain.model.ApiResult
import com.sattrakk.app.domain.model.UserSettings
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.every
import io.mockk.mockk
import io.mockk.verify
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.test.TestScope
import kotlinx.coroutines.test.runCurrent
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

// The observer's collectors never complete on their own (they follow two infinite Flows), so it
// runs on runTest's backgroundScope — cancelled automatically when the test body ends, which
// avoids the never-reaches-idle drain problem documented in DashboardViewModelTest.
@OptIn(ExperimentalCoroutinesApi::class)
class FcmTokenSyncObserverTest {

    private class FakeFcmTokenStore(initial: String? = null) : FcmTokenStore {
        val state = MutableStateFlow(initial)
        override val pendingToken = state
        override suspend fun savePendingToken(token: String) { state.value = token }
        override suspend fun clearPendingToken() { state.value = null }
    }

    private val settingsRepository = mockk<SettingsRepository>()
    private val fcmTokenFetcher = mockk<FcmTokenFetcher>(relaxed = true)

    private fun sessionManager(valid: Boolean): SessionManager {
        val apiKeyStore = mockk<ApiKeyStore>()
        every { apiKeyStore.getKey() } returns if (valid) "raw-key" else null
        return SessionManager(apiKeyStore)
    }

    private fun TestScope.startObserver(sessionManager: SessionManager, store: FcmTokenStore) {
        FcmTokenSyncObserver(sessionManager, store, settingsRepository, fcmTokenFetcher, backgroundScope).start()
        runCurrent()
    }

    private val success = ApiResult.Success(UserSettings(alertMinutes = emptyList(), fcmToken = "token-1"))

    @Test
    fun `valid session with a pending token sends it and clears it on success`() = runTest {
        coEvery { settingsRepository.updateFcmToken("token-1") } returns success
        val store = FakeFcmTokenStore(initial = "token-1")

        startObserver(sessionManager(valid = true), store)

        coVerify(exactly = 1) { settingsRepository.updateFcmToken("token-1") }
        assertNull(store.state.value)
    }

    @Test
    fun `failed send leaves the pending token in place`() = runTest {
        coEvery { settingsRepository.updateFcmToken("token-1") } returns ApiResult.NetworkError
        val store = FakeFcmTokenStore(initial = "token-1")

        startObserver(sessionManager(valid = true), store)

        coVerify(exactly = 1) { settingsRepository.updateFcmToken("token-1") }
        assertEquals("token-1", store.state.value)
    }

    @Test
    fun `backend error also leaves the pending token in place`() = runTest {
        coEvery { settingsRepository.updateFcmToken("token-1") } returns ApiResult.Error(500, "boom")
        val store = FakeFcmTokenStore(initial = "token-1")

        startObserver(sessionManager(valid = true), store)

        assertEquals("token-1", store.state.value)
    }

    @Test
    fun `RequiresReauth with a pending token does not send`() = runTest {
        val store = FakeFcmTokenStore(initial = "token-1")

        startObserver(sessionManager(valid = false), store)

        coVerify(exactly = 0) { settingsRepository.updateFcmToken(any()) }
        assertEquals("token-1", store.state.value)
    }

    @Test
    fun `valid session with no pending token does not send`() = runTest {
        startObserver(sessionManager(valid = true), FakeFcmTokenStore(initial = null))

        coVerify(exactly = 0) { settingsRepository.updateFcmToken(any()) }
    }

    @Test
    fun `token held during RequiresReauth is sent once the session becomes Valid`() = runTest {
        coEvery { settingsRepository.updateFcmToken("token-1") } returns success
        val sessionManager = sessionManager(valid = false)
        val store = FakeFcmTokenStore(initial = "token-1")
        startObserver(sessionManager, store)

        sessionManager.markValid()
        runCurrent()

        coVerify(exactly = 1) { settingsRepository.updateFcmToken("token-1") }
        assertNull(store.state.value)
    }

    @Test
    fun `token arriving while already Valid is sent`() = runTest {
        coEvery { settingsRepository.updateFcmToken("token-2") } returns success
        val store = FakeFcmTokenStore(initial = null)
        startObserver(sessionManager(valid = true), store)

        store.savePendingToken("token-2")
        runCurrent()

        coVerify(exactly = 1) { settingsRepository.updateFcmToken("token-2") }
        assertNull(store.state.value)
    }

    @Test
    fun `a newer token saved while a send is in flight is not cleared`() = runTest {
        val firstSend = CompletableDeferred<ApiResult<UserSettings>>()
        coEvery { settingsRepository.updateFcmToken("token-1") } coAnswers { firstSend.await() }
        coEvery { settingsRepository.updateFcmToken("token-2") } returns ApiResult.NetworkError
        val store = FakeFcmTokenStore(initial = "token-1")
        startObserver(sessionManager(valid = true), store)

        store.savePendingToken("token-2")
        firstSend.complete(success)
        runCurrent()

        assertEquals("token-2", store.state.value)
    }

    @Test
    fun `RequiresReauth to Valid transition proactively fetches the current token`() = runTest {
        val sessionManager = sessionManager(valid = false)
        startObserver(sessionManager, FakeFcmTokenStore())

        sessionManager.markValid()
        runCurrent()

        verify(exactly = 1) { fcmTokenFetcher.fetchToken() }
    }

    @Test
    fun `starting already Valid does not fetch the token`() = runTest {
        startObserver(sessionManager(valid = true), FakeFcmTokenStore())

        verify(exactly = 0) { fcmTokenFetcher.fetchToken() }
    }
}
