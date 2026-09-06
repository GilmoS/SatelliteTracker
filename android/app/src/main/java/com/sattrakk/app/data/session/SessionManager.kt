package com.sattrakk.app.data.session

import javax.inject.Inject
import javax.inject.Singleton
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import com.sattrakk.app.data.local.ApiKeyStore

// Session invalidation is modeled as state, not a one-shot event stream — per current Android
// guidance (state-driven UI), a future root composable observes `sessionState` and reacts to
// RequiresReauth however many times it's collected, rather than consuming a single navigation
// event that could be missed across process/config changes.
sealed interface SessionState {
    object Valid : SessionState
    object RequiresReauth : SessionState
}

// The single app-wide source of truth for "does the stored API key still work." Its initial
// value is read from ApiKeyStore at construction time, so a fresh install / logged-out device
// starts in RequiresReauth without needing a first failed request to discover that. The only
// other writer of RequiresReauth is SafeApiCaller (data/util/SafeApiCall.kt), at the exact point
// a 401 is mapped to ApiResult.AuthRequired — no repository calls this directly. markValid is
// called by TesterEntryViewModel after a successful registration (see android/CLAUDE.md's Tester
// Entry Screen section).
@Singleton
class SessionManager @Inject constructor(private val apiKeyStore: ApiKeyStore) {
    private val _sessionState = MutableStateFlow(
        if (apiKeyStore.getKey() != null) SessionState.Valid else SessionState.RequiresReauth
    )
    val sessionState: StateFlow<SessionState> = _sessionState.asStateFlow()

    fun markReauthRequired() {
        _sessionState.value = SessionState.RequiresReauth
    }

    fun markValid() {
        _sessionState.value = SessionState.Valid
    }
}
