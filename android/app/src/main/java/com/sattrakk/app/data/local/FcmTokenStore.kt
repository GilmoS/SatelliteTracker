package com.sattrakk.app.data.local

import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.stringPreferencesKey
import javax.inject.Inject
import javax.inject.Singleton
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.map

// Holds an FCM token that hasn't been confirmed-sent to the backend yet. FCM can hand the app a
// token (SatTrakkMessagingService.onNewToken, or the proactive fetch in FcmTokenFetcher) at any
// time, including before a tester has registered — i.e. while SessionManager is RequiresReauth and
// PUT /api/settings/me/fcm-token would just 401. Dropping it then would mean the backend never
// learns this device's token until FCM happens to rotate it. So every token lands here
// unconditionally, and FcmTokenSyncObserver drains it once a valid session exists, clearing it only
// on a successful PUT. Persisted (not in-memory) so a token received right before process death
// still gets sent on the next launch. See android/CLAUDE.md.
interface FcmTokenStore {
    val pendingToken: Flow<String?>
    suspend fun savePendingToken(token: String)
    suspend fun clearPendingToken()
}

@Singleton
class DataStoreFcmTokenStore @Inject constructor(
    private val dataStore: DataStore<Preferences>
) : FcmTokenStore {

    override val pendingToken: Flow<String?> =
        dataStore.data.map { prefs -> prefs[PENDING_FCM_TOKEN_KEY] }

    override suspend fun savePendingToken(token: String) {
        dataStore.edit { prefs -> prefs[PENDING_FCM_TOKEN_KEY] = token }
    }

    override suspend fun clearPendingToken() {
        dataStore.edit { prefs -> prefs.remove(PENDING_FCM_TOKEN_KEY) }
    }

    private companion object {
        val PENDING_FCM_TOKEN_KEY = stringPreferencesKey("pending_fcm_token")
    }
}
