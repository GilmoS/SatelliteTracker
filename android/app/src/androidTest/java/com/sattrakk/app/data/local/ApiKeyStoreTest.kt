package com.sattrakk.app.data.local

import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import org.junit.Assert.assertEquals
import org.junit.Test
import org.junit.runner.RunWith

// Instrumented, not a plain JVM unit test: EncryptedSharedPreferences needs the real
// Android Keystore, which isn't available on the JVM. See ApiKeyStore for why the class
// constructs it directly rather than taking an injectable SharedPreferences.
@RunWith(AndroidJUnit4::class)
class ApiKeyStoreTest {

    @Test
    fun saveKey_thenGetKey_roundTrips() {
        val store = ApiKeyStore(InstrumentationRegistry.getInstrumentation().targetContext)

        store.saveKey("abc123def456")

        assertEquals("abc123def456", store.getKey())
    }
}
