package com.sattrakk.app.navigation

import android.content.Intent
import com.sattrakk.app.data.session.SessionManager
import io.mockk.every
import io.mockk.mockk
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

// The deep-link hand-off state between MainActivity and MainNavHost. The actual navigate() call
// happens in a Composable LaunchedEffect against a real NavController, which isn't unit-testable
// here (no Robolectric / navigation-testing / Compose UI test infrastructure in this project).
class AppViewModelTest {

    private val passId = "3f2b8c1e-9a4d-4e6f-8b7a-1c2d3e4f5a6b"
    private val viewModel = AppViewModel(mockk<SessionManager>(relaxed = true))

    private fun intent(type: String?, passId: String?): Intent = mockk {
        every { getStringExtra("type") } returns type
        every { getStringExtra("passId") } returns passId
    }

    @Test
    fun `pass reminder intent sets the pending passId`() {
        viewModel.onLaunchIntent(intent("pass_reminder", passId))

        assertEquals(passId, viewModel.pendingPassDetailsId.value)
    }

    @Test
    fun `consuming clears the pending passId`() {
        viewModel.onLaunchIntent(intent("pass_reminder", passId))

        viewModel.onPassDetailsDeepLinkConsumed()

        assertNull(viewModel.pendingPassDetailsId.value)
    }

    @Test
    fun `non-reminder intent does not clobber a pending passId`() {
        viewModel.onLaunchIntent(intent("pass_reminder", passId))

        viewModel.onLaunchIntent(intent(type = null, passId = null))

        assertEquals(passId, viewModel.pendingPassDetailsId.value)
    }

    @Test
    fun `null intent leaves nothing pending`() {
        viewModel.onLaunchIntent(null)

        assertNull(viewModel.pendingPassDetailsId.value)
    }
}
