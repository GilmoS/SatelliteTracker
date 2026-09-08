package com.sattrakk.app.ui.passdetails

import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.sattrakk.app.data.repository.NotesRepository
import com.sattrakk.app.data.repository.PassRepository
import com.sattrakk.app.data.repository.SatelliteRepository
import com.sattrakk.app.domain.model.ApiResult
import dagger.hilt.android.lifecycle.HiltViewModel
import javax.inject.Inject
import kotlinx.coroutines.async
import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.coroutineScope
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.receiveAsFlow
import kotlinx.coroutines.launch

// Screen state + orchestration for the Pass Details Modal (Milestone E) — the last piece of Step
// 3's ViewModel/UiState layer. Set up as a "screen" destination by the nav scaffolding but
// functions as a modal dialog. No Composable is wired to this yet — covered entirely by
// PassDetailsViewModelTest. See android/CLAUDE.md's "Pass Details Modal" section.
//
// passId comes from a nav arg via SavedStateHandle, same pattern as FullPassListViewModel's
// satelliteId/satelliteName args.
@HiltViewModel
class PassDetailsViewModel @Inject constructor(
    private val passRepository: PassRepository,
    private val notesRepository: NotesRepository,
    private val satelliteRepository: SatelliteRepository,
    savedStateHandle: SavedStateHandle
) : ViewModel() {

    private val passId: String = requireNotNull(savedStateHandle["passId"]) { "passId nav arg" }

    private val _uiState = MutableStateFlow(PassDetailsUiState(pass = null))
    val uiState: StateFlow<PassDetailsUiState> = _uiState.asStateFlow()

    private val _events = Channel<PassDetailsEvent>(Channel.BUFFERED)
    val events: Flow<PassDetailsEvent> = _events.receiveAsFlow()

    init {
        viewModelScope.launch { loadInitialData() }
    }

    // Pass, notes, and the satellite catalog are fetched in parallel (same async/awaitAll shape as
    // DashboardViewModel's per-tab loading and FullPassListViewModel's loadAll). Per your
    // confirmation: without the pass itself there's not much value in showing partial content, so
    // a getPassById failure blanks the whole screen into an error state — notes and satellite
    // lookup are discarded even if they loaded fine. A getNotes or satellite-lookup failure alone
    // is treated as partial content instead, since the pass details are the primary content and
    // both notes and the satellite name/NORAD id are secondary.
    //
    // Satellite name/NORAD id resolution: PassDetailsUiState previously had no satellite lookup at
    // all (Pass only carries an opaque satelliteId) — this is a real, confirmed gap, not something
    // silently added. It's resolved the same way Dashboard/Settings already do it: fetch the full
    // catalog via SatelliteRepository.getSatellites() (24h-TTL, Room-cached — see
    // android/CLAUDE.md) and match by id, rather than inventing a new by-id repository method.
    private suspend fun loadInitialData() = coroutineScope {
        val passDeferred = async { passRepository.getPassById(passId) }
        val notesDeferred = async { notesRepository.getNotes(passId) }
        val satellitesDeferred = async { satelliteRepository.getSatellites() }
        val passResult = passDeferred.await()
        val notesResult = notesDeferred.await()
        val satellitesResult = satellitesDeferred.await()

        if (passResult !is ApiResult.Success) {
            _uiState.value = _uiState.value.copy(
                pass = null,
                satelliteName = null,
                satelliteNoradId = null,
                notes = emptyList(),
                isLoading = false,
                error = errorMessageFor(passResult)
            )
            return@coroutineScope
        }

        val notes = (notesResult as? ApiResult.Success)?.data ?: emptyList()
        val satellite = (satellitesResult as? ApiResult.Success)?.data
            ?.firstOrNull { it.id == passResult.data.satelliteId }

        _uiState.value = _uiState.value.copy(
            pass = passResult.data,
            satelliteName = satellite?.name,
            satelliteNoradId = satellite?.noradId,
            notes = notes,
            isLoading = false,
            error = when {
                notesResult !is ApiResult.Success -> errorMessageFor(notesResult)
                satellitesResult !is ApiResult.Success -> errorMessageFor(satellitesResult)
                else -> null
            }
        )
    }

    // No optimistic flip — the pass's notify value only changes once the repository confirms the
    // toggle. On failure, the general error field is set and pass is left exactly as it was.
    fun toggleNotify() {
        val currentPass = _uiState.value.pass ?: return
        viewModelScope.launch {
            when (val result = passRepository.setNotify(passId, !currentPass.notify)) {
                is ApiResult.Success -> _uiState.value = _uiState.value.copy(
                    pass = currentPass.copy(notify = result.data.notify),
                    error = null
                )
                else -> _uiState.value = _uiState.value.copy(error = errorMessageFor(result))
            }
        }
    }

    fun openNewNoteDialog() {
        _uiState.value = _uiState.value.copy(editingNote = EditingNoteState.NewNote)
    }

    fun openEditNoteDialog(noteId: String) {
        val note = _uiState.value.notes.firstOrNull { it.id == noteId } ?: return
        _uiState.value = _uiState.value.copy(
            editingNote = EditingNoteState.ExistingNote(noteId = note.id, currentContent = note.content)
        )
    }

    // Discards any in-progress (unsaved) edit — no draft persistence, and no repository call.
    fun closeNoteDialog() {
        _uiState.value = _uiState.value.copy(editingNote = null)
    }

    // Behavior depends on the current editingNote mode. On success, closes the dialog and patches
    // the notes list with the repository's own returned Note directly (NotesRepository already
    // returns the created/updated Note — see NotesRepository.createNote/updateNote), rather than
    // re-fetching via getNotes. On failure (including NetworkError — a general error is sufficient
    // for now, per your decision, no dedicated "requires connection" state), the dialog is left
    // OPEN and editingNote is left untouched so the user's typed content isn't lost and they can
    // retry without retyping.
    fun saveNote(content: String) {
        val editing = _uiState.value.editingNote ?: return
        viewModelScope.launch {
            val result = when (editing) {
                EditingNoteState.NewNote -> notesRepository.createNote(passId, content)
                is EditingNoteState.ExistingNote -> notesRepository.updateNote(editing.noteId, content)
            }
            when (result) {
                is ApiResult.Success -> {
                    val savedNote = result.data
                    val updatedNotes = if (_uiState.value.notes.any { it.id == savedNote.id }) {
                        _uiState.value.notes.map { if (it.id == savedNote.id) savedNote else it }
                    } else {
                        _uiState.value.notes + savedNote
                    }
                    _uiState.value = _uiState.value.copy(
                        notes = updatedNotes,
                        editingNote = null,
                        error = null
                    )
                }
                else -> _uiState.value = _uiState.value.copy(error = errorMessageFor(result))
            }
        }
    }

    fun deleteNote(noteId: String) {
        viewModelScope.launch {
            when (val result = notesRepository.deleteNote(noteId)) {
                is ApiResult.Success -> _uiState.value = _uiState.value.copy(
                    notes = _uiState.value.notes.filterNot { it.id == noteId },
                    error = null
                )
                else -> _uiState.value = _uiState.value.copy(error = errorMessageFor(result))
            }
        }
    }

    // Pure one-shot signal, no state mutation. The Map screen (step 6) has no listener wired to
    // this yet — expected, not a gap.
    fun showOnMap() {
        viewModelScope.launch { _events.send(PassDetailsEvent.NavigateToMap(passId)) }
    }

    // "Export to calendar (ICS)" stub — per the confirmed decision, built the same way
    // SettingsViewModel.addSatellite() is stubbed: no CalendarRepository/backing action exists yet
    // on the Android side (a confirmed gap, to be built in a separate future task), so this only
    // sets stubMessage and never calls a repository.
    fun exportToCalendar() {
        _uiState.value = _uiState.value.copy(stubMessage = "Exporting to calendar isn't available yet")
    }

    fun consumeStubMessage() {
        _uiState.value = _uiState.value.copy(stubMessage = null)
    }

    fun clearError() {
        _uiState.value = _uiState.value.copy(error = null)
    }

    private fun errorMessageFor(result: ApiResult<*>): String = when (result) {
        is ApiResult.Error -> result.message
        ApiResult.AuthRequired -> "Authentication required."
        ApiResult.NetworkError -> "No network connection."
        is ApiResult.Success -> error("errorMessageFor called with a Success result")
    }
}
