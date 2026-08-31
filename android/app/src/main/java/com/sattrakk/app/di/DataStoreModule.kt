package com.sattrakk.app.di

import android.content.Context
import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.preferencesDataStore
import com.sattrakk.app.data.local.DataStoreHiddenSatellitesStore
import com.sattrakk.app.data.local.HiddenSatellitesStore
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.android.qualifiers.ApplicationContext
import dagger.hilt.components.SingletonComponent
import javax.inject.Singleton

// Standard `by preferencesDataStore(name = ...)` Context extension — ties one DataStore<Preferences>
// instance to the Application Context, matching the singleton lifetime DatabaseModule gives
// AppDatabase. A single file backs every local preference key (currently just hidden satellite
// ids); a second key added later reuses this same instance rather than a second DataStore file.
private val Context.settingsDataStore: DataStore<Preferences> by preferencesDataStore(name = "sattrakk_settings")

@Module
@InstallIn(SingletonComponent::class)
object DataStoreModule {

    @Provides
    @Singleton
    fun providePreferencesDataStore(@ApplicationContext context: Context): DataStore<Preferences> =
        context.settingsDataStore

    // Binds the interface to its DataStore-backed implementation. No @Binds abstract-class module
    // exists elsewhere in this codebase (every DI module here is an `object` with @Provides), so
    // this follows that same style rather than introducing the pattern for the first time — Dagger
    // already knows how to construct DataStoreHiddenSatellitesStore via its own @Inject
    // constructor; this just exposes it as the interface type.
    @Provides
    @Singleton
    fun provideHiddenSatellitesStore(impl: DataStoreHiddenSatellitesStore): HiddenSatellitesStore = impl
}
