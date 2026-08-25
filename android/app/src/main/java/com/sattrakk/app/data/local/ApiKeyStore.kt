// EncryptedSharedPreferences/MasterKey are deprecated — see the class doc comment below for why
// this file still uses them. A class-level @Suppress doesn't cover deprecation warnings on the
// import statements themselves, hence the file-level annotation.
@file:Suppress("DEPRECATION")

package com.sattrakk.app.data.local

import android.content.Context
import android.content.SharedPreferences
import androidx.security.crypto.EncryptedSharedPreferences
import androidx.security.crypto.MasterKey
import dagger.hilt.android.qualifiers.ApplicationContext
import javax.inject.Inject
import javax.inject.Singleton

// The single place in the app that touches EncryptedSharedPreferences — everything else that
// needs the raw API key (the auth interceptor, future settings/session UI) goes through
// getKey()/saveKey() here instead of reading SharedPreferences directly.
//
// EncryptedSharedPreferences and MasterKey are deprecated in security-crypto 1.1.0 with no
// maintained replacement shipped — MasterKey's own deprecation note says to use
// javax.crypto.KeyGenerator + AndroidKeyStore directly, and EncryptedSharedPreferences' just
// says "use android.content.SharedPreferences," i.e. roll your own AndroidKeyStore-backed
// Cipher encryption on top of plain prefs. Both classes are still fully functional and remain
// the most widely used pattern for this exact use case; accepted as-is rather than hand-rolling
// key management (see android/CLAUDE.md).
@Singleton
class ApiKeyStore @Inject constructor(
    @ApplicationContext context: Context
) {
    private val prefs: SharedPreferences by lazy {
        val masterKey = MasterKey.Builder(context)
            .setKeyScheme(MasterKey.KeyScheme.AES256_GCM)
            .build()

        EncryptedSharedPreferences.create(
            context,
            PREFS_FILE_NAME,
            masterKey,
            EncryptedSharedPreferences.PrefKeyEncryptionScheme.AES256_SIV,
            EncryptedSharedPreferences.PrefValueEncryptionScheme.AES256_GCM
        )
    }

    fun getKey(): String? = prefs.getString(KEY_API_KEY, null)

    fun saveKey(apiKey: String) {
        prefs.edit().putString(KEY_API_KEY, apiKey).apply()
    }

    private companion object {
        const val PREFS_FILE_NAME = "sattrakk_secure_prefs"
        const val KEY_API_KEY = "api_key"
    }
}
