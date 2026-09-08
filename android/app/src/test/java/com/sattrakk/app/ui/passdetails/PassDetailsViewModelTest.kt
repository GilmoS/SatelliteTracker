package com.sattrakk.app.ui.passdetails

import androidx.lifecycle.SavedStateHandle
import com.sattrakk.app.MainDispatcherRule
import com.sattrakk.app.data.repository.NotesRepository
import com.sattrakk.app.data.repository.PassRepository
import com.sattrakk.app.data.repository.SatelliteRepository
import com.sattrakk.app.domain.model.ApiResult
import com.sattrakk.app.domain.model.Note
import com.sattrakk.app.domain.model.NotifyStatus
import com.sattrakk.app.domain.model.Pass
import com.sattrakk.app.domain.model.Satellite
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.mockk
import java.time.OffsetDateTime
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.launch
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test

// PassDetailsViewModel has no long-lived polling/ticker coroutine (unlike DashboardViewModel), so
// its launched work always completes on its own. As with FullPassListViewModelTest, mid-test
// assertions drive mainDispatcherRule.testDispatcher.scheduler directly via runCurrent() rather
// than wrapping the body in runTest{} — see that test's comment / DashboardViewModelTest's comment
// for why runTest{} would hang once Dispatchers.Main is redirected to a TestDispatcher.
@OptIn(ExperimentalCoroutinesApi::class)
class PassDetailsViewModelTest {

    @get:Rule
    val mainDispatcherRule = MainDispatcherRule()

    private val passRepository = mockk<PassRepository>()
    private val notesRepository = mockk<NotesRepository>()
    private val satelliteRepository = mockk<SatelliteRepository>()

    private val passId = "pass-1"
    private val now: OffsetDateTime = OffsetDateTime.parse("2026-09-03T00:00:00Z")

    private fun runCurrent() = mainDispatcherRule.testDispatcher.scheduler.runCurrent()

    private fun savedStateHandle() = SavedStateHandle(mapOf("passId" to passId))

    // Default stub used by every test that doesn't specifically exercise satellite-lookup
    // behavior — a single satellite matching pass()'s satelliteId ("sat-1").
    private fun stubDefaultSatellites() {
        coEvery { satelliteRepository.getSatellites() } returns ApiResult.Success(listOf(satellite()))
    }

    private fun satellite(id: String = "sat-1", name: String = "EROS C3", noradId: Int = 43689) = Satellite(
        id = id,
        name = name,
        noradId = noradId,
        description = null,
        isActive = true,
        isDefault = true,
        createdAt = now
    )

    private fun pass(notify: Boolean = true) = Pass(
        id = passId,
        satelliteId = "sat-1",
        tleId = "tle-1",
        orbitNumber = 1,
        aos = now.plusMinutes(10),
        los = now.plusMinutes(15),
        maxElevation = 45.0,
        aosAzimuth = 10.0,
        losAzimuth = 20.0,
        durationSec = 300,
        notify = notify,
        outlookSynced = false,
        calculatedAt = now
    )

    private fun note(id: String, content: String) = Note(
        id = id,
        passId = passId,
        content = content,
        createdAt = now,
        updatedAt = now
    )

    private fun createViewModel(): PassDetailsViewModel {
        val viewModel = PassDetailsViewModel(passRepository, notesRepository, satelliteRepository, savedStateHandle())
        runCurrent()
        return viewModel
    }

    @Test
    fun `successful load populates pass, notes, satellite name and NORAD id, and clears loading`() {
        val p = pass()
        val n1 = note("n1", "first note")
        coEvery { passRepository.getPassById(passId) } returns ApiResult.Success(p)
        coEvery { notesRepository.getNotes(passId) } returns ApiResult.Success(listOf(n1))
        stubDefaultSatellites()

        val viewModel = createViewModel()

        val state = viewModel.uiState.value
        assertEquals(p, state.pass)
        assertEquals(listOf(n1), state.notes)
        assertEquals("EROS C3", state.satelliteName)
        assertEquals(43689, state.satelliteNoradId)
        assertFalse(state.isLoading)
        assertNull(state.error)
    }

    @Test
    fun `getPassById failure produces full error state with notes and satellite discarded`() {
        coEvery { passRepository.getPassById(passId) } returns ApiResult.NetworkError
        coEvery { notesRepository.getNotes(passId) } returns ApiResult.Success(listOf(note("n1", "would have loaded")))
        stubDefaultSatellites()

        val viewModel = createViewModel()

        val state = viewModel.uiState.value
        assertNull(state.pass)
        assertTrue(state.notes.isEmpty())
        assertNull(state.satelliteName)
        assertNull(state.satelliteNoradId)
        assertFalse(state.isLoading)
        assertEquals("No network connection.", state.error)
    }

    @Test
    fun `getPassById succeeds and getNotes fails shows pass with empty notes and error set`() {
        val p = pass()
        coEvery { passRepository.getPassById(passId) } returns ApiResult.Success(p)
        coEvery { notesRepository.getNotes(passId) } returns ApiResult.NetworkError
        stubDefaultSatellites()

        val viewModel = createViewModel()

        val state = viewModel.uiState.value
        assertEquals(p, state.pass)
        assertTrue(state.notes.isEmpty())
        assertEquals("No network connection.", state.error)
    }

    @Test
    fun `getPassById succeeds and satellite lookup fails shows pass with null satellite fields and error set`() {
        val p = pass()
        coEvery { passRepository.getPassById(passId) } returns ApiResult.Success(p)
        coEvery { notesRepository.getNotes(passId) } returns ApiResult.Success(emptyList())
        coEvery { satelliteRepository.getSatellites() } returns ApiResult.NetworkError

        val viewModel = createViewModel()

        val state = viewModel.uiState.value
        assertEquals(p, state.pass)
        assertNull(state.satelliteName)
        assertNull(state.satelliteNoradId)
        assertEquals("No network connection.", state.error)
    }

    @Test
    fun `no matching satellite in the catalog leaves satellite fields null without an error`() {
        val p = pass()
        coEvery { passRepository.getPassById(passId) } returns ApiResult.Success(p)
        coEvery { notesRepository.getNotes(passId) } returns ApiResult.Success(emptyList())
        coEvery { satelliteRepository.getSatellites() } returns ApiResult.Success(listOf(satellite(id = "some-other-satellite")))

        val viewModel = createViewModel()

        val state = viewModel.uiState.value
        assertEquals(p, state.pass)
        assertNull(state.satelliteName)
        assertNull(state.satelliteNoradId)
        assertNull(state.error)
    }

    @Test
    fun `toggleNotify success flips pass notify to the repository response value`() {
        val p = pass(notify = true)
        coEvery { passRepository.getPassById(passId) } returns ApiResult.Success(p)
        coEvery { notesRepository.getNotes(passId) } returns ApiResult.Success(emptyList())
        coEvery { passRepository.setNotify(passId, false) } returns ApiResult.Success(NotifyStatus(passId, false))
        stubDefaultSatellites()
        val viewModel = createViewModel()

        viewModel.toggleNotify()
        runCurrent()

        assertEquals(false, viewModel.uiState.value.pass?.notify)
        assertNull(viewModel.uiState.value.error)
    }

    @Test
    fun `toggleNotify failure sets error and leaves notify unchanged`() {
        val p = pass(notify = true)
        coEvery { passRepository.getPassById(passId) } returns ApiResult.Success(p)
        coEvery { notesRepository.getNotes(passId) } returns ApiResult.Success(emptyList())
        coEvery { passRepository.setNotify(passId, false) } returns ApiResult.NetworkError
        stubDefaultSatellites()
        val viewModel = createViewModel()

        viewModel.toggleNotify()
        runCurrent()

        assertEquals(true, viewModel.uiState.value.pass?.notify)
        assertEquals("No network connection.", viewModel.uiState.value.error)
    }

    @Test
    fun `openNewNoteDialog openEditNoteDialog and closeNoteDialog transition editingNote correctly`() {
        val p = pass()
        val n1 = note("n1", "existing content")
        coEvery { passRepository.getPassById(passId) } returns ApiResult.Success(p)
        coEvery { notesRepository.getNotes(passId) } returns ApiResult.Success(listOf(n1))
        stubDefaultSatellites()
        val viewModel = createViewModel()

        viewModel.openNewNoteDialog()
        assertEquals(EditingNoteState.NewNote, viewModel.uiState.value.editingNote)

        viewModel.openEditNoteDialog("n1")
        assertEquals(
            EditingNoteState.ExistingNote(noteId = "n1", currentContent = "existing content"),
            viewModel.uiState.value.editingNote
        )

        viewModel.closeNoteDialog()
        assertNull(viewModel.uiState.value.editingNote)
        coVerify(exactly = 0) { notesRepository.createNote(any(), any()) }
        coVerify(exactly = 0) { notesRepository.updateNote(any(), any()) }
    }

    @Test
    fun `saveNote in NewNote mode calls createNote not updateNote`() {
        val p = pass()
        coEvery { passRepository.getPassById(passId) } returns ApiResult.Success(p)
        coEvery { notesRepository.getNotes(passId) } returns ApiResult.Success(emptyList())
        val created = note("new-1", "hello")
        coEvery { notesRepository.createNote(passId, "hello") } returns ApiResult.Success(created)
        stubDefaultSatellites()
        val viewModel = createViewModel()
        viewModel.openNewNoteDialog()

        viewModel.saveNote("hello")
        runCurrent()

        coVerify(exactly = 1) { notesRepository.createNote(passId, "hello") }
        coVerify(exactly = 0) { notesRepository.updateNote(any(), any()) }
        assertNull(viewModel.uiState.value.editingNote)
        assertEquals(listOf(created), viewModel.uiState.value.notes)
    }

    @Test
    fun `saveNote in ExistingNote mode calls updateNote with the right noteId`() {
        val p = pass()
        val original = note("n1", "old content")
        coEvery { passRepository.getPassById(passId) } returns ApiResult.Success(p)
        coEvery { notesRepository.getNotes(passId) } returns ApiResult.Success(listOf(original))
        val updated = note("n1", "new content")
        coEvery { notesRepository.updateNote("n1", "new content") } returns ApiResult.Success(updated)
        stubDefaultSatellites()
        val viewModel = createViewModel()
        viewModel.openEditNoteDialog("n1")

        viewModel.saveNote("new content")
        runCurrent()

        coVerify(exactly = 1) { notesRepository.updateNote("n1", "new content") }
        coVerify(exactly = 0) { notesRepository.createNote(any(), any()) }
        assertNull(viewModel.uiState.value.editingNote)
        assertEquals(listOf(updated), viewModel.uiState.value.notes)
    }

    @Test
    fun `saveNote failure keeps dialog open with error set and content not lost`() {
        val p = pass()
        coEvery { passRepository.getPassById(passId) } returns ApiResult.Success(p)
        coEvery { notesRepository.getNotes(passId) } returns ApiResult.Success(emptyList())
        coEvery { notesRepository.createNote(passId, "hello") } returns ApiResult.NetworkError
        stubDefaultSatellites()
        val viewModel = createViewModel()
        viewModel.openNewNoteDialog()

        viewModel.saveNote("hello")
        runCurrent()

        assertEquals(EditingNoteState.NewNote, viewModel.uiState.value.editingNote)
        assertEquals("No network connection.", viewModel.uiState.value.error)
        assertTrue(viewModel.uiState.value.notes.isEmpty())
    }

    @Test
    fun `deleteNote success removes the note from the list`() {
        val p = pass()
        val n1 = note("n1", "content")
        coEvery { passRepository.getPassById(passId) } returns ApiResult.Success(p)
        coEvery { notesRepository.getNotes(passId) } returns ApiResult.Success(listOf(n1))
        coEvery { notesRepository.deleteNote("n1") } returns ApiResult.Success(Unit)
        stubDefaultSatellites()
        val viewModel = createViewModel()

        viewModel.deleteNote("n1")
        runCurrent()

        assertTrue(viewModel.uiState.value.notes.isEmpty())
        assertNull(viewModel.uiState.value.error)
    }

    @Test
    fun `deleteNote failure sets error and leaves the note in the list`() {
        val p = pass()
        val n1 = note("n1", "content")
        coEvery { passRepository.getPassById(passId) } returns ApiResult.Success(p)
        coEvery { notesRepository.getNotes(passId) } returns ApiResult.Success(listOf(n1))
        coEvery { notesRepository.deleteNote("n1") } returns ApiResult.NetworkError
        stubDefaultSatellites()
        val viewModel = createViewModel()

        viewModel.deleteNote("n1")
        runCurrent()

        assertEquals(listOf(n1), viewModel.uiState.value.notes)
        assertEquals("No network connection.", viewModel.uiState.value.error)
    }

    @Test
    fun `showOnMap emits exactly one NavigateToMap event without mutating state`() {
        val p = pass()
        coEvery { passRepository.getPassById(passId) } returns ApiResult.Success(p)
        coEvery { notesRepository.getNotes(passId) } returns ApiResult.Success(emptyList())
        stubDefaultSatellites()
        val viewModel = createViewModel()
        val stateBefore = viewModel.uiState.value

        val events = mutableListOf<PassDetailsEvent>()
        val testScope = CoroutineScope(mainDispatcherRule.testDispatcher)
        val collectJob = testScope.launch {
            viewModel.events.collect { events.add(it) }
        }
        runCurrent()

        viewModel.showOnMap()
        runCurrent()

        assertEquals(listOf(PassDetailsEvent.NavigateToMap(passId)), events)
        assertEquals(stateBefore, viewModel.uiState.value)
        collectJob.cancel()
    }

    @Test
    fun `clearError resets error without touching other fields`() {
        coEvery { passRepository.getPassById(passId) } returns ApiResult.NetworkError
        coEvery { notesRepository.getNotes(passId) } returns ApiResult.Success(emptyList())
        stubDefaultSatellites()
        val viewModel = createViewModel()
        assertEquals("No network connection.", viewModel.uiState.value.error)

        viewModel.clearError()

        val state = viewModel.uiState.value
        assertNull(state.error)
        assertNull(state.pass)
        assertTrue(state.notes.isEmpty())
        assertFalse(state.isLoading)
    }

    @Test
    fun `exportToCalendar sets a stub message without calling any repository`() {
        val p = pass()
        coEvery { passRepository.getPassById(passId) } returns ApiResult.Success(p)
        coEvery { notesRepository.getNotes(passId) } returns ApiResult.Success(emptyList())
        stubDefaultSatellites()
        val viewModel = createViewModel()

        viewModel.exportToCalendar()

        assertEquals("Exporting to calendar isn't available yet", viewModel.uiState.value.stubMessage)
    }

    @Test
    fun `consumeStubMessage clears the stub message`() {
        val p = pass()
        coEvery { passRepository.getPassById(passId) } returns ApiResult.Success(p)
        coEvery { notesRepository.getNotes(passId) } returns ApiResult.Success(emptyList())
        stubDefaultSatellites()
        val viewModel = createViewModel()
        viewModel.exportToCalendar()

        viewModel.consumeStubMessage()

        assertNull(viewModel.uiState.value.stubMessage)
    }
}
