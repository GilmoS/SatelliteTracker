package com.sattrakk.app.di

import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
import java.time.Clock
import javax.inject.Singleton

// Injected wherever "now" needs to be compared against a Pass's OffsetDateTime fields (currently
// DashboardViewModel's countdown ticker) instead of calling OffsetDateTime.now() directly, so
// tests can substitute a Clock driven by kotlinx-coroutines-test's virtual time.
@Module
@InstallIn(SingletonComponent::class)
object ClockModule {

    @Provides
    @Singleton
    fun provideClock(): Clock = Clock.systemUTC()
}
