package com.sattrakk.app

import androidx.compose.ui.test.junit4.createAndroidComposeRule
import androidx.compose.ui.test.onNodeWithText
import androidx.test.ext.junit.runners.AndroidJUnit4
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith

/**
 * Smoke test: the app launches and the start destination (Dashboard) renders.
 *
 * Asserts on the top app bar title ("SatelliteTracker") rather than any load-state-dependent
 * content — Dashboard now shows real ViewModel-driven content (loading/error/data), so a literal
 * "Dashboard" string no longer exists anywhere on screen, but the static top app bar title is
 * present regardless of load state (see DashboardScreen).
 */
@RunWith(AndroidJUnit4::class)
class MainActivityTest {
    @get:Rule
    val composeTestRule = createAndroidComposeRule<MainActivity>()

    @Test
    fun dashboardIsStartDestination() {
        composeTestRule.onNodeWithText("SatelliteTracker").assertExists()
    }
}
