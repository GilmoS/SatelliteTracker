package com.sattrakk.app.data.local

import java.io.File
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.test.runTest
import org.junit.After
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class NotificationPromptStoreTest {

    private val tempFile: File = newTempPreferencesFile("notification_prompt_test")
    private val store: NotificationPromptStore =
        DataStoreNotificationPromptStore(testPreferencesDataStore(tempFile))

    @After
    fun tearDown() {
        tempFile.delete()
    }

    @Test
    fun `hasRequestedNotificationPermission starts false`() = runTest {
        assertFalse(store.hasRequestedNotificationPermission.first())
    }

    @Test
    fun `markNotificationPermissionRequested flips it to true`() = runTest {
        store.markNotificationPermissionRequested()

        assertTrue(store.hasRequestedNotificationPermission.first())
    }
}
