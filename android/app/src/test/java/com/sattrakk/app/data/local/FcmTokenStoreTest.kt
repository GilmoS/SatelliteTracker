package com.sattrakk.app.data.local

import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.Preferences
import java.io.File
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.test.runTest
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Before
import org.junit.Test

// Same temp-file DataStore approach as HiddenSatellitesStoreTest — no Android Context needed.
class FcmTokenStoreTest {

    private lateinit var tempFile: File
    private lateinit var dataStore: DataStore<Preferences>
    private lateinit var store: FcmTokenStore

    @Before
    fun setUp() {
        tempFile = newTempPreferencesFile("fcm_token_test")
        dataStore = testPreferencesDataStore(tempFile)
        store = DataStoreFcmTokenStore(dataStore)
    }

    @After
    fun tearDown() {
        tempFile.delete()
    }

    @Test
    fun `pendingToken starts null`() = runTest {
        assertNull(store.pendingToken.first())
    }

    @Test
    fun `savePendingToken then read back returns the token`() = runTest {
        store.savePendingToken("token-1")

        assertEquals("token-1", store.pendingToken.first())
    }

    @Test
    fun `savePendingToken overwrites an earlier pending token`() = runTest {
        store.savePendingToken("token-1")
        store.savePendingToken("token-2")

        assertEquals("token-2", store.pendingToken.first())
    }

    @Test
    fun `clearPendingToken removes the pending token`() = runTest {
        store.savePendingToken("token-1")

        store.clearPendingToken()

        assertNull(store.pendingToken.first())
    }

    @Test
    fun `clearPendingToken with nothing pending is a no-op`() = runTest {
        store.clearPendingToken()

        assertNull(store.pendingToken.first())
    }
}
