package com.sattrakk.app.navigation

import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.material3.Surface
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Modifier
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.sattrakk.app.data.session.SessionManager
import com.sattrakk.app.data.session.SessionState
import com.sattrakk.app.ui.reauth.ReauthScreen
import com.sattrakk.app.ui.theme.SatTrakkTheme

// App root. Observes the single app-wide SessionManager (see data/session/SessionManager.kt) and
// swaps the entire nav graph out for a dead-end ReauthScreen the moment SafeApiCaller marks the
// stored API key invalid — modeled as state, not a one-shot event, so it's correct regardless of
// how many times this is (re)collected across recomposition/process death. `sessionManager`
// defaults to a fresh (always-Valid) instance for previews; MainActivity passes in the real
// Hilt-injected singleton.
@Composable
fun SatTrakkApp(sessionManager: SessionManager = SessionManager()) {
    SatTrakkTheme {
        Surface(modifier = Modifier.fillMaxSize()) {
            val sessionState by sessionManager.sessionState.collectAsStateWithLifecycle()
            when (sessionState) {
                SessionState.Valid -> MainNavHost()
                SessionState.RequiresReauth -> ReauthScreen()
            }
        }
    }
}
