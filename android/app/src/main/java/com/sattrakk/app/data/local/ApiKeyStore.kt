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
