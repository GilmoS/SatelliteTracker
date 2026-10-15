package com.sattrakk.app.navigation

import android.content.Intent
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.material3.Surface
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Modifier
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.ViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.sattrakk.app.data.push.PassNotificationDeepLink
import com.sattrakk.app.data.session.SessionManager
import com.sattrakk.app.data.session.SessionState
import com.sattrakk.app.ui.testerentry.TesterEntryScreen
import com.sattrakk.app.ui.theme.SatTrakkTheme
import dagger.hilt.android.lifecycle.HiltViewModel
import javax.inject.Inject
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow

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
//
// Also holds a pass-reminder deep link (notification tap -> Pass Details) between MainActivity
// receiving it and MainNavHost consuming it. MainActivity obtains this same instance via
// `by viewModels()` — both it and hiltViewModel() here resolve against the Activity's
// ViewModelStore — so the pending link survives a config change between the two.
@HiltViewModel
class AppViewModel @Inject constructor(
    val sessionManager: SessionManager
) : ViewModel() {

    private val _pendingPassDetailsId = MutableStateFlow<String?>(null)
    val pendingPassDetailsId: StateFlow<String?> = _pendingPassDetailsId.asStateFlow()

    // No-op for any Intent that isn't a well-formed pass reminder (e.g. an ordinary launcher tap),
    // so it never clobbers a link still waiting to be consumed.
    fun onLaunchIntent(intent: Intent?) {
        PassNotificationDeepLink.passIdFrom(intent)?.let { _pendingPassDetailsId.value = it }
    }

    fun onPassDetailsDeepLinkConsumed() {
        _pendingPassDetailsId.value = null
    }
}

@Composable
fun SatTrakkApp(appViewModel: AppViewModel = hiltViewModel()) {
    SatTrakkTheme {
        Surface(modifier = Modifier.fillMaxSize()) {
            val sessionState by appViewModel.sessionManager.sessionState.collectAsStateWithLifecycle()
            val pendingPassDetailsId by appViewModel.pendingPassDetailsId.collectAsStateWithLifecycle()
            when (sessionState) {
                // Deep link under RequiresReauth: MainNavHost (and its NavController) isn't composed
                // at all, so nothing navigates and nothing can crash — the passId simply stays in
                // AppViewModel. Once the tester re-registers, SessionManager flips to Valid,
                // MainNavHost composes, and it consumes the link then (deferred, not dropped). If
                // the process dies before that, the link is lost — acceptable, the tester can reopen
                // the pass from Dashboard.
                SessionState.Valid -> MainNavHost(
                    pendingPassDetailsId = pendingPassDetailsId,
                    onPendingPassDetailsConsumed = appViewModel::onPassDetailsDeepLinkConsumed,
                )
                SessionState.RequiresReauth -> TesterEntryScreen()
            }
        }
    }
}
