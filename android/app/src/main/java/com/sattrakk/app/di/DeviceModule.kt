package com.sattrakk.app.di

import com.sattrakk.app.data.device.AndroidArCompatibilityChecker
import com.sattrakk.app.data.device.ArCompatibilityChecker
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
import javax.inject.Singleton

@Module
@InstallIn(SingletonComponent::class)
object DeviceModule {

    @Provides
    @Singleton
    fun provideArCompatibilityChecker(
        impl: AndroidArCompatibilityChecker
    ): ArCompatibilityChecker = impl
}
