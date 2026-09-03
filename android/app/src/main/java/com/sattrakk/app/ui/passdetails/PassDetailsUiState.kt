package com.sattrakk.app.ui.passdetails

import com.sattrakk.app.domain.model.Note
import com.sattrakk.app.domain.model.Pass

// Single flat state for the Pass Details Modal (Milestone E) — surfaced as a "screen" destination
// by the nav scaffolding but functions as a modal dialog over the Dashboard/Full Pass List. No
// Composable is wired to this yet (designed separately) — covered entirely by
// PassDetailsViewModelTest. See android/CLAUDE.md's "Pass Details Modal" section.
data class PassDetailsUiState(
    val pass: Pass?, // null while loading, and stays null if getPassById fails
    val notes: List<Note> = emptyList(),
    // Drives whether the note-editing dialog is shown and its mode — see EditingNoteState. The
    // notes list itself stays read-only/display-only regardless of this field's value; editing
    // always happens through the dialog, never inline in the list (confirmed design decision).
    val editingNote: EditingNoteState? = null,
    val isLoading: Boolean = true,
    val error: String? = null
)

// Whether the note dialog is open, and if so, in create-mode or edit-mode.
sealed interface EditingNoteState {
    object NewNote : EditingNoteState
    data class ExistingNote(val noteId: String, val currentContent: String) : EditingNoteState
}
