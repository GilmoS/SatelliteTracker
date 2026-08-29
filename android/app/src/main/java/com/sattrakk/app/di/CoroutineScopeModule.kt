package com.sattrakk.app.di

import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
import javax.inject.Qualifier
import javax.inject.Singleton
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob

// Marks the process-lifetime CoroutineScope for fire-and-forget work that must outlive whichever
// caller triggered it — e.g. PassRepository.getPassById's background list-refresh convenience
// call, which must not be cancelled just because the ViewModel that called getPassById (e.g. one
// scoped to a pass-details dialog destination) is cleared first. viewModelScope can't provide
// this since it dies with its ViewModel. SupervisorJob so a failure in one piece of fire-and-forget
// work never cancels the scope or any sibling work.
@Qualifier
@Retention(AnnotationRetention.BINARY)
annotation class ApplicationScope

@Module
@InstallIn(SingletonComponent::class)
object CoroutineScopeModule {

    @Provides
    @Singleton
    @ApplicationScope
    fun provideApplicationScope(): CoroutineScope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
}
