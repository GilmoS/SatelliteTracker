package com.sattrakk.app.data.local

import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.core.PreferenceDataStoreFactory
import java.io.File
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.test.runTest
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test

// Backed by a temp-file DataStore instance — the standard androidx.datastore JVM testing approach
// (PreferenceDataStoreFactory.create() needs only a produceFile lambda, no Android Context) —
// rather than an instrumented test. Unlike ApiKeyStore's EncryptedSharedPreferences (which needs
// the real Android Keystore), Preferences DataStore has no Android-framework dependency forcing
// this onto a device/emulator.
class HiddenSatellitesStoreTest {

    private lateinit var tempFile: File
    private lateinit var dataStore: DataStore<Preferences>
    private lateinit var store: HiddenSatellitesStore

    @Before
    fun setUp() {
        tempFile = File.createTempFile("hidden_satellites_test", ".preferences_pb")
        dataStore = PreferenceDataStoreFactory.create(produceFile = { tempFile })
        store = DataStoreHiddenSatellitesStore(dataStore)
    }

    @After
    fun tearDown() {
        tempFile.delete()
    }

    @Test
    fun `hiddenSatelliteIds starts empty`() = runTest {
        assertEquals(emptySet<String>(), store.hiddenSatelliteIds.first())
    }

    @Test
    fun `setHidden true then read back reflects the hidden satellite`() = runTest {
        store.setHidden("sat-1", hidden = true)

        assertEquals(setOf("sat-1"), store.hiddenSatelliteIds.first())
    }

    @Test
    fun `setHidden true for multiple satellites accumulates`() = runTest {
        store.setHidden("sat-1", hidden = true)
        store.setHidden("sat-2", hidden = true)

        assertEquals(setOf("sat-1", "sat-2"), store.hiddenSatelliteIds.first())
    }

    @Test
    fun `setHidden false removes a previously hidden satellite`() = runTest {
        store.setHidden("sat-1", hidden = true)
        store.setHidden("sat-2", hidden = true)

        store.setHidden("sat-1", hidden = false)

        assertEquals(setOf("sat-2"), store.hiddenSatelliteIds.first())
    }

    @Test
    fun `setHidden false on a never-hidden satellite is a no-op`() = runTest {
        store.setHidden("sat-1", hidden = false)

        assertTrue(store.hiddenSatelliteIds.first().isEmpty())
    }

    // A second DataStore instance backed by the same file, opened concurrently with the first, is
    // deliberately not exercised here — DataStore enforces single-instance-per-file within a
    // process and throws if a second one is opened while the first is still live, which isn't
    // something HiddenSatellitesStore itself needs to guard against (there's only ever one
    // Hilt-provided singleton instance in the running app). The round-trip tests above already
    // confirm every write is durably persisted to `dataStore.data`, which is what "survives a
    // restart" actually depends on.
}
