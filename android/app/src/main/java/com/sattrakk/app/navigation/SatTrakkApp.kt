package com.sattrakk.app.navigation

import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.material3.Surface
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Modifier
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.ViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.sattrakk.app.data.session.SessionManager
import com.sattrakk.app.data.session.SessionState
import com.sattrakk.app.ui.testerentry.TesterEntryScreen
import com.sattrakk.app.ui.theme.SatTrakkTheme
import dagger.hilt.android.lifecycle.HiltViewModel
import javax.inject.Inject

// App root. Observes the single app-wide SessionManager (see data/session/SessionManager.kt) and
// swaps the entire nav graph out for TesterEntryScreen (the beta program's tester entry point —
// see android/CLAUDE.md) the moment SafeApiCaller marks the stored API key invalid, or
// SessionManager's own startup check finds no stored key at all — modeled as state, not a
// one-shot event, so it's correct regardless of how many times this is (re)collected across
// recomposition/process death. TesterEntryScreen's own ViewModel calls SessionManager.markValid()
// on a successful registration, which flips this back to MainNavHost with no explicit navigation
// call needed here. Held behind a thin AppViewModel (rather than SatTrakkApp taking a
// SessionManager parameter directly, the pre-startup-check shape) purely so hiltViewModel() can
// supply the real Hilt-injected singleton without MainActivity needing its own
// @Inject lateinit var SessionManager field.
@HiltViewModel
class AppViewModel @Inject constructor(
    val sessionManager: SessionManager
) : ViewModel()

@Composable
fun SatTrakkApp(appViewModel: AppViewModel = hiltViewModel()) {
    SatTrakkTheme {
        Surface(modifier = Modifier.fillMaxSize()) {
            val sessionState by appViewModel.sessionManager.sessionState.collectAsStateWithLifecycle()
            when (sessionState) {
                SessionState.Valid -> MainNavHost()
                SessionState.RequiresReauth -> TesterEntryScreen()
            }
        }
    }
}
