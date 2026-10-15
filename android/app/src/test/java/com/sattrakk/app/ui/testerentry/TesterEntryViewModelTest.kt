package com.sattrakk.app.ui.testerentry

import com.sattrakk.app.MainDispatcherRule
import com.sattrakk.app.data.repository.AuthRepository
import com.sattrakk.app.data.session.SessionManager
import com.sattrakk.app.domain.model.ApiResult
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.mockk
import kotlinx.coroutines.ExperimentalCoroutinesApi
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test

// TesterEntryViewModel.register() has no long-lived polling/ticker coroutine (same shape as
// PassDetailsViewModel) -- its launched work always completes on its own, so these tests drive
// mainDispatcherRule.testDispatcher.scheduler directly via runCurrent() rather than runTest{}, per
// the established convention (see DashboardViewModelTest's comment for the full rationale).
@OptIn(ExperimentalCoroutinesApi::class)
class TesterEntryViewModelTest {

    @get:Rule
    val mainDispatcherRule = MainDispatcherRule()

    private val authRepository = mockk<AuthRepository>()
    private val sessionManager = mockk<SessionManager>(relaxUnitFun = true)

    private fun runCurrent() = mainDispatcherRule.testDispatcher.scheduler.runCurrent()

    private fun createViewModel() = TesterEntryViewModel(authRepository, sessionManager)

    @Test
    fun `register 403 maps to NotAllowlisted`() {
        coEvery { authRepository.register(any(), any()) } returns ApiResult.Error(403, "not allowlisted")
        val viewModel = createViewModel()

        viewModel.register("tester@example.com", "Tester")
        runCurrent()

        assertEquals(TesterEntryUiState.NotAllowlisted, viewModel.uiState.value)
        coVerify(exactly = 0) { sessionManager.markValid() }
    }

    @Test
    fun `register 409 maps to AlreadyRegistered`() {
        coEvery { authRepository.register(any(), any()) } returns ApiResult.Error(409, "already registered")
        val viewModel = createViewModel()

        viewModel.register("tester@example.com", "Tester")
        runCurrent()

        assertEquals(TesterEntryUiState.AlreadyRegistered, viewModel.uiState.value)
        coVerify(exactly = 0) { sessionManager.markValid() }
    }

    @Test
    fun `register other Error maps to generic Error state with message`() {
        coEvery { authRepository.register(any(), any()) } returns ApiResult.Error(500, "server error")
        val viewModel = createViewModel()

        viewModel.register("tester@example.com", "Tester")
        runCurrent()

        assertEquals(TesterEntryUiState.Error("server error"), viewModel.uiState.value)
        coVerify(exactly = 0) { sessionManager.markValid() }
    }

    @Test
    fun `register NetworkError maps to generic Error state`() {
        coEvery { authRepository.register(any(), any()) } returns ApiResult.NetworkError
        val viewModel = createViewModel()

        viewModel.register("tester@example.com", "Tester")
        runCurrent()

        assertTrue(viewModel.uiState.value is TesterEntryUiState.Error)
        coVerify(exactly = 0) { sessionManager.markValid() }
    }

    @Test
    fun `register success transitions to Success and marks the session valid`() {
        coEvery { authRepository.register(any(), any()) } returns ApiResult.Success(Unit)
        val viewModel = createViewModel()

        viewModel.register("tester@example.com", "Tester")
        runCurrent()

        assertEquals(TesterEntryUiState.Success, viewModel.uiState.value)
        coVerify(exactly = 1) { sessionManager.markValid() }
    }

    // AuthRepository.register() already saves the raw key internally (step 2.3's design) -- this
    // ViewModel must only react to the ApiResult, never touch ApiKeyStore itself. There's no
    // ApiKeyStore reference reachable from this ViewModel at all, so the strongest thing this test
    // can assert is that the ViewModel calls the repository exactly once and does nothing beyond
    // updating its own state / SessionManager.
    @Test
    fun `register success calls the repository exactly once and nothing else`() {
        coEvery { authRepository.register(any(), any()) } returns ApiResult.Success(Unit)
        val viewModel = createViewModel()

        viewModel.register("tester@example.com", "Tester")
        runCurrent()

        coVerify(exactly = 1) { authRepository.register("tester@example.com", "Tester") }
    }

    @Test
    fun `register sets Loading state immediately`() {
        coEvery { authRepository.register(any(), any()) } returns ApiResult.Success(Unit)
        val viewModel = createViewModel()

        viewModel.register("tester@example.com", "Tester")

        assertEquals(TesterEntryUiState.Loading, viewModel.uiState.value)
    }

    @Test
    fun `initial state is Idle`() {
        val viewModel = createViewModel()

        assertEquals(TesterEntryUiState.Idle, viewModel.uiState.value)
    }
}
