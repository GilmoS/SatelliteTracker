package com.sattrakk.app

import androidx.compose.ui.test.junit4.createAndroidComposeRule
import androidx.compose.ui.test.onNodeWithText
import androidx.test.ext.junit.runners.AndroidJUnit4
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith

/** Smoke test: the skeleton launches and the start destination (Dashboard) renders. */
@Suppress("DEPRECATION")
@RunWith(AndroidJUnit4::class)
class MainActivityTest {
    @get:Rule
    val composeTestRule = createAndroidComposeRule<MainActivity>()

    @Test
    fun dashboardIsStartDestination() {
        composeTestRule.onNodeWithText("Dashboard").assertExists()
    }
}
