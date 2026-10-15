package com.sattrakk.app.di

import com.sattrakk.app.data.push.FcmTokenFetcher
import com.sattrakk.app.data.push.FirebaseFcmTokenFetcher
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
import javax.inject.Singleton

@Module
@InstallIn(SingletonComponent::class)
object PushModule {

    @Provides
    @Singleton
    fun provideFcmTokenFetcher(impl: FirebaseFcmTokenFetcher): FcmTokenFetcher = impl
}
