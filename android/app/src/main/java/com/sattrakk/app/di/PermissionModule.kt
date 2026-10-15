package com.sattrakk.app.di

import com.sattrakk.app.data.permission.AndroidNotificationPermissionManager
import com.sattrakk.app.data.permission.NotificationPermissionManager
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
import javax.inject.Singleton

@Module
@InstallIn(SingletonComponent::class)
object PermissionModule {

    @Provides
    @Singleton
    fun provideNotificationPermissionManager(
        impl: AndroidNotificationPermissionManager
    ): NotificationPermissionManager = impl
}
