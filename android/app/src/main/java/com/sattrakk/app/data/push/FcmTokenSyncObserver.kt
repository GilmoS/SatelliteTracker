package com.sattrakk.app.data.push

import com.sattrakk.app.data.local.FcmTokenStore
import com.sattrakk.app.data.repository.SettingsRepository
import com.sattrakk.app.data.session.SessionManager
import com.sattrakk.app.data.session.SessionState
import com.sattrakk.app.di.ApplicationScope
import com.sattrakk.app.domain.model.ApiResult
import javax.inject.Inject
import javax.inject.Singleton
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.Job
import kotlinx.coroutines.flow.collectLatest
import kotlinx.coroutines.flow.combine
import kotlinx.coroutines.flow.distinctUntilChanged
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.launch

// Process-lifetime bridge between FcmTokenStore's pending slot and PUT /api/settings/me/fcm-token.
// Started exactly once from SatTrakkApplication.onCreate, on the shared @ApplicationScope scope
// (the same one PassRepository.getPassById's background refresh uses) — no ViewModel lives long
// enough for this. See android/CLAUDE.md's FCM section.
//
// - Sends only while SessionState is Valid AND a token is pending; RequiresReauth with a token
//   pending does nothing (the PUT would just 401), and the token simply waits.
// - Clears the pending token only after a successful PUT. Any failure (NetworkError, 5xx,
//   AuthRequired) leaves it in place — the retry is the next trigger: the next app launch (the
//   observer's first emission re-reads both values), a new token arriving, or the session flipping
//   back to Valid. There is deliberately no timed retry loop.
// - On a RequiresReauth -> Valid transition (a tester just (re-)registered), also asks FCM for the
//   current token: a re-registration creates a brand-new ApiKey whose UserSettings row has no
//   token, and a token synced under the old key was already cleared from the pending slot, so
//   without this the backend would never learn it until FCM happened to rotate it.
@Singleton
class FcmTokenSyncObserver @Inject constructor(
    private val sessionManager: SessionManager,
    private val fcmTokenStore: FcmTokenStore,
    private val settingsRepository: SettingsRepository,
    private val fcmTokenFetcher: FcmTokenFetcher,
    @ApplicationScope private val applicationScope: CoroutineScope
) {
    private var syncJob: Job? = null
    private var reauthJob: Job? = null

    @OptIn(ExperimentalCoroutinesApi::class)
    fun start() {
        if (syncJob != null) return

        syncJob = applicationScope.launch {
            combine(sessionManager.sessionState, fcmTokenStore.pendingToken) { state, token -> state to token }
                .distinctUntilChanged()
                // collectLatest: a newer token arriving mid-PUT cancels the stale send.
                .collectLatest { (state, token) ->
                    if (state == SessionState.Valid && token != null) sync(token)
                }
        }

        reauthJob = applicationScope.launch {
            var previous: SessionState? = null
            sessionManager.sessionState.collect { state ->
                if (previous == SessionState.RequiresReauth && state == SessionState.Valid) {
                    fcmTokenFetcher.fetchToken()
                }
                previous = state
            }
        }
    }

    private suspend fun sync(token: String) {
        if (settingsRepository.updateFcmToken(token) !is ApiResult.Success) return
        // Only clear if nothing newer was saved while the PUT was in flight — otherwise the newer
        // token would be dropped without ever being sent.
        if (fcmTokenStore.pendingToken.first() == token) fcmTokenStore.clearPendingToken()
    }
}
