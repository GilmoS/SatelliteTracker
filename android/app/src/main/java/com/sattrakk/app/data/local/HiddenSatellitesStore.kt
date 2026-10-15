package com.sattrakk.app.data.local

import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.stringSetPreferencesKey
import javax.inject.Inject
import javax.inject.Singleton
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.map

// Which satellites are hidden from the Dashboard — a purely visual/local preference. Deliberately
// NOT synced to the backend, NOT part of UserSettings/SettingsRepository, and NOT stored in Room
// (a simple string-set preference, not structured/relational data). Survives app restarts (unlike
// in-memory ViewModel state) but is device-local only: a reinstall or device switch resets it to
// "nothing hidden" — an accepted, deliberate tradeoff, not a bug. See android/CLAUDE.md.
interface HiddenSatellitesStore {
    val hiddenSatelliteIds: Flow<Set<String>>
    suspend fun setHidden(satelliteId: String, hidden: Boolean)
}

@Singleton
class DataStoreHiddenSatellitesStore @Inject constructor(
    private val dataStore: DataStore<Preferences>
) : HiddenSatellitesStore {

    override val hiddenSatelliteIds: Flow<Set<String>> =
        dataStore.data.map { prefs -> prefs[HIDDEN_SATELLITE_IDS_KEY] ?: emptySet() }

    override suspend fun setHidden(satelliteId: String, hidden: Boolean) {
        dataStore.edit { prefs ->
            val current = prefs[HIDDEN_SATELLITE_IDS_KEY] ?: emptySet()
            prefs[HIDDEN_SATELLITE_IDS_KEY] = if (hidden) current + satelliteId else current - satelliteId
        }
    }

    private companion object {
        val HIDDEN_SATELLITE_IDS_KEY = stringSetPreferencesKey("hidden_satellite_ids")
    }
}
