package com.sattrakk.app.ui.testerentry

// State for the beta program's tester entry screen (see TesterEntryViewModel). NotAllowlisted
// (403) and AlreadyRegistered (409) are kept as distinct states, not collapsed into a shared
// Error case, because they need different on-screen messaging and neither is retry-able the same
// way a generic Error is — see android/CLAUDE.md's Tester Entry Screen section.
sealed interface TesterEntryUiState {
    object Idle : TesterEntryUiState
    object Loading : TesterEntryUiState
    object NotAllowlisted : TesterEntryUiState // 403
    object AlreadyRegistered : TesterEntryUiState // 409
    data class Error(val message: String) : TesterEntryUiState // NetworkError or any other Error
    object Success : TesterEntryUiState
}
