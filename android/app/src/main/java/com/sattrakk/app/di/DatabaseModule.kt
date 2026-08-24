package com.sattrakk.app.di

import android.content.Context
import androidx.room.Room
import com.sattrakk.app.data.local.AppDatabase
import com.sattrakk.app.data.local.PassDao
import com.sattrakk.app.data.local.SatelliteDao
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.android.qualifiers.ApplicationContext
import dagger.hilt.components.SingletonComponent
import javax.inject.Singleton

@Module
@InstallIn(SingletonComponent::class)
object DatabaseModule {

    private const val DATABASE_NAME = "sattrakk.db"

    @Provides
    @Singleton
    fun provideAppDatabase(@ApplicationContext context: Context): AppDatabase =
        Room.databaseBuilder(context, AppDatabase::class.java, DATABASE_NAME).build()

    @Provides
    fun providePassDao(database: AppDatabase): PassDao = database.passDao()

    @Provides
    fun provideSatelliteDao(database: AppDatabase): SatelliteDao = database.satelliteDao()
}
