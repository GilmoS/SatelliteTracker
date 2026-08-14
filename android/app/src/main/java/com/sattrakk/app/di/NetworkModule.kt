package com.sattrakk.app.di

import dagger.Module
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent

/**
 * TODO(Milestone E): provide Retrofit + the kotlinx.serialization converter here,
 * pointed at the SatelliteTracker.API base URL. No Retrofit service interfaces
 * exist yet — this app never calls N2YO/Graph directly, only our own backend.
 */
@Module
@InstallIn(SingletonComponent::class)
object NetworkModule
