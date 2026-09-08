@file:OptIn(ExperimentalMaterial3Api::class)

package com.sattrakk.app.ui.passdetails

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Surface
import androidx.compose.material3.Switch
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.sattrakk.app.domain.model.Note
import com.sattrakk.app.domain.model.Pass
import com.sattrakk.app.navigation.BackArrowIcon
import com.sattrakk.app.navigation.PlusIcon
import com.sattrakk.app.navigation.TrashIcon
import com.sattrakk.app.ui.theme.TelemetryTextStyle
import java.time.OffsetDateTime
import java.time.ZoneId
import java.time.ZoneOffset
import java.time.format.DateTimeFormatter
import kotlin.math.roundToInt

// Real Pass Details Modal content, replacing the one-line placeholder. Wired to the already-
// complete PassDetailsViewModel/PassDetailsUiState (see android/CLAUDE.md's "Pass Details Modal"
// section) per the code truth map's Screen 7/8 verdicts and this task's confirmed decisions:
// - The header ground-track sparkline is on hold (not built) — see android/CLAUDE.md.
// - "Add to Outlook" becomes a stubbed "Export to calendar (ICS)" button (no CalendarRepository
//   exists yet — same pattern as SettingsViewModel.addSatellite()).
// - "Set alert" is a plain boolean Switch on Pass.notify, not a per-pass minute picker.
// - Notes are dialog-based multi-note CRUD (EditingNoteState), not a single inline field.
// Registered as a `dialog(...)` nav destination (see SatTrakkNavHost), so this composable floats
// over whatever's behind it as a card rather than assuming full-screen chrome (no top app bar).
@Composable
fun PassDetailsScreen(
    modifier: Modifier = Modifier,
    viewModel: PassDetailsViewModel = hiltViewModel(),
    onBackClick: () -> Unit = {},
    onNavigateToMap: (passId: String) -> Unit = {},
) {
    val state by viewModel.uiState.collectAsStateWithLifecycle()
    val snackbarHostState = remember { SnackbarHostState() }

    // First Channel-based event consumer in the app (see PassDetailsEvent's doc comment) — the Map
    // screen has no listener of its own yet, so onNavigateToMap just forwards the passId to
    // whatever the caller (SatTrakkNavHost) wants to do with it.
    LaunchedEffect(Unit) {
        viewModel.events.collect { event ->
            when (event) {
                is PassDetailsEvent.NavigateToMap -> onNavigateToMap(event.passId)
            }
        }
    }

    // "Export to calendar (ICS)" stub message — same consume-then-clear snackbar pattern as
    // SettingsScreen's "Add satellite" stub.
    LaunchedEffect(state.stubMessage) {
        val message = state.stubMessage ?: return@LaunchedEffect
        snackbarHostState.showSnackbar(message)
        viewModel.consumeStubMessage()
    }

    Box(modifier = modifier.fillMaxSize().padding(20.dp), contentAlignment = Alignment.Center) {
        Surface(
            shape = MaterialTheme.shapes.large,
            color = MaterialTheme.colorScheme.surface,
            tonalElevation = 3.dp,
            modifier = Modifier.fillMaxWidth().heightIn(max = 640.dp),
        ) {
            Column {
                HeaderRow(
                    satelliteName = state.satelliteName,
                    orbitNumber = state.pass?.orbitNumber,
                    noradId = state.satelliteNoradId,
                    maxElevation = state.pass?.maxElevation,
                    onBackClick = onBackClick,
                )
                Box(modifier = Modifier.fillMaxWidth()) {
                    when {
                        state.isLoading -> LoadingBody()
                        state.pass == null -> FullErrorBody(state.error ?: "Failed to load this pass.")
                        else -> PassDetailsBody(
                            state = state,
                            pass = state.pass!!,
                            onToggleNotify = viewModel::toggleNotify,
                            onExportToCalendar = viewModel::exportToCalendar,
                            onAddNote = viewModel::openNewNoteDialog,
                            onEditNote = viewModel::openEditNoteDialog,
                            onDeleteNote = viewModel::deleteNote,
                            onShowOnMap = viewModel::showOnMap,
                        )
                    }
                    SnackbarHost(snackbarHostState, modifier = Modifier.align(Alignment.BottomCenter))
                }
            }
        }
    }

    state.editingNote?.let { editing ->
        NoteEditDialog(
            editingNote = editing,
            onSave = viewModel::saveNote,
            onDismiss = viewModel::closeNoteDialog,
        )
    }
}

@Composable
private fun HeaderRow(
    satelliteName: String?,
    orbitNumber: Int?,
    noradId: Int?,
    maxElevation: Double?,
    onBackClick: () -> Unit,
) {
    Row(
        modifier = Modifier.fillMaxWidth().padding(12.dp, 14.dp, 16.dp, 4.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        IconButton(onClick = onBackClick) {
            BackArrowIcon(MaterialTheme.colorScheme.onSurface)
        }
        Column(modifier = Modifier.weight(1f).padding(start = 2.dp)) {
            Text(satelliteName ?: "Pass details", style = MaterialTheme.typography.titleLarge)
            if (orbitNumber != null) {
                Text(
                    // NORAD id is only shown once resolved from the satellite catalog — see
                    // PassDetailsViewModel.loadInitialData. "Pass #N" alone is still shown if the
                    // catalog lookup hasn't resolved yet or failed.
                    text = buildString {
                        append("Pass #$orbitNumber")
                        if (noradId != null) append(" · NORAD $noradId")
                    },
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
        }
        if (maxElevation != null) {
            Surface(shape = MaterialTheme.shapes.small, color = MaterialTheme.colorScheme.primaryContainer) {
                Text(
                    text = "${maxElevation.roundToInt()}° MAX ELEV",
                    style = MaterialTheme.typography.labelMedium,
                    color = MaterialTheme.colorScheme.onPrimaryContainer,
                    modifier = Modifier.padding(horizontal = 10.dp, vertical = 5.dp),
                )
            }
        }
    }
}

@Composable
private fun LoadingBody() {
    Box(modifier = Modifier.fillMaxWidth().padding(40.dp), contentAlignment = Alignment.Center) {
        CircularProgressIndicator()
    }
}

// Full-state error — mirrors PassDetailsViewModel's getPassById-failure asymmetry: without the
// pass itself there's nothing else worth rendering in the body (see android/CLAUDE.md).
@Composable
private fun FullErrorBody(message: String) {
    Text(
        text = message,
        style = MaterialTheme.typography.bodyLarge,
        color = MaterialTheme.colorScheme.error,
        modifier = Modifier.fillMaxWidth().padding(24.dp),
    )
}

@Composable
private fun PassDetailsBody(
    state: PassDetailsUiState,
    pass: Pass,
    onToggleNotify: () -> Unit,
    onExportToCalendar: () -> Unit,
    onAddNote: () -> Unit,
    onEditNote: (String) -> Unit,
    onDeleteNote: (String) -> Unit,
    onShowOnMap: () -> Unit,
) {
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .verticalScroll(rememberScrollState())
            .padding(16.dp, 4.dp, 16.dp, 20.dp),
    ) {
        // Partial-content error (notes or satellite-lookup failure) — the pass itself still
        // rendered normally below, per PassDetailsViewModel's asymmetry (see android/CLAUDE.md).
        state.error?.let { error ->
            Text(
                text = error,
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.error,
                modifier = Modifier.fillMaxWidth().padding(bottom = 10.dp),
            )
        }

        // Both AOS and LOS derive from the same OffsetDateTime pair; a pass never spans a UTC day
        // boundary long enough to matter for this one date line, so AOS's own local date is used.
        Text(
            text = formatDateLocal(pass.aos),
            style = MaterialTheme.typography.titleSmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
        Spacer(modifier = Modifier.height(8.dp))
        AosLosRow(pass)
        Spacer(modifier = Modifier.height(18.dp))
        MetricGrid(pass)
        Spacer(modifier = Modifier.height(20.dp))
        NotifyRow(notify = pass.notify, onToggle = onToggleNotify)
        Spacer(modifier = Modifier.height(12.dp))
        Button(onClick = onExportToCalendar, modifier = Modifier.fillMaxWidth()) {
            Text("Export to calendar (ICS)")
        }
        Spacer(modifier = Modifier.height(24.dp))
        NotesSection(notes = state.notes, onAddNote = onAddNote, onEditNote = onEditNote, onDeleteNote = onDeleteNote)
        Spacer(modifier = Modifier.height(24.dp))
        OutlinedButton(onClick = onShowOnMap, modifier = Modifier.fillMaxWidth()) {
            Text("Show on map")
        }
    }
}

@Composable
private fun AosLosRow(pass: Pass) {
    Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
        AosLosColumn(label = "AOS", dateTime = pass.aos, alignEnd = false)
        AosLosColumn(label = "LOS", dateTime = pass.los, alignEnd = true)
    }
}

@Composable
private fun AosLosColumn(label: String, dateTime: OffsetDateTime, alignEnd: Boolean) {
    Column(horizontalAlignment = if (alignEnd) Alignment.End else Alignment.Start) {
        Text(label, style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
        Text(
            text = "${formatTimeLocal(dateTime)} local",
            style = TelemetryTextStyle.copy(fontSize = 15.sp),
            modifier = Modifier.padding(top = 2.dp),
        )
        Text(
            text = "${formatTimeUtc(dateTime)} UTC",
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
    }
}

// Local copy of Dashboard's metric-card layout (see FullPassListScreen's own doc comment on why
// this isn't factored into a shared file — this task's scope excludes touching Dashboard).
@Composable
private fun MetricGrid(pass: Pass) {
    Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
        Row(horizontalArrangement = Arrangement.spacedBy(8.dp), modifier = Modifier.fillMaxWidth()) {
            MetricCell("DURATION", formatDuration(pass.durationSec), Modifier.weight(1f))
            MetricCell("AOS AZ", "${pass.aosAzimuth.roundToInt()}°", Modifier.weight(1f))
            MetricCell("LOS AZ", "${pass.losAzimuth.roundToInt()}°", Modifier.weight(1f))
        }
        Row(horizontalArrangement = Arrangement.spacedBy(8.dp), modifier = Modifier.fillMaxWidth()) {
            MetricCell("ORBIT", "#${pass.orbitNumber}", Modifier.weight(1f))
            MetricCell(
                "MAX ELEV",
                "${pass.maxElevation.roundToInt()}°",
                Modifier.weight(1f),
                valueColor = MaterialTheme.colorScheme.primary,
            )
        }
    }
}

@Composable
private fun MetricCell(
    label: String,
    value: String,
    modifier: Modifier = Modifier,
    valueColor: Color = Color.Unspecified,
) {
    Card(
        shape = MaterialTheme.shapes.medium,
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceContainerHigh),
        modifier = modifier,
    ) {
        Column(
            modifier = Modifier.padding(vertical = 10.dp, horizontal = 6.dp),
            horizontalAlignment = Alignment.CenterHorizontally,
        ) {
            Text(label, style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
            Text(
                value,
                style = TelemetryTextStyle.copy(fontSize = 14.sp),
                color = if (valueColor == Color.Unspecified) MaterialTheme.colorScheme.onSurface else valueColor,
                modifier = Modifier.padding(top = 5.dp),
            )
        }
    }
}

// Per confirmed decision #4: a plain boolean toggle on Pass.notify, not a per-pass minute picker
// — the tester's actual alert-timing minutes are a separate, global Settings-screen preference.
@Composable
private fun NotifyRow(notify: Boolean, onToggle: () -> Unit) {
    Row(
        modifier = Modifier.fillMaxWidth(),
        horizontalArrangement = Arrangement.SpaceBetween,
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Text("Notify me about this pass", style = MaterialTheme.typography.bodyLarge)
        Switch(checked = notify, onCheckedChange = { onToggle() })
    }
}

// Per confirmed decision #5: the already-built dialog-based multi-note CRUD (EditingNoteState),
// not the design's single inline field with a char counter.
@Composable
private fun NotesSection(
    notes: List<Note>,
    onAddNote: () -> Unit,
    onEditNote: (String) -> Unit,
    onDeleteNote: (String) -> Unit,
) {
    Column {
        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically,
        ) {
            Text("Notes", style = MaterialTheme.typography.titleSmall, color = MaterialTheme.colorScheme.primary)
            TextButton(onClick = onAddNote) {
                PlusIcon(MaterialTheme.colorScheme.primary, modifier = Modifier.size(16.dp))
                Spacer(modifier = Modifier.width(4.dp))
                Text("Add note")
            }
        }
        if (notes.isEmpty()) {
            Text(
                text = "No notes yet.",
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.padding(top = 4.dp, bottom = 4.dp),
            )
        } else {
            Column(verticalArrangement = Arrangement.spacedBy(6.dp), modifier = Modifier.padding(top = 4.dp)) {
                notes.forEach { note ->
                    NoteRow(note = note, onClick = { onEditNote(note.id) }, onDelete = { onDeleteNote(note.id) })
                }
            }
        }
    }
}

@Composable
private fun NoteRow(note: Note, onClick: () -> Unit, onDelete: () -> Unit) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(12.dp))
            .background(MaterialTheme.colorScheme.surfaceContainerLow)
            .clickable(onClick = onClick)
            .padding(start = 12.dp, end = 4.dp, top = 10.dp, bottom = 10.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Text(
            text = note.content,
            style = MaterialTheme.typography.bodyMedium,
            modifier = Modifier.weight(1f),
        )
        IconButton(onClick = onDelete) {
            TrashIcon(MaterialTheme.colorScheme.onSurfaceVariant)
        }
    }
}

@Composable
private fun NoteEditDialog(
    editingNote: EditingNoteState,
    onSave: (String) -> Unit,
    onDismiss: () -> Unit,
) {
    var content by remember(editingNote) {
        mutableStateOf(
            when (editingNote) {
                EditingNoteState.NewNote -> ""
                is EditingNoteState.ExistingNote -> editingNote.currentContent
            }
        )
    }
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text(if (editingNote is EditingNoteState.NewNote) "Add note" else "Edit note") },
        text = {
            OutlinedTextField(
                value = content,
                onValueChange = { content = it },
                modifier = Modifier.fillMaxWidth(),
                minLines = 3,
            )
        },
        confirmButton = {
            TextButton(onClick = { onSave(content) }, enabled = content.isNotBlank()) {
                Text("Save")
            }
        },
        dismissButton = {
            TextButton(onClick = onDismiss) { Text("Cancel") }
        },
    )
}

private fun formatDateLocal(dateTime: OffsetDateTime): String =
    dateTime.atZoneSameInstant(ZoneId.systemDefault()).format(DateTimeFormatter.ofPattern("EEE, MMM d yyyy"))

private fun formatTimeLocal(dateTime: OffsetDateTime): String =
    dateTime.atZoneSameInstant(ZoneId.systemDefault()).format(DateTimeFormatter.ofPattern("HH:mm"))

private fun formatTimeUtc(dateTime: OffsetDateTime): String =
    dateTime.withOffsetSameInstant(ZoneOffset.UTC).format(DateTimeFormatter.ofPattern("HH:mm"))

private fun formatDuration(totalSeconds: Int): String {
    val minutes = totalSeconds / 60
    val seconds = totalSeconds % 60
    return "${minutes}m ${seconds.toString().padStart(2, '0')}s"
}
