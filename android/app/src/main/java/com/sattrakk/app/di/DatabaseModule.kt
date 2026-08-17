package com.sattrakk.app.di

import dagger.Module
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent

/**
 * TODO(Milestone E): provide the Room database + DAOs here, mirroring the
 * backend's satellites/tles/passes/notes/settings tables for offline viewing.
 */
@Module
@InstallIn(SingletonComponent::class)
object DatabaseModule
