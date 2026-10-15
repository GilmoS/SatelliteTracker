package com.sattrakk.app.ui.testerentry

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.sattrakk.app.data.repository.AuthRepository
import com.sattrakk.app.data.session.SessionManager
import com.sattrakk.app.domain.model.ApiResult
import dagger.hilt.android.lifecycle.HiltViewModel
import javax.inject.Inject
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

// Screen logic for the beta program's tester entry point (see android/CLAUDE.md's Tester Entry
// Screen section) — new, isolated logic, deliberately not folded into SessionManager or any
// existing ViewModel. Wired to the already-complete AuthRepository.register(), which already
// handles saving the raw API key on success (step 2.3's design) -- this ViewModel only reacts to
// the ApiResult and must never touch ApiKeyStore itself.
@HiltViewModel
class TesterEntryViewModel @Inject constructor(
    private val authRepository: AuthRepository,
    private val sessionManager: SessionManager
) : ViewModel() {

    private val _uiState = MutableStateFlow<TesterEntryUiState>(TesterEntryUiState.Idle)
    val uiState: StateFlow<TesterEntryUiState> = _uiState.asStateFlow()

    // No-ops while a request is already in flight -- the submit button is also disabled during
    // Loading, but this guards against a second call reaching the ViewModel some other way.
    fun register(email: String, displayName: String) {
        if (_uiState.value is TesterEntryUiState.Loading) return
        _uiState.value = TesterEntryUiState.Loading
        viewModelScope.launch {
            when (val result = authRepository.register(email, displayName)) {
                is ApiResult.Success -> {
                    // Session was RequiresReauth (no stored key) up to this point -- registration
                    // just stored a fresh one, so flip SessionManager back to Valid here rather
                    // than requiring an app restart for SatTrakkApp to notice.
                    sessionManager.markValid()
                    _uiState.value = TesterEntryUiState.Success
                }
                is ApiResult.Error -> _uiState.value = when (result.code) {
                    403 -> TesterEntryUiState.NotAllowlisted
                    409 -> TesterEntryUiState.AlreadyRegistered
                    else -> TesterEntryUiState.Error(result.message)
                }
                ApiResult.NetworkError -> _uiState.value =
                    TesterEntryUiState.Error("No internet connection. Check your connection and try again.")
                // register() is the one endpoint that runs with no stored key at all, so a 401
                // here would be a backend anomaly rather than an expected outcome -- handled the
                // same as any other unexpected failure rather than left unreachable.
                ApiResult.AuthRequired -> _uiState.value =
                    TesterEntryUiState.Error("Something went wrong. Please try again.")
            }
        }
    }
}
