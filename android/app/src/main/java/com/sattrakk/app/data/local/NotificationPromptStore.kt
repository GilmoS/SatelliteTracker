package com.sattrakk.app.data.local

import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.core.booleanPreferencesKey
import androidx.datastore.preferences.core.edit
import javax.inject.Inject
import javax.inject.Singleton
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.map

// Whether Dashboard has already fired its one-time POST_NOTIFICATIONS request (on the first
// successful Dashboard load — see DashboardViewModel). Persisted so the system dialog isn't
// re-triggered on every app open; once set, the Settings screen's permission card is the only
// place the tester can (re-)request it. Device-local, same tradeoff as HiddenSatellitesStore: a
// reinstall resets it, which is fine since a reinstall also resets the OS permission grant.
interface NotificationPromptStore {
    val hasRequestedNotificationPermission: Flow<Boolean>
    suspend fun markNotificationPermissionRequested()
}

@Singleton
class DataStoreNotificationPromptStore @Inject constructor(
    private val dataStore: DataStore<Preferences>
) : NotificationPromptStore {

    override val hasRequestedNotificationPermission: Flow<Boolean> =
        dataStore.data.map { prefs -> prefs[HAS_REQUESTED_KEY] ?: false }

    override suspend fun markNotificationPermissionRequested() {
        dataStore.edit { prefs -> prefs[HAS_REQUESTED_KEY] = true }
    }

    private companion object {
        val HAS_REQUESTED_KEY = booleanPreferencesKey("has_requested_notification_permission")
    }
}
